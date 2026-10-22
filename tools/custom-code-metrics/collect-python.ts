import { posix } from "node:path";
import { collectSource, type SourceProvider, type ObservationContext, type NativeLibraryInput, type ExcludedNativeLibrary, type NativeSnapshot } from "./collect-source.ts";
import { generatorHeader, globMatch, object, parseMetadata, string, strings, type JsonObject, type JsonValue } from "./metadata.ts";

function optionalObject(value: JsonValue | undefined, context: string): JsonObject {
  return value === undefined ? {} : object(value, context);
}
function productionPath(relative: string): boolean {
  return !relative.split("/").slice(0, -1).some((part) =>
    /^(?:tests?|generated_tests|review_tests|samples?|generated_samples|docs?|swagger|specs?|scripts|_scripts|stress|perf|__pycache__|build|dist|\.tox|\.venv)$/.test(part));
}

export async function collectPython(source: SourceProvider, context: ObservationContext): Promise<NativeSnapshot> {
  if (context.repository.name !== "Azure/azure-sdk-for-python") throw new Error("Python source requires the Python repository identity.");
  const paths = new Set(source.paths);
  const metadataPaths = source.paths.filter((path) => /^sdk\/[^/]+\/[^/]+\/(?:setup\.py|pyproject\.toml)$/.test(path)).sort();
  const records = [];
  for (const path of metadataPaths) records.push({
    path, kind: path.endsWith(".toml") ? "toml" as const : "setup" as const, text: await source.readText(path),
  });
  const decoded = parseMetadata(records);
  const roots = [...new Set(metadataPaths.map(posix.dirname))].sort();
  const packages: NativeLibraryInput[] = [];
  const excluded: ExcludedNativeLibrary[] = [];
  const identities = new Set<string>();
  for (const root of roots) {
    const tomlPath = `${root}/pyproject.toml`;
    const setupPath = `${root}/setup.py`;
    const toml = paths.has(tomlPath) ? object(decoded.get(tomlPath), tomlPath) : {};
    const project = optionalObject(toml.project, tomlPath);
    const setup = paths.has(setupPath) ? object(decoded.get(setupPath), setupPath) : {};
    const declarative = typeof project.name === "string";
    const projectPath = declarative ? tomlPath : setupPath;
    const name = string(declarative ? project.name : setup.name, projectPath).toLowerCase().replace(/[-_.]+/g, "-");
    const service = root.split("/")[1];
    let reason = "";
    if (service === "template") reason = "template";
    else if (root === "sdk/openai/azure-openai") reason = "placeholder";
    else if (root === "sdk/textanalytics/azure-ai-textanalytics") reason = "retired-package";
    else if (service === "nspkg") reason = "namespace-package";
    else if (["azure", "azure-mgmt", "azure-keyvault"].includes(name) || setup.sdist_only === true) reason = "metadata-only-package";
    else if (!name.startsWith("azure-")) reason = "unbranded-package";
    if (reason) { excluded.push({ projectPath, reason }); continue; }
    if (identities.has(name)) throw new Error(`Duplicate active Python distribution: ${name}.`);
    identities.add(name);
    const setuptools = declarative ? optionalObject(optionalObject(toml.tool, tomlPath).setuptools, tomlPath) : {};
    const selection = declarative ? setuptools.packages : setup.packages;
    if (!declarative) {
      for (const field of ["packages", "package_dir", "package_data", "exclude_package_data", "py_modules"]) {
        if (setup[`${field}_error`]) throw new Error(`${projectPath}: ${setup[`${field}_error`]}`);
      }
    }
    if (selection === undefined) throw new Error(`${projectPath}: package membership must be explicit.`);
    const packageDir = optionalObject(declarative ? setuptools["package-dir"] : setup.package_dir, projectPath);
    if (Object.keys(packageDir).some((key) => key !== "")) {
      throw new Error(`${projectPath}: package-specific directory mappings require explicit source resolution.`);
    }
    const packageData = optionalObject(declarative ? setuptools["package-data"] : setup.package_data, projectPath);
    const excludedData = optionalObject(declarative ? setuptools["exclude-package-data"] : setup.exclude_package_data, projectPath);
    const modules = declarative ? setuptools["py-modules"] : setup.py_modules;
    const explicit = Array.isArray(selection) ? strings(selection, projectPath) : null;
    const find = explicit ? null : object(object(selection, projectPath).find, projectPath);
    const where = find?.where === undefined ? [string(packageDir[""] ?? ".", projectPath)] :
      typeof find.where === "string" ? [find.where] : strings(find.where, projectPath);
    const include = find?.include === undefined ? ["*"] : strings(find.include, projectPath);
    const exclude = find?.exclude === undefined ? [] : strings(find.exclude, projectPath);
    const namespaces = declarative ? find?.namespaces !== false : find?.namespaces === true;
    const members = new Set<string>();
    const discovered = new Map<string, string>();
    const files = source.paths.filter((path) => path.startsWith(`${root}/`) && /\.pyi?$/.test(path) &&
      productionPath(path.slice(root.length + 1)));
    for (const file of files) {
      for (const directory of where) {
        const base = posix.normalize(posix.join(root, directory));
        if (!file.startsWith(`${base}/`)) continue;
        const relative = file.slice(base.length + 1);
        const folder = posix.dirname(relative);
        if (folder === ".") {
          if (modules && strings(modules, projectPath).includes(posix.basename(file).replace(/\.pyi?$/, ""))) members.add(file);
          continue;
        }
        const packageName = folder.replaceAll("/", ".");
        const selected = explicit ? explicit.includes(packageName) :
          include.some((pattern) => globMatch(packageName, pattern, ".")) &&
          !exclude.some((pattern) => globMatch(packageName, pattern, "."));
        if (!selected) continue;
        if (!explicit && !namespaces) {
          const parts = folder.split("/");
          if (parts.some((_, index) => !paths.has(`${base}/${parts.slice(0, index + 1).join("/")}/__init__.py`))) continue;
        }
        members.add(file);
        discovered.set(packageName, `${base}/${folder}`);
      }
    }
    for (const [packageName, directory] of discovered) {
      const patterns = [
        ...(packageData["*"] === undefined ? [] : strings(packageData["*"], projectPath)),
        ...(packageData[packageName] === undefined ? [] : strings(packageData[packageName], projectPath)),
      ];
      const exclusions = [
        ...(excludedData["*"] === undefined ? [] : strings(excludedData["*"], projectPath)),
        ...(excludedData[packageName] === undefined ? [] : strings(excludedData[packageName], projectPath)),
      ];
      for (const file of files.filter((path) => path.startsWith(`${directory}/`))) {
        const relative = file.slice(directory.length + 1);
        if (patterns.some((pattern) => globMatch(relative, pattern)) &&
            !exclusions.some((pattern) => globMatch(relative, pattern))) members.add(file);
      }
    }
    packages.push({
      library: name, service, category: name.startsWith("azure-mgmt-") ? "management" : "data-plane",
      projectPath, sourcePaths: [...members].sort(),
    });
  }
  const generatedDirectories = new Set<string>();
  const generatedRoots = new Set<string>();
  for (const member of packages) {
    for (const path of member.sourcePaths) {
      if (!generatorHeader(await source.readText(path), "python")) continue;
      generatedDirectories.add(posix.dirname(path));
      const match = /^(.*\/_generated)(?:\/|$)/.exec(path);
      if (match) generatedRoots.add(match[1]);
    }
    const root = posix.dirname(member.projectPath);
    for (const path of source.paths.filter((path) => path.startsWith(`${root}/`) && /\/(?:SWAGGER|README)\.md$/.test(path))) {
      if (!path.includes("/swagger/")) continue;
      const text = await source.readText(path);
      for (const match of text.matchAll(/output-folder:\s*["']?([^\r\n"']*_generated)\b/g)) {
        const suffix = match[1].replaceAll("\\", "/");
        for (const file of member.sourcePaths) {
          const generated = /^(.*\/_generated)(?:\/|$)/.exec(file)?.[1];
          if (generated && (suffix.endsWith(generated) || generated.endsWith(suffix.replace(/^\.\//, "")))) generatedRoots.add(generated);
        }
      }
    }
  }
  return collectSource(source, context, packages, excluded, (path, text) => {
    if (["_patch.py", "_patch.pyi"].includes(posix.basename(path))) {
      return { provenance: "custom", evidence: "customization-patch" };
    }
    if (generatorHeader(text, "python")) return { provenance: "generated", evidence: "auto-generated-header" };
    if ([...generatedRoots].some((root) => path.startsWith(`${root}/`)) ||
        ["_serialization.py", "_model_base.py"].includes(posix.basename(path)) && generatedDirectories.has(posix.dirname(path))) {
      return { provenance: "generated", evidence: "generator-output" };
    }
    return { provenance: "custom", evidence: "no-generated-signal" };
  });
}
