import validate from "./generated/validate-snapshot.mjs";
import type { CustomCodeMetrics, Metrics, LibraryMetrics } from "./generated/snapshot.js";

export type Snapshot = CustomCodeMetrics;
export type Library = LibraryMetrics;
export type MeasuredLibrary = Pick<Library, "library" | "service" | "category" | "metrics">;
export type Observation = Pick<Snapshot, "schemaVersion" | "snapshotId" | "collectedAt" | "repository"> & {
  libraries: MeasuredLibrary[];
};
export const REPOSITORIES = [
  { name: "Azure/azure-sdk-for-net", language: ".NET", implemented: true },
  { name: "Azure/azure-sdk-for-java", language: "Java", implemented: false },
  { name: "Azure/azure-sdk-for-js", language: "JavaScript/TypeScript", implemented: false },
  { name: "Azure/azure-sdk-for-python", language: "Python", implemented: false },
  { name: "Azure/azure-sdk-for-go", language: "Go", implemented: false },
  { name: "Azure/azure-sdk-for-rust", language: "Rust", implemented: false },
  { name: "Azure/azure-sdk-for-cpp", language: "C++", implemented: false },
] as const;
export type RepositoryName = typeof REPOSITORIES[number]["name"];

export function forRepository<T extends Observation>(observations: readonly T[], repository: RepositoryName): T[] {
  return observations.filter((observation) => observation.repository.name === repository);
}

export type Metric = Metrics;
export type Category = Library["category"];
export const CATEGORIES: readonly Category[] = ["management", "data-plane", "provisioning"];
export const COUNTS = [
  "libraryCount", "customFiles", "generatedFiles", "totalFiles",
  "customLines", "generatedLines", "totalLines",
] as const;
export type Filters = { category: string; service: string; search: string };
export type SortKey = "library" | "service" | "category" | "customRatio" | "customLines" | "totalLines";
export type HistoryPoint = {
  snapshot: Observation;
  metrics: Metric;
  added: number;
  removed: number;
};
export type History = {
  points: HistoryPoint[];
  fixedIds: Set<string> | null;
  distinctRevisions: number;
  excludedDirty: number;
  excludedIncompatible: number;
};

