import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { acceptPreviewMetadata, indexPreviewMetadata, previewTime, verifyPreviewHashes, type PreviewMetadata } from "../dashboard/preview.ts";
import { acceptSnapshot, collectionTime, forRepository, history, mergeSnapshots, snapshotKey, trendSeries } from "../dashboard/data.ts";
import { compactObservation, inRange, mergeObservations } from "../dashboard/report.ts";
import { filters, library, snapshot } from "../dashboard/tests/fixtures.ts";

const dates = ["2026-09-06", "2026-09-13", "2026-09-20", "2026-09-27", "2026-10-04", "2026-10-11"];
const weekly = dates.map((_, index) => snapshot([library("Azure.One", index < 3 ? 10 : 20, 30)],
  `2026-10-14T12:0${index}:00Z`, ["2", "3", "3", "4", "5", "6"][index]));
const original = snapshot(undefined, "2026-10-07T18:29:15.6451752Z");
const hash = (source: string) => createHash("sha256").update(source).digest("hex");
const metadata: PreviewMetadata = {
  schemaVersion: "1.0",
  observations: weekly.map((value, index) => ({
    repository: "Azure/azure-sdk-for-net", snapshotId: value.snapshotId,
    snapshotSha256: hash(JSON.stringify(value)), asOf: `${dates[index]}T23:59:59.9999999Z`,
  })),
};

test("preview metadata binds exact original identities without rewriting snapshots or receipt data", () => {
  const inputs = [...weekly, original];
  const before = structuredClone({ inputs, metadata });
  const accepted = acceptPreviewMetadata(metadata, inputs);
  const hashes = new Map(weekly.map((value) => [snapshotKey(value), new Set([hash(JSON.stringify(value))])]));
  verifyPreviewHashes(accepted, hashes);
  const time = previewTime(indexPreviewMetadata(accepted));
  assert.equal(new Date(time(weekly[0])).toISOString(), "2026-09-06T23:59:59.999Z");
  assert.equal(time(original), collectionTime(original));
  for (const value of inputs) assert.doesNotThrow(() => acceptSnapshot(value));
  assert.deepEqual({ inputs, metadata }, before);
  assert.deepEqual(acceptPreviewMetadata({ schemaVersion: "1.0", observations: [] }, inputs).observations, []);
});

