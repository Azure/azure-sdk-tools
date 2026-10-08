import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import {
  acceptHistoryMonth, acceptReportIndex, compactObservation, inRange,
  isStale, loadHistory, loadReport, mergeObservations,
} from "../generated/report.mjs";
import { history } from "../generated/data.mjs";
import { filters, legacySnapshot, library, snapshot } from "./fixtures.mjs";

export function document(observations) {
  const month = new Date(observations[0].collectedAt).toISOString().slice(0, 7);
  const value = { schemaVersion: "1.0", month, observations: observations.map(compactObservation) };
  const text = JSON.stringify(value);
  const digest = createHash("sha256").update(text).digest("hex");
  return { value, text, reference: { month, path: `history/${digest}/${month}.json` } };
}
const index = (latest, documents) => ({
  schemaVersion: "1.0", latest: `snapshots/${latest.snapshotId}.json`,
  history: documents.map((doc) => doc.reference),
});
const fetcher = (entries, requests = []) => async (url, options) => {
  requests.push(String(url));
  assert.equal(options.credentials, "omit");
  return entries.has(String(url)) ? new Response(entries.get(String(url)), { status: 200 }) : new Response("", { status: 404 });
};
const latest = snapshot();
const currentMonth = document([latest]);
const validIndex = index(latest, [currentMonth]);

