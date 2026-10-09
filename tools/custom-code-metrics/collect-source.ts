import { CATEGORIES, aggregate, assertMetric, type Category, type Metric } from "./dashboard/data.ts";

export interface SourceProvider {
  readonly paths: readonly string[];
  readText(path: string): Promise<string>;
}

export type NativeRepositoryName = "Azure/azure-sdk-for-java" | "Azure/azure-sdk-for-python";
export type ObservationContext = {
  repository: { name: NativeRepositoryName; commit: string; isDirty: boolean };
  collectedAt: string;
};
export type SourceClassification = {
  provenance: "custom" | "generated";
  evidence: string;
};
export type NativeLibraryInput = {
  library: string;
  service: string;
  category: Category;
  projectPath: string;
  sourcePaths: readonly string[];
};
export type NativeFileEvidence = SourceClassification & { path: string; lines: number };
export type NativeLibrary = Omit<NativeLibraryInput, "sourcePaths"> & {
  metrics: Metric;
  files: NativeFileEvidence[];
};
export type ExcludedNativeLibrary = { projectPath: string; reason: string };
export type NativeSnapshot = {
  schemaVersion: "1.0";
  snapshotId: string;
  collectedAt: string;
  repository: ObservationContext["repository"];
  summary: Metric;
  categories: { category: Category; metrics: Metric }[];
  services: { service: string; metrics: Metric; categories: { category: Category; metrics: Metric }[] }[];
  libraries: NativeLibrary[];
  excludedLibraries: ExcludedNativeLibrary[];
};
export type ClassifySource = (path: string, text: string, library: NativeLibraryInput) => SourceClassification;

export function physicalLines(text: string): number {
  if (text.length === 0) return 0;
  const endings = text.match(/\r\n|\r|\n/g)?.length ?? 0;
  return endings + (/[\r\n]$/.test(text) ? 0 : 1);
}

export function assertSourcePath(path: string): void {
  if (!path || path.startsWith("/") || path.includes("\\") || /[\0\r\n]/.test(path) ||
      path.split("/").some((part) => !part || part === "." || part === "..")) {
    throw new Error(`Invalid repository-relative source path: ${path}.`);
  }
}

export async function collectSource(
  source: SourceProvider, context: ObservationContext, packages: readonly NativeLibraryInput[],
  excludedLibraries: readonly ExcludedNativeLibrary[], classify: ClassifySource,
): Promise<NativeSnapshot> {
  if (!["Azure/azure-sdk-for-java", "Azure/azure-sdk-for-python"].includes(context.repository.name) ||
      !/^[0-9a-f]{40,64}$/.test(context.repository.commit) || typeof context.repository.isDirty !== "boolean") {
    throw new Error("Native collection requires an explicit supported repository, commit and source state.");
  }
  const fraction = context.collectedAt.match(/\.(\d+)(?:Z|[+-]\d{2}:\d{2})$/)?.[1] ?? "";
  if (!Number.isFinite(Date.parse(context.collectedAt)) || /[1-9]/.test(fraction.slice(7))) {
    throw new Error("Native collection requires an exact representable observation time.");
  }
  if (!packages.length) throw new Error("No shipping packages were selected; no observation was produced.");
  const paths = new Set<string>();
  for (const path of source.paths) {
    assertSourcePath(path);
    if (paths.has(path)) throw new Error(`Duplicate source inventory path: ${path}.`);
    paths.add(path);
  }
  const ids = new Set<string>();
  const libraries: NativeLibrary[] = [];
  for (const member of packages) {
    assertSourcePath(member.projectPath);
    if (!member.library || !member.service || !CATEGORIES.includes(member.category) || ids.has(member.library)) {
      throw new Error(`Invalid or duplicate shipping package metadata: ${member.library}.`);
    }
    if (!paths.has(member.projectPath)) throw new Error(`Package metadata is absent from the source inventory: ${member.projectPath}.`);
    ids.add(member.library);
    const metrics: Metric = { ...aggregate([]), libraryCount: 1 };
    const files: NativeFileEvidence[] = [];
    const counted = new Set<string>();
    for (const path of member.sourcePaths) {
      assertSourcePath(path);
      if (!paths.has(path)) throw new Error(`Counted source is absent from the source inventory: ${path}.`);
      if (counted.has(path)) throw new Error(`Duplicate counted file in ${member.library}: ${path}.`);
      counted.add(path);
      const text = await source.readText(path);
      const classification = classify(path, text, member);
      if (!["custom", "generated"].includes(classification.provenance) || !classification.evidence) {
        throw new Error(`Missing source classification for ${path}.`);
      }
      const lines = physicalLines(text);
      if (classification.provenance === "custom") {
        metrics.customFiles++;
        metrics.customLines += lines;
      } else {
        metrics.generatedFiles++;
        metrics.generatedLines += lines;
      }
      metrics.totalFiles++;
      metrics.totalLines += lines;
      files.push({ path, lines, ...classification });
    }
    metrics.customRatio = metrics.totalLines === 0 ? null : metrics.customLines / metrics.totalLines;
    assertMetric(metrics, member.library);
    const { sourcePaths, ...metadata } = member;
    libraries.push({ ...metadata, metrics, files });
  }
  const categories = (members: readonly NativeLibrary[]) => CATEGORIES.map((category) => ({
    category, metrics: aggregate(members.filter((member) => member.category === category)),
  }));
  const stamp = new Date(context.collectedAt).toISOString().slice(0, 19).replace(/[-:]/g, "") +
    fraction.padEnd(7, "0").slice(0, 7);
  return {
    schemaVersion: "1.0", snapshotId: `${stamp}Z-${context.repository.commit}`,
    collectedAt: context.collectedAt, repository: { ...context.repository },
    summary: aggregate(libraries), categories: categories(libraries),
    services: [...new Set(libraries.map((member) => member.service))].sort().map((service) => {
      const members = libraries.filter((member) => member.service === service);
      return { service, metrics: aggregate(members), categories: categories(members) };
    }),
    libraries, excludedLibraries: excludedLibraries.map((entry) => ({ ...entry })),
  };
}
