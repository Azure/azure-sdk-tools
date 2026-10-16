import { posix } from "node:path";
import { parse } from "yaml";
import { collectSource, type SourceProvider, type ObservationContext, type NativeLibraryInput, type ExcludedNativeLibrary, type NativeSnapshot } from "./collect-source.ts";
import { generatorHeader, globMatch, object, parseMetadata, string, type JsonValue, type JsonObject } from "./metadata.ts";
import type { Category } from "./dashboard/data.ts";

type XmlNode = { name: string; text: string; attributes: Record<string, string>; children: XmlNode[] };
function xml(value: unknown): XmlNode {
  const record = object(value, "XML metadata");
  if (typeof record.name !== "string" || typeof record.text !== "string" || !Array.isArray(record.children)) throw new Error("Invalid XML metadata tree.");
  const attributes: Record<string, string> = {};
  for (const [key, value] of Object.entries(object(record.attributes, "XML attributes"))) attributes[key] = string(value, key);
  return { name: record.name, text: record.text, attributes, children: record.children.map(xml) };
}
const children = (node: XmlNode | undefined, name: string): XmlNode[] => node?.children.filter((child) => child.name === name) ?? [];
const child = (node: XmlNode | undefined, name: string) => children(node, name)[0];
const value = (node: XmlNode | undefined, name: string) => child(node, name)?.text;
function descendants(node: XmlNode | undefined, name: string): XmlNode[] {
  if (!node) return [];
  return [...children(node, name), ...node.children.flatMap((member) => descendants(member, name))];
}
function nonProduction(path: string, coordinate: string): boolean {
  return /^sdk\/(?:tools|template(?:-v2)?|parents|boms)\//.test(path) ||
    /(?:^|[-_:])(tests?|samples?|perf|benchmark|stress|customization|generator)(?:$|[-_])/.test(coordinate) ||
    coordinate === "com.azure:azure-ai-openai-stainless" || coordinate.startsWith("com.azure.tools:");
}