function assert(condition: boolean, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

export function assertObservationIdentity(observation: Observation): void {
  assert(Number.isFinite(Date.parse(observation.collectedAt)), "Observation timestamp must be representable by the browser.");
  assert(observation.snapshotId.endsWith(`-${observation.repository.commit}`), "Snapshot identity and revision disagree.");
  const fraction = observation.collectedAt.match(/\.(\d+)(?:[Zz]|[+-]\d{2}:\d{2})$/)?.[1] ?? "";
  const stamp = new Date(observation.collectedAt).toISOString().slice(0, 19).replace(/[-:]/g, "");
  assert(!/[1-9]/.test(fraction.slice(7)) &&
    observation.snapshotId === `${stamp}${fraction.padEnd(7, "0").slice(0, 7)}Z-${observation.repository.commit}`,
  "Snapshot identity and timestamp disagree.");
}

export function aggregate(libraries: readonly MeasuredLibrary[]): Metric {
  const result: Metric = {
    libraryCount: 0, customFiles: 0, generatedFiles: 0, totalFiles: 0,
    customLines: 0, generatedLines: 0, totalLines: 0, customRatio: null,
  };
  for (const library of libraries) {
    for (const key of COUNTS) {
      result[key] += library.metrics[key];
      assert(Number.isSafeInteger(result[key]), `Aggregated ${key} exceeds JavaScript's exact integer range.`);
    }
  }
  result.customRatio = result.totalLines === 0 ? null : result.customLines / result.totalLines;
  return result;
}

export function assertMetric(metric: Metric, context: string): void {
  for (const key of COUNTS) {
    assert(Number.isSafeInteger(metric[key]) && metric[key] >= 0, `${context}: ${key} must be a nonnegative safe integer.`);
  }
  assert(metric.totalLines === metric.customLines + metric.generatedLines, `${context}: line provenance does not add up.`);
  assert(metric.totalFiles === metric.customFiles + metric.generatedFiles, `${context}: file provenance does not add up.`);
  if (metric.totalLines === 0) {
    assert(metric.customRatio === null, `${context}: an empty denominator requires a null ratio.`);
  } else {
    assert(metric.customRatio !== null && Math.abs(metric.customRatio - metric.customLines / metric.totalLines) <= 1e-12,
      `${context}: custom ratio does not match its numerator and denominator.`);
  }
}

function assertRollup(actual: Metric, expected: Metric, context: string): void {
  assertMetric(actual, context);
  for (const key of COUNTS) assert(actual[key] === expected[key], `${context}: ${key} rollup differs from library totals.`);
}

function assertCategories(rows: Snapshot["categories"], libraries: readonly Library[], context: string): void {
  assert(new Set(rows.map((row) => row.category)).size === CATEGORIES.length, `${context}: categories must be distinct.`);
  for (const category of CATEGORIES) {
    const row = rows.find((candidate) => candidate.category === category);
    assert(row !== undefined, `${context}: missing ${category} category.`);
    assertRollup(row.metrics, aggregate(libraries.filter((library) => library.category === category)), `${context}/${category}`);
  }
}

export function acceptSnapshot(value: unknown): Snapshot {
  if (!validate(value)) {
    const error = validate.errors?.[0];
    throw new Error(`Invalid snapshot ${error?.instancePath || "/"}: ${error?.message || "schema validation failed"}.`);
  }
  assertObservationIdentity(value);
  const ids = new Set<string>();
  for (const library of value.libraries) {
    assert(!ids.has(library.library), `Duplicate library ID: ${library.library}.`);
    ids.add(library.library);
    assertMetric(library.metrics, library.library);
    assert(library.metrics.libraryCount === 1, `${library.library}: libraryCount must be one.`);
    if (library.files) {
      assert(library.files.length === library.metrics.totalFiles, `${library.library}: file evidence count differs.`);
      const paths = new Set<string>();
      let lines = 0;
      const provenance = {
        custom: { files: 0, lines: 0 }, generated: { files: 0, lines: 0 },
      };
      for (const file of library.files) {
        const coreShared = /^(sdk\/core\/[^/]+\/src\/)Shared\//.exec(file.path);
        assert(!coreShared || library.projectPath.startsWith(coreShared[1]),
          `${library.library}: linked core Shared helper must be excluded from counts and evidence (${file.path}).`);
        assert(!paths.has(file.path), `${library.library}: duplicate file evidence ${file.path}.`);
        paths.add(file.path);
        assert(Number.isSafeInteger(file.lines), `${file.path}: lines exceed JavaScript's exact integer range.`);
        lines += file.lines;
        provenance[file.provenance].files++;
        provenance[file.provenance].lines += file.lines;
      }
      assert(lines === library.metrics.totalLines, `${library.library}: file evidence line total differs.`);
      assert(provenance.custom.files === library.metrics.customFiles && provenance.custom.lines === library.metrics.customLines &&
        provenance.generated.files === library.metrics.generatedFiles && provenance.generated.lines === library.metrics.generatedLines,
      `${library.library}: file evidence provenance differs from reported counts.`);
    }
  }
  assertRollup(value.summary, aggregate(value.libraries), "Repository");
  assertCategories(value.categories, value.libraries, "Repository");
  const services = new Set<string>();
  for (const service of value.services) {
    assert(!services.has(service.service), `Duplicate service: ${service.service}.`);
    services.add(service.service);
    const members = value.libraries.filter((library) => library.service === service.service);
    assert(members.length > 0, `Service ${service.service} has no libraries.`);
    assertRollup(service.metrics, aggregate(members), service.service);
    assertCategories(service.categories, members, service.service);
  }
  assert(value.libraries.every((library) => services.has(library.service)), "A library's service is missing from service rollups.");
  return value;
}

export function parseSnapshot(text: string): Snapshot {
  return acceptSnapshot(JSON.parse(text));
}

export function withoutFileEvidence(snapshot: Snapshot): Snapshot {
  return {
    ...snapshot,
    libraries: snapshot.libraries.map(({ files, ...library }) => library),
  };
}

export function measurementKey(snapshot: Observation): string {
  return JSON.stringify([snapshot.schemaVersion, snapshot.repository.name]);
}

function observationContent(snapshot: Snapshot): string {
  return JSON.stringify([
    [snapshot.repository.name, snapshot.repository.commit, snapshot.repository.isDirty],
    measurementKey(snapshot),
    [...snapshot.libraries].sort((a, b) => a.library.localeCompare(b.library)).map((library) => [
      library.library, library.service, library.category, library.projectPath,
      [...library.targetFrameworks].sort(), COUNTS.map((key) => library.metrics[key]),
    ]),
    [...snapshot.excludedLibraries].sort((a, b) => a.projectPath.localeCompare(b.projectPath)).map((library) => [library.projectPath, library.reason]),
  ]);
}

export function mergeSnapshots(existing: readonly Snapshot[], incoming: readonly Snapshot[]): Snapshot[] {
  const unique = new Map<string, Snapshot>();
  for (const snapshot of [...existing, ...incoming]) {
    const prior = unique.get(snapshot.snapshotId);
    if (prior) {
      assert(Date.parse(prior.collectedAt) === Date.parse(snapshot.collectedAt) && observationContent(prior) === observationContent(snapshot),
        `Conflicting content for snapshot ${snapshot.snapshotId}.`);
      if (snapshot.libraries.some((library) => library.files)) unique.set(snapshot.snapshotId, snapshot);
    } else {
      unique.set(snapshot.snapshotId, snapshot);
    }
  }
  const retries = new Map<string, Snapshot>();
  for (const snapshot of [...unique.values()].sort(compareSnapshots)) {
    const day = new Date(snapshot.collectedAt).toISOString().slice(0, 10);
    const key = snapshot.repository.isDirty ? snapshot.snapshotId :
      `${measurementKey(snapshot)}|${snapshot.repository.commit}|${day}`;
    const prior = retries.get(key);
    if (prior) {
      assert(observationContent(prior) === observationContent(snapshot),
        `Conflicting measurements for revision ${snapshot.repository.commit.slice(0, 12)} on ${day}; these are not identical retries.`);
    }
    retries.set(key, snapshot);
  }
  return [...retries.values()].sort(compareSnapshots);
}

function compareSnapshots(a: Observation, b: Observation): number {
  return Date.parse(a.collectedAt) - Date.parse(b.collectedAt) || a.snapshotId.localeCompare(b.snapshotId);
}

export function filterLibraries<T extends MeasuredLibrary>(libraries: readonly T[], filters: Filters): T[] {
  const query = filters.search.trim().toLowerCase();
  return libraries.filter((library) =>
    (!filters.category || library.category === filters.category) &&
    (!filters.service || library.service === filters.service) &&
    (!query || library.library.toLowerCase().includes(query)));
}

export function sortLibraries(libraries: readonly Library[], key: SortKey, descending: boolean): Library[] {
  return [...libraries].sort((a, b) => {
    if (key === "library" || key === "service" || key === "category") {
      const order = a[key].localeCompare(b[key]);
      return (descending ? -order : order) || a.library.localeCompare(b.library);
    }
    const left = a.metrics[key];
    const right = b.metrics[key];
    if (left === null || right === null) {
      if (left === right) return a.library.localeCompare(b.library);
      return left === null ? 1 : -1;
    }
    const order = left - right;
    return (descending ? -order : order) || a.library.localeCompare(b.library);
  });
}

export function breakdown(libraries: readonly Library[], group: "category" | "service"): { name: string; metrics: Metric }[] {
  const names = group === "category" ? CATEGORIES : [...new Set(libraries.map((library) => library.service))];
  return names.map((name) => ({
    name, metrics: aggregate(libraries.filter((library) => library[group] === name)),
  })).sort((a, b) => b.metrics.totalLines - a.metrics.totalLines || a.name.localeCompare(b.name));
}

export function history(
  snapshots: readonly Observation[], selected: Observation, filters: Filters,
  options: { fixed: boolean; includeDirty: boolean; libraryId: string },
): History {
  const compatible = snapshots.filter((snapshot) => measurementKey(snapshot) === measurementKey(selected));
  const eligible = compatible.filter((snapshot) => options.includeDirty || !snapshot.repository.isDirty).sort(compareSnapshots);
  const members = eligible.map((snapshot) => filterLibraries(snapshot.libraries, filters)
    .filter((library) => !options.libraryId || library.library === options.libraryId));
  let fixedIds: Set<string> | null = null;
  if (options.fixed) {
    fixedIds = new Set(members[0]?.map((library) => library.library) || []);
    for (const libraries of members.slice(1)) {
      const ids = new Set(libraries.map((library) => library.library));
      for (const id of fixedIds) if (!ids.has(id)) fixedIds.delete(id);
    }
  }
  let priorIds: Set<string> | null = null;
  const points = eligible.map((snapshot, index) => {
    const libraries = members[index].filter((library) => !fixedIds || fixedIds.has(library.library));
    const ids = new Set(libraries.map((library) => library.library));
    const point = {
      snapshot, metrics: aggregate(libraries),
      added: priorIds ? [...ids].filter((id) => !priorIds?.has(id)).length : 0,
      removed: priorIds ? [...priorIds].filter((id) => !ids.has(id)).length : 0,
    };
    priorIds = ids;
    return point;
  });
  return {
    points, fixedIds,
    distinctRevisions: new Set(eligible.map((snapshot) => snapshot.repository.commit)).size,
    excludedDirty: compatible.length - eligible.length,
    excludedIncompatible: snapshots.length - compatible.length,
  };
}

export function trendSeries(points: readonly HistoryPoint[]): { x: number; y: number | null }[] {
  const result: { x: number; y: number | null }[] = [];
  let priorDay: number | null = null;
  for (const point of points) {
    const time = Date.parse(point.snapshot.collectedAt);
    const day = Math.floor(time / 86400000);
    if (priorDay !== null && day - priorDay > 1) {
      result.push({ x: (priorDay + 1) * 86400000, y: null });
    }
    result.push({ x: time, y: point.metrics.customRatio === null ? null : point.metrics.customRatio * 100 });
    priorDay = day;
  }
  return result;
}

export async function loadIndex(url: string, fetcher: typeof fetch = fetch): Promise<Snapshot[]> {
  const indexUrl = new URL(url);
  assert(["http:", "https:"].includes(indexUrl.protocol), "Snapshot indexes require an HTTP(S) URL.");
  const response = await fetcher(indexUrl, { credentials: "omit" });
  assert(response.ok, `Snapshot index request failed: HTTP ${response.status}.`);
  const value: unknown = await response.json();
  assert(typeof value === "object" && value !== null && "snapshots" in value && Array.isArray(value.snapshots),
    "Snapshot index must be an object with a snapshots array of URLs.");
  const paths: unknown[] = value.snapshots;
  assert(paths.length > 0 && paths.every((path) => typeof path === "string"), "Snapshot index must contain at least one URL string.");
  const loaded: Snapshot[] = [];
  for (const path of paths) {
    assert(typeof path === "string", "Snapshot URL must be a string.");
    const source = new URL(path, indexUrl);
    assert(["http:", "https:"].includes(source.protocol), `Unsupported snapshot URL: ${source.protocol}`);
    const snapshot = await fetcher(source, { credentials: "omit" });
    assert(snapshot.ok, `Snapshot request failed: HTTP ${snapshot.status} (${source}).`);
    loaded.push(parseSnapshot(await snapshot.text()));
  }
  return mergeSnapshots([], loaded);
}