test("compact history omits project, framework, evidence and redundant rollups", () => {
  const value = compactObservation(latest);
  assert.equal(value.summary, undefined);
  assert.equal(value.libraries[0].projectPath, undefined);
  assert.equal(value.libraries[0].files, undefined);
  assert.deepEqual(value.libraries[0].metrics, latest.libraries[0].metrics);
  assert.deepEqual(acceptHistoryMonth(currentMonth.value), currentMonth.value);
});
test("thin history preserves category/service filtering, weighting and fixed membership", () => {
  const first = snapshot([library("Azure.One", 2, 8), library("Azure.Two", 20, 80, "beta", "management")]);
  const second = snapshot([library("Azure.One", 4, 6), library("Azure.New", 8, 2)], "2026-10-02T12:00:00Z", "2");
  assert.deepEqual(history([first, second].map(compactObservation), second, filters,
    { fixed: true, includeDirty: false, libraryId: "" }).points.map((point) => point.metrics.customRatio), [0.2, 0.4]);
  const observed = history([first, second].map(compactObservation), second, filters,
    { fixed: false, includeDirty: false, libraryId: "" });
  assert.equal(observed.points[1].added, 1);
  assert.equal(observed.points[1].removed, 1);
  assert.equal(history([first, second].map(compactObservation), second, { ...filters, service: "beta" },
    { fixed: false, includeDirty: false, libraryId: "" }).points[1].metrics.customRatio, null);
});
test("rejects history schema/version changes, extra fields and invalid calendars", () => {
  for (const mutate of [
    (value) => { value.schemaVersion = "9"; },
    (value) => { value.extra = true; },
    (value) => { value.observations[0].collectedAt = "2026-02-31T12:00:00Z"; },
    (value) => { value.observations[0].libraries[0].projectPath = "sdk/x"; },
  ]) {
    const value = structuredClone(currentMonth.value); mutate(value);
    assert.throws(() => acceptHistoryMonth(value), /Invalid history/);
  }
});
test("initial history rejects all prior version literals and the old prototype-1 metric shape", () => {
  for (const observations of [
    ...["0.0", "2.0", "3.0"].flatMap((schemaVersion) => [
      [{ ...compactObservation(latest), schemaVersion }],
      [compactObservation(latest), { ...compactObservation(snapshot(undefined, "2026-10-02T12:00:00Z", "2")), schemaVersion }],
    ]),
    [compactObservation(legacySnapshot())],
  ]) {
    const value = { ...currentMonth.value, observations };
    const before = structuredClone(value);
    assert.throws(() => acceptHistoryMonth(value), /Invalid history/);
    assert.deepEqual(value, before);
  }
});
test("hosted latest rejects old versions and prototype-1 shapes with unchanged discovery envelopes", async () => {
  for (const prior of [...["0.0", "2.0", "3.0"].map((schemaVersion) => ({ ...latest, schemaVersion })), legacySnapshot()]) {
    const entries = new Map([
      ["https://metrics.invalid/index.json", JSON.stringify(validIndex)],
      [`https://metrics.invalid/${validIndex.latest}`, JSON.stringify(prior)],
    ]);
    await assert.rejects(loadReport("https://metrics.invalid/index.json", fetcher(entries)), /Invalid snapshot/);
  }
});
test("lazy loading rejects a content-addressed old v2 month without replacing the caller's cache", async () => {
  const old = document([{ ...snapshot(undefined, "2026-09-01T12:00:00Z", "2"), schemaVersion: "2.0" }]);
  const report = { indexUrl: new URL("https://metrics.invalid/index.json"), index: index(latest, [old, currentMonth]), latest };
  const cache = new Map([[`https://metrics.invalid/${currentMonth.reference.path}`, currentMonth.value]]);
  const before = new Map(cache);
  await assert.rejects(loadHistory(report, 90, cache, fetcher(new Map([
    [`https://metrics.invalid/${old.reference.path}`, old.text],
  ]))), /Invalid history/);
  assert.deepEqual(cache, before);
});
test("rejects wrong UTC month and mismatched identity/revision/timestamp", () => {
  for (const mutate of [
    (value) => { value.month = "2026-09"; },
    (value) => { value.observations[0].repository.commit = "a".repeat(40); },
    (value) => { value.observations[0].collectedAt = "2026-10-02T12:00:00Z"; },
  ]) {
    const value = structuredClone(currentMonth.value); mutate(value);
    assert.throws(() => acceptHistoryMonth(value), /month|disagree/);
  }
});
test("rejects duplicate libraries, non-unit library counts and unsafe/wrong metrics", () => {
  for (const mutate of [
    (value) => { value.observations[0].libraries.push(value.observations[0].libraries[0]); },
    (value) => { value.observations[0].libraries[0].metrics.libraryCount = 2; },
    (value) => { value.observations[0].libraries[0].metrics.totalLines++; },
    (value) => { value.observations[0].libraries[0].metrics.totalLines = Number.MAX_SAFE_INTEGER + 1; },
  ]) {
    const value = structuredClone(currentMonth.value); mutate(value);
    assert.throws(() => acceptHistoryMonth(value), /Duplicate|libraryCount|provenance|safe integer/);
  }
});
test("deduplicates identical same-day retries and rejects count or metadata conflicts", () => {
  const retry = snapshot(latest.libraries, "2026-10-01T14:00:00Z");
  assert.equal(mergeObservations([latest, retry]).length, 1);
  assert.equal(mergeObservations([retry, latest])[0].snapshotId, retry.snapshotId);
  for (const replacement of [library("Azure.One", 1, 44), library("Azure.One", 10, 35, "another")]) {
    assert.throws(() => mergeObservations([latest, snapshot([replacement], "2026-10-01T14:00:00Z")]), /Conflicting measurements/);
  }
});
test("retains dirty observations separately and rejects conflicting timestamps for an identity", () => {
  const dirty = snapshot(latest.libraries, "2026-10-01T14:00:00Z", "1", true);
  assert.equal(mergeObservations([latest, dirty]).length, 2);
  assert.throws(() => mergeObservations([latest, { ...latest, collectedAt: "2026-10-01T14:00:00Z" }]), /timestamps/);
});
test("rejects duplicate/mismatched history references, missing latest month and arbitrary external paths", () => {
  for (const mutate of [
    (value) => { value.history.push(value.history[0]); },
    (value) => { value.history[0].month = "2026-09"; },
    (value) => { value.history[0].path = "https://other.invalid/history.json"; },
    (value) => { value.latest = "../snapshot.json"; },
    (value) => { value.extra = true; },
  ]) {
    const value = structuredClone(validIndex); mutate(value);
    assert.throws(() => acceptReportIndex(value), /Invalid|Duplicate|disagree|missing/);
  }
});
test("ranges use inclusive UTC calendar days, not the local timezone, with a bounded size", () => {
  const end = snapshot(undefined, "2026-10-31T23:00:00Z");
  const before = snapshot(undefined, "2026-10-01T23:59:59Z", "2");
  const edge = snapshot(undefined, "2026-10-02T00:00:00Z", "3");
  assert.deepEqual(inRange([before, edge, end], end, 30).map((value) => value.snapshotId), [edge.snapshotId, end.snapshotId]);
  assert.equal(inRange([before, edge, end], end, null).length, 3);
  assert.throws(() => inRange([end], end, 0), /range/);
  assert.throws(() => inRange([end], end, 10000), /range/);
});
test("staleness begins strictly after 36 hours, not at the boundary", () => {
  const boundary = Date.parse(latest.collectedAt) + 36 * 3600000;
  assert.equal(isStale(latest, boundary - 1), false);
  assert.equal(isStale(latest, boundary), false);
  assert.equal(isStale(latest, boundary + 1), true);
});
test("startup fetches just index and full latest, with anonymous requests", async () => {
  const requests = [];
  const entries = new Map([
    ["https://metrics.invalid/dotnet/index.json", JSON.stringify(validIndex)],
    [`https://metrics.invalid/dotnet/${validIndex.latest}`, JSON.stringify(latest)],
  ]);
  const result = await loadReport("https://metrics.invalid/dotnet/index.json", fetcher(entries, requests));
  assert.equal(result.latest.snapshotId, latest.snapshotId);
  assert.equal(requests.length, 2);
});
test("latest load rejects HTTP errors, invalid identity and uncommitted source with plain error wording", async () => {
  await assert.rejects(loadReport("https://metrics.invalid/index.json", fetcher(new Map())), /HTTP 404/);
  await assert.rejects(loadReport("file:///index.json"), /HTTP/);
  await assert.rejects(loadReport("https://user:password@metrics.invalid/index.json"), /credentials/);
  for (const value of [{ ...latest, snapshotId: snapshot(undefined, undefined, "2").snapshotId }, snapshot(undefined, undefined, "1", true)]) {
    const entries = new Map([
      ["https://metrics.invalid/index.json", JSON.stringify(validIndex)],
      [`https://metrics.invalid/${validIndex.latest}`, JSON.stringify(value)],
    ]);
    await assert.rejects(loadReport("https://metrics.invalid/index.json", fetcher(entries)),
      value.repository.isDirty ? { message: "Official observations require committed source." } : /identity/);
  }
});
test("lazy history fetches only intersecting months, verifies digest and reuses immutable URLs", async () => {
  const older = document([snapshot(undefined, "2026-05-01T12:00:00Z", "2")]);
  const report = { indexUrl: new URL("https://metrics.invalid/index.json"), index: index(latest, [older, currentMonth]), latest };
  const entries = new Map([
    [`https://metrics.invalid/${currentMonth.reference.path}`, currentMonth.text],
    [`https://metrics.invalid/${older.reference.path}`, older.text],
  ]);
  const requests = [];
  const first = await loadHistory(report, 30, new Map(), fetcher(entries, requests));
  assert.equal(requests.length, 1);
  assert.equal(first.cache.size, 1);
  assert.equal((await loadHistory(report, 30, first.cache, fetcher(entries, requests))).observations.length, 1);
  assert.equal(requests.length, 1);
  const extended = await loadHistory(report, 365, first.cache, fetcher(entries, requests));
  assert.equal(requests.length, 2);
  assert.equal(extended.observations.length, 2);
});
test("an imported historical selection anchors lazy requests to its UTC date, not the feed's latest date", async () => {
  const selected = snapshot(undefined, "2026-05-01T12:00:00Z", "2");
  const older = document([selected]);
  const report = { indexUrl: new URL("https://metrics.invalid/index.json"), index: index(latest, [older, currentMonth]), latest };
  const requests = [];
  const result = await loadHistory(report, 30, new Map(), fetcher(new Map([
    [`https://metrics.invalid/${older.reference.path}`, older.text],
  ]), requests), selected);
  assert.equal(requests.length, 1);
  assert.equal(result.observations.length, 1);
  assert.equal(result.observations[0].snapshotId, selected.snapshotId);
});
test("partial history failures retain the caller's cache and reject corrupt/dirty/missing latest data", async () => {
  const older = document([snapshot(undefined, "2026-09-01T12:00:00Z", "2")]);
  const report = { indexUrl: new URL("https://metrics.invalid/index.json"), index: index(latest, [older, currentMonth]), latest };
  const cache = new Map();
  await assert.rejects(loadHistory(report, 90, cache, fetcher(new Map([
    [`https://metrics.invalid/${older.reference.path}`, older.text],
  ]))), /HTTP 404/);
  assert.equal(cache.size, 0);
  await assert.rejects(loadHistory({ ...report, index: validIndex }, 30, cache, fetcher(new Map([
    [`https://metrics.invalid/${currentMonth.reference.path}`, `${currentMonth.text} `],
  ]))), /immutable address/);
  const dirty = document([snapshot(undefined, undefined, "1", true)]);
  await assert.rejects(loadHistory({ ...report, index: index(latest, [dirty]) }, 30, cache, fetcher(new Map([
    [`https://metrics.invalid/${dirty.reference.path}`, dirty.text],
  ]))), { message: "Official observations require committed source." });
  const missing = document([snapshot(undefined, "2026-10-02T12:00:00Z", "2")]);
  await assert.rejects(loadHistory({ ...report, index: index(latest, [missing]) }, 30, cache, fetcher(new Map([
    [`https://metrics.invalid/${missing.reference.path}`, missing.text],
  ]))), /missing from/);
});