export async function collectJava(source: SourceProvider, context: ObservationContext, catalogText?: string): Promise<NativeSnapshot> {
  if (context.repository.name !== "Azure/azure-sdk-for-java") throw new Error("Java source requires the Java repository identity.");
  const pomPaths = source.paths.filter((path) => path === "pom.xml" || /^sdk\/.*\/pom\.xml$/.test(path)).sort();
  const records = [];
  for (const path of pomPaths) records.push({ path, kind: "xml" as const, text: await source.readText(path) });
  const decoded = parseMetadata(records);
  const poms = new Map(pomPaths.map((path) => [path, xml(decoded.get(path))]));
  const localCoordinate = (pom: XmlNode): string => `${value(pom, "groupId") || value(child(pom, "parent"), "groupId")}:${value(pom, "artifactId")}`;
  const coordinatePaths = new Map<string, string[]>();
  for (const [path, pom] of poms) {
    const coordinate = localCoordinate(pom);
    coordinatePaths.set(coordinate, [...(coordinatePaths.get(coordinate) ?? []), path]);
  }
  const lineage = (path: string, seen = new Set<string>()): { path: string; pom: XmlNode }[] => {
    if (seen.has(path)) throw new Error(`Circular Maven parent chain: ${path}.`);
    seen.add(path);
    const pom = poms.get(path);
    if (!pom) throw new Error(`Missing committed Maven POM: ${path}.`);
    const parent = child(pom, "parent");
    if (!parent) return [{ path, pom }];
    const relative = value(parent, "relativePath");
    const candidate = posix.normalize(posix.join(posix.dirname(path), relative === undefined ? "../pom.xml" : relative));
    const target = relative !== "" && poms.has(candidate) ? candidate :
      coordinatePaths.get(`${value(parent, "groupId")}:${value(parent, "artifactId")}`)?.find((entry) => entry !== path);
    if (!target) throw new Error(`Maven parent is not available in the committed source: ${path}.`);
    return [...lineage(target, seen), { path, pom }];
  };
  const categories = new Map<string, Category>();
  if (catalogText) {
    const catalog = parseMetadata([{ path: "java-catalog.csv", kind: "csv", text: catalogText }]).get("java-catalog.csv");
    if (!Array.isArray(catalog)) throw new Error("Java category catalog is invalid.");
    for (const row of catalog) {
      const record = object(row, "Java catalog row");
      if (typeof record.GroupId !== "string" || typeof record.Package !== "string" || typeof record.Type !== "string") continue;
      if (["mgmt", "client", "spring"].includes(record.Type)) {
        categories.set(`${record.GroupId}:${record.Package}`, record.Type === "mgmt" ? "management" : "data-plane");
      }
    }
  }
  const selected = new Map<string, string>();
  for (const path of source.paths.filter((path) => /^sdk\/.*\/ci[^/]*\.ya?ml$/.test(path))) {
    const parsed: unknown = parse(await source.readText(path));
    const document = object(parsed, path);
    if (!document.extends || typeof document.extends !== "object") continue;
    const extend = object(document.extends, path);
    if (!extend.parameters) continue;
    const parameters = object(extend.parameters, path);
    if (!parameters.Artifacts) continue;
    if (!Array.isArray(parameters.Artifacts)) throw new Error(`${path}: release Artifacts must be an explicit array.`);
    for (const entry of parameters.Artifacts) {
      const artifact = object(entry, path);
      const coordinate = `${string(artifact.groupId, path)}:${string(artifact.name, path)}`;
      const matches = (coordinatePaths.get(coordinate) ?? []).filter((pom) =>
        posix.dirname(pom) === posix.dirname(path) || posix.dirname(posix.dirname(pom)) === posix.dirname(path));
      if (matches.length !== 1) throw new Error(`${path}: release coordinate ${coordinate} has ${matches.length} matching POMs.`);
      const previous = selected.get(coordinate);
      if (previous && previous !== matches[0]) throw new Error(`Ambiguous release package ${coordinate}.`);
      selected.set(coordinate, matches[0]);
    }
  }
  const packages: NativeLibraryInput[] = [];
  const excluded: ExcludedNativeLibrary[] = [];
  const selectedPaths = new Set<string>();
  for (const [coordinate, projectPath] of selected) {
    selectedPaths.add(projectPath);
    const group = coordinate.split(":")[0];
    const chain = lineage(projectPath);
    const pom = chain.at(-1)!.pom;
    let reason = "";
    if (!/^com\.azure(?:\.|$)/.test(group)) reason = group.startsWith("com.microsoft.azure") ? "legacy-package" : "unbranded-package";
    else if (nonProduction(projectPath, coordinate)) reason = "non-production-package";
    else if ((value(pom, "packaging") || "jar") !== "jar") reason = "non-runtime-artifact";
    if (reason) { excluded.push({ projectPath, reason }); continue; }
    const base = posix.dirname(projectPath);
    const properties: Record<string, string> = { basedir: base, "project.basedir": base, "project.build.directory": `${base}/target` };
    for (const { pom } of chain) for (const property of child(pom, "properties")?.children ?? []) properties[property.name] = property.text;
    const resolve = (text: string): string => {
      let result = text;
      for (let iteration = 0; iteration < 20 && /\$\{/.test(result); iteration++) {
        result = result.replace(/\$\{([^}]+)\}/g, (_, key: string) => {
          const replacement = properties[key];
          if (replacement === undefined) throw new Error(`${projectPath}: unresolved Maven source property ${key}.`);
          return replacement;
        });
      }
      if (/\$\{/.test(result)) throw new Error(`${projectPath}: recursive Maven source property.`);
      return result;
    };
    const resolvePath = (text: string): string => {
      const resolved = resolve(text).replaceAll("\\", "/");
      return posix.normalize(resolved.startsWith("sdk/") ? resolved : posix.join(base, resolved));
    };
    const executions = new Map<string, { plugin: string; execution: XmlNode; configuration?: XmlNode }>();
    let sourceDirectory = `${base}/src/main/java`;
    const roots = new Set<string>();
    const copies = new Map<string, { root: string; include: string[]; exclude: string[] }[]>();
    for (const { pom } of chain) {
      const builds = [child(pom, "build"), ...children(child(pom, "profiles"), "profile")
        .filter((profile) => !/(?:test|e2e|shade|samples|javadoc|checkstyle|spotless|jacoco)/i.test(value(profile, "id") || "") &&
          (value(child(profile, "activation"), "activeByDefault") === "true" || child(child(profile, "activation"), "jdk")))
        .map((profile) => child(profile, "build"))];
      for (const build of builds) {
        const directory = value(build, "sourceDirectory");
        if (directory) {
          if (build === child(pom, "build")) sourceDirectory = resolvePath(directory);
          else roots.add(resolvePath(directory));
        }
        for (const plugin of children(child(build, "plugins"), "plugin")) {
          const name = value(plugin, "artifactId");
          if (!name) throw new Error(`${projectPath}: Maven build plugin has no identity.`);
          if (name === "scala-maven-plugin") roots.add(`${base}/src/main/scala`);
          if (name === "maven-compiler-plugin") {
            for (const root of descendants(child(plugin, "configuration"), "compileSourceRoot")) roots.add(resolvePath(root.text));
          }
          for (const execution of children(child(plugin, "executions"), "execution")) {
            const id = value(execution, "id") || "default";
            executions.set(`${name}:${id}`, { plugin: name, execution, configuration: child(execution, "configuration") || child(plugin, "configuration") });
          }
        }
      }
    }
    roots.add(sourceDirectory);
    for (const { plugin, execution, configuration } of executions.values()) {
      const goals = children(child(execution, "goals"), "goal").map((entry) => entry.text);
      if (plugin === "build-helper-maven-plugin" && goals.includes("add-source")) {
        for (const entry of children(child(configuration, "sources"), "source")) roots.add(resolvePath(entry.text));
      } else if (plugin === "maven-compiler-plugin" && goals.includes("compile")) {
        for (const entry of descendants(configuration, "compileSourceRoot")) roots.add(resolvePath(entry.text));
      } else if (plugin === "maven-resources-plugin" && goals.includes("copy-resources")) {
        const destination = value(configuration, "outputDirectory");
        if (!destination) throw new Error(`${projectPath}: source-copy destination is missing.`);
        copies.set(resolvePath(destination), children(child(configuration, "resources"), "resource").map((resource) => ({
          root: resolvePath(string(value(resource, "directory"), projectPath)),
          include: children(child(resource, "includes"), "include").map((entry) => entry.text),
          exclude: children(child(resource, "excludes"), "exclude").map((entry) => entry.text),
        })));
      }
    }
    const files = new Set<string>();
    for (const root of roots) {
      const origins = copies.get(root) || [{ root, include: [], exclude: [] }];
      for (const origin of origins) {
        if (origin.root.includes("/target/") && !copies.has(root)) continue;
        if (!origin.root.startsWith("sdk/") || origin.root.split("/").includes("..")) throw new Error(`${projectPath}: unsafe compiled source root ${origin.root}.`);
        const candidates = source.paths.filter((path) => path.startsWith(`${origin.root}/`) && /\.(java|scala)$/.test(path));
        if (!candidates.length && !origin.root.startsWith(`${base}/`)) throw new Error(`${projectPath}: declared shared source is missing: ${origin.root}.`);
        for (const path of candidates) {
          const relative = path.slice(origin.root.length + 1);
          if ((origin.include.length === 0 || origin.include.some((pattern) => globMatch(relative, pattern))) &&
              !origin.exclude.some((pattern) => globMatch(relative, pattern))) files.add(path);
        }
      }
    }
    const category = categories.get(coordinate) || (group.startsWith("com.azure.resourcemanager") ||
      coordinate === "com.azure:azure-core-management" ? "management" : "data-plane");
    packages.push({ library: coordinate, service: base.split("/")[1], category, projectPath, sourcePaths: [...files].sort() });
  }
  for (const path of pomPaths.filter((path) => path.startsWith("sdk/") && !selectedPaths.has(path))) {
    excluded.push({ projectPath: path, reason: "not-ci-artifact" });
  }
  return collectSource(source, context, packages, excluded, (_, text) =>
    generatorHeader(text, "java") ? { provenance: "generated", evidence: "auto-generated-header" } :
      { provenance: "custom", evidence: "no-generated-signal" });
}
