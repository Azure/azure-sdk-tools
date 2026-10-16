import { collectionTime, snapshotKey, type Observation, type ObservationTime, type Snapshot } from "./data.ts";

export type Reconstruction = {
  repository: "Azure/azure-sdk-for-net";
  snapshotId: string;
  snapshotSha256: string;
  asOf: string;
};
export type PreviewMetadata = { schemaVersion: "1.0"; observations: Reconstruction[] };
export type PreviewMetadataIndex = ReadonlyMap<string, Reconstruction>;

function assert(condition: boolean, message: string): asserts condition {
  if (!condition) throw new Error(`Invalid preview metadata: ${message}.`);
}
function record(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
function exactKeys(value: Record<string, unknown>, keys: readonly string[]): boolean {
  return Object.keys(value).length === keys.length && keys.every((key) => Object.hasOwn(value, key));
}
function reconstructionKey(entry: Reconstruction): string {
  return JSON.stringify([entry.repository, entry.snapshotId]);
}
function utcTime(value: string): number {
  const match = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(?:Z|\+00:00)$/.exec(value);
  const time = Date.parse(value);
  assert(match !== null && Number.isFinite(time) &&
    new Date(time).toISOString() === `${match[1]}.${(match[2] || "").padEnd(3, "0").slice(0, 3)}Z`,
  "asOf must be a valid UTC timestamp");
  return time;
}

export function acceptPreviewMetadata(value: unknown, snapshots: readonly Snapshot[]): PreviewMetadata {
  assert(record(value) && exactKeys(value, ["schemaVersion", "observations"]), "unexpected document shape");
  assert(value.schemaVersion === "1.0" && Array.isArray(value.observations), "unsupported version or observations");
  const known = new Map(snapshots.map((snapshot) => [snapshotKey(snapshot), snapshot]));
  const seen = new Set<string>();
  const observations = value.observations.map((entry: unknown): Reconstruction => {
    assert(record(entry) && exactKeys(entry, ["repository", "snapshotId", "snapshotSha256", "asOf"]),
      "unexpected observation shape");
    assert(entry.repository === "Azure/azure-sdk-for-net", "reconstruction supports only .NET preview observations");
    assert(typeof entry.snapshotId === "string" && typeof entry.snapshotSha256 === "string" &&
      /^[a-f0-9]{64}$/.test(entry.snapshotSha256) && typeof entry.asOf === "string", "invalid identity, hash or asOf");
    const result: Reconstruction = {
      repository: entry.repository, snapshotId: entry.snapshotId, snapshotSha256: entry.snapshotSha256, asOf: entry.asOf,
    };
    const key = reconstructionKey(result);
    assert(!seen.has(key), "duplicate reconstruction identity");
    seen.add(key);
    const snapshot = known.get(key);
    assert(snapshot !== undefined, "reconstruction does not match an embedded repository and snapshot ID");
    assert(!snapshot.repository.isDirty, "reconstruction requires committed source");
    assert(utcTime(result.asOf) < collectionTime(snapshot), "source asOf must precede the actual collection time");
    return result;
  });
  return { schemaVersion: "1.0", observations };
}

export function verifyPreviewHashes(
  metadata: PreviewMetadata, hashes: ReadonlyMap<string, ReadonlySet<string>>,
): void {
  for (const entry of metadata.observations) {
    const source = hashes.get(reconstructionKey(entry));
    assert(source?.size === 1 && source.has(entry.snapshotSha256), "snapshotSha256 differs from the original input bytes");
  }
}

export function indexPreviewMetadata(metadata: PreviewMetadata): PreviewMetadataIndex {
  return new Map(metadata.observations.map((entry) => [reconstructionKey(entry), entry]));
}

export function previewTime(metadata: PreviewMetadataIndex): ObservationTime {
  return (observation: Observation) => {
    const entry = metadata.get(snapshotKey(observation));
    return entry ? Date.parse(entry.asOf) : collectionTime(observation);
  };
}