const entry = metadata.observations[0];
const malformed: [string, unknown][] = [
  ["null", null], ["array", []], ["extra document field", { ...metadata, asOf: entry.asOf }],
  ["missing version", { observations: [] }], ["wrong version", { ...metadata, schemaVersion: "2.0" }],
  ["non-array observations", { ...metadata, observations: {} }],
  ["null entry", { ...metadata, observations: [null] }],
  ["extra entry field", { ...metadata, observations: [{ ...entry, collectedAt: entry.asOf }] }],
  ["missing entry field", { ...metadata, observations: [{ repository: entry.repository, snapshotId: entry.snapshotId, asOf: entry.asOf }] }],
  ["foreign repository", { ...metadata, observations: [{ ...entry, repository: "Azure/azure-sdk-for-java" }] }],
  ["unknown identity", { ...metadata, observations: [{ ...entry, snapshotId: original.snapshotId + "x" }] }],
  ["non-string identity", { ...metadata, observations: [{ ...entry, snapshotId: 1 }] }],
  ["non-string hash", { ...metadata, observations: [{ ...entry, snapshotSha256: 1 }] }],
  ["short hash", { ...metadata, observations: [{ ...entry, snapshotSha256: "a" }] }],
  ["uppercase hash", { ...metadata, observations: [{ ...entry, snapshotSha256: "A".repeat(64) }] }],
  ["non-string date", { ...metadata, observations: [{ ...entry, asOf: 1 }] }],
  ["invalid calendar date", { ...metadata, observations: [{ ...entry, asOf: "2026-02-30T00:00:00Z" }] }],
  ["invalid clock time", { ...metadata, observations: [{ ...entry, asOf: "2026-09-06T24:00:00Z" }] }],
  ["date without time", { ...metadata, observations: [{ ...entry, asOf: dates[0] }] }],
  ["non-UTC offset", { ...metadata, observations: [{ ...entry, asOf: "2026-09-06T00:00:00+01:00" }] }],
  ["excess precision", { ...metadata, observations: [{ ...entry, asOf: "2026-09-06T00:00:00.12345678Z" }] }],
  ["future source date", { ...metadata, observations: [{ ...entry, asOf: "2026-10-15T00:00:00Z" }] }],
  ["source equals collection", { ...metadata, observations: [{ ...entry, asOf: weekly[0].collectedAt }] }],
  ["duplicate identity", { ...metadata, observations: [entry, entry] }],
];
for (const [name, value] of malformed) {
  test(`preview metadata rejects ${name}`, () => {
    assert.throws(() => acceptPreviewMetadata(value, weekly), /Invalid preview metadata/);
  });
}
test("preview metadata rejects uncommitted and repository-mismatched input", () => {
  const dirty = structuredClone(weekly[0]);
  dirty.repository.isDirty = true;
  assert.throws(() => acceptPreviewMetadata({ schemaVersion: "1.0", observations: [entry] }, [dirty]), /committed source/);
  assert.throws(() => acceptPreviewMetadata(metadata, [original]), /does not match/);
});
test("preview source dates accept .NET zero-offset UTC timestamps with exact seven-digit precision", () => {
  const value = { schemaVersion: "1.0", observations: [{ ...entry, asOf: "2026-09-06T23:59:59.9999999+00:00" }] };
  const accepted = acceptPreviewMetadata(value, weekly);
  assert.equal(accepted.observations[0].asOf, value.observations[0].asOf);
  assert.equal(new Date(previewTime(indexPreviewMetadata(accepted))(weekly[0])).toISOString(), "2026-09-06T23:59:59.999Z");
});
test("reconstruction source hashes cannot bind missing, compacted, edited or conflicting input bytes", () => {
  const single = { schemaVersion: "1.0", observations: [entry] } satisfies PreviewMetadata;
  for (const values of [[], ["0".repeat(64)], [entry.snapshotSha256, "0".repeat(64)], [hash(JSON.stringify(compactObservation(weekly[0])))]]) {
    assert.throws(() => verifyPreviewHashes(single, new Map([[snapshotKey(weekly[0]), new Set(values)]])), /original input bytes/);
  }
  assert.throws(() => verifyPreviewHashes(single, new Map()), /original input bytes/);
});
test("source-date ordering selects October 11 and preserves same-commit observations from different weeks", () => {
  const time = previewTime(indexPreviewMetadata(acceptPreviewMetadata(metadata, weekly)));
  const before = structuredClone(weekly);
  const seeds = mergeSnapshots([original], [...weekly].reverse(), time);
  const expected = [...dates, "2026-10-07"].sort();
  assert.equal(seeds.length, 7);
  assert.deepEqual(seeds.map((value) => new Date(time(value)).toISOString().slice(0, 10)), expected);
  assert.equal(seeds.at(-1)?.snapshotId, weekly[5].snapshotId);
  assert.equal(mergeSnapshots([], [weekly[1], weekly[2]]).length, 1, "Default collection-date retry behavior changed.");
  const compact = mergeObservations(seeds, time);
  assert.equal(compact.length, 7);
  assert.deepEqual(compact.map((value) => value.snapshotId), seeds.map((value) => value.snapshotId));
  assert.deepEqual(weekly, before);
  assert.equal(forRepository(seeds, "Azure/azure-sdk-for-java").length, 0);
});
test("preview history ranges anchor to historical UTC days, not the current reconstruction date", () => {
  const time = previewTime(indexPreviewMetadata(metadata));
  const observations = mergeObservations([...weekly, original], time);
  const latest = weekly[5];
  const days = (values: typeof observations) => values.map((value) => new Date(time(value)).toISOString().slice(0, 10));
  assert.deepEqual(days(inRange(observations, latest, 30, time)), ["2026-09-13", "2026-09-20", "2026-09-27", "2026-10-04", "2026-10-07", "2026-10-11"]);
  for (const range of [90, 365, null]) assert.equal(inRange(observations, latest, range, time).length, 7);
  assert.deepEqual(days(inRange(observations, weekly[2], 30, time)), dates.slice(0, 3));
  assert.equal(inRange(observations, latest, 1, time)[0].snapshotId, latest.snapshotId);
});
test("chronological preview cohorts remain weighted with real missing-day gaps and null denominators", () => {
  const first = snapshot([library("Azure.One", 10, 90), library("Azure.Old", 90, 10)], weekly[0].collectedAt, "2");
  const later = snapshot([library("Azure.One", 20, 80), library("Azure.New", 1, 9)], weekly[5].collectedAt, "6");
  const mapped = { ...metadata, observations: [metadata.observations[0], metadata.observations[5]] };
  const time = previewTime(indexPreviewMetadata(acceptPreviewMetadata(mapped, [first, later])));
  const fixed = history([later, first], later, filters, { fixed: true, includeDirty: false, libraryId: "" }, time);
  assert.deepEqual(fixed.points.map((point) => point.metrics.customRatio), [0.1, 0.2]);
  assert.deepEqual([...fixed.fixedIds || []], ["Azure.One"]);
  const observed = history([later, first], later, filters, { fixed: false, includeDirty: false, libraryId: "" }, time);
  assert.equal(observed.points[1].metrics.customRatio, 21 / 110);
  assert.equal(observed.points[1].added, 1);
  assert.equal(observed.points[1].removed, 1);
  const missing = history([first, later], later, filters, { fixed: false, includeDirty: false, libraryId: "Azure.Old" }, time);
  assert.equal(missing.points[1].metrics.customRatio, null);
  const series = trendSeries(fixed.points, time);
  assert.deepEqual(series.map((point) => point.y), [10, null, 20]);
  assert.equal(new Date(series[1].x).toISOString(), "2026-09-07T00:00:00.000Z");
  assert.equal(new Date(series[2].x).toISOString(), "2026-10-11T23:59:59.999Z");
});
