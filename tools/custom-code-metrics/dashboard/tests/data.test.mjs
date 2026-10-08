import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import {
  acceptSnapshot, aggregate, breakdown, filterLibraries, forRepository, history, loadIndex,
  measurementKey, mergeSnapshots, parseSnapshot, REPOSITORIES, sortLibraries, trendSeries, withoutFileEvidence,
} from "../generated/data.mjs";
import { filters, legacySnapshot, library, snapshot } from "./fixtures.mjs";

test("repository choices are exactly the seven requested languages, with only .NET collection implemented", () => {
  assert.deepEqual(REPOSITORIES.map(({ name, language }) => [name, language]), [
    ["Azure/azure-sdk-for-net", ".NET"], ["Azure/azure-sdk-for-java", "Java"],
    ["Azure/azure-sdk-for-js", "JavaScript/TypeScript"], ["Azure/azure-sdk-for-python", "Python"],
    ["Azure/azure-sdk-for-go", "Go"], ["Azure/azure-sdk-for-rust", "Rust"], ["Azure/azure-sdk-for-cpp", "C++"],
  ]);
  assert.deepEqual(REPOSITORIES.filter((repository) => repository.implemented).map((repository) => repository.name),
    ["Azure/azure-sdk-for-net"]);
});
test("repository selection scopes observations without pooling .NET data into uncollected repositories", () => {
  const observations = [snapshot(), snapshot(undefined, "2026-10-02T12:00:00Z", "2")];
  const before = structuredClone(observations);
  assert.deepEqual(forRepository(observations, REPOSITORIES[0].name), observations);
  for (const repository of REPOSITORIES.slice(1)) assert.deepEqual(forRepository(observations, repository.name), []);
  assert.deepEqual(observations, before);
});
test("selectable repositories do not widen the .NET-only initial snapshot contract", () => {
  for (const repository of REPOSITORIES.slice(1)) {
    const foreign = snapshot();
    foreign.repository.name = repository.name;
    assert.throws(() => acceptSnapshot(foreign), /Invalid snapshot/);
  }
});

test("public report snapshots remove only file evidence without mutating local audit data", () => {
  const data = snapshot([library("Azure.One", 10)]);
  data.libraries[0].files = [{ path: "sdk/alpha/Azure.One/src/One.cs", lines: 10, provenance: "custom", evidence: "no-generated-signal" }];
  const before = structuredClone(data);
  const report = withoutFileEvidence(acceptSnapshot(data));
  assert.equal(report.libraries[0].files, undefined);
  assert.equal(acceptSnapshot(report), report);
  const { files, ...expected } = data.libraries[0];
  assert.deepEqual(report, { ...data, libraries: [expected] });
  assert.deepEqual(data, before);
});

test("validates a two-way custom/generated snapshot with weighted ratios", () => {
  const data = snapshot([library("Azure.Small", 1, 0), library("Azure.Large", 10, 99)]);
  assert.equal(acceptSnapshot(data), data);
  assert.equal(data.summary.customLines, 11);
  assert.equal(data.summary.totalLines, 110);
  assert.equal(data.summary.customRatio, 0.1);
  assert.equal(data.summary.generatedLines, 99);
  assert.ok(!("unknownLines" in data.summary));
});
test("parses JSON and rejects invalid JSON explicitly", () => {
  assert.equal(parseSnapshot(JSON.stringify(snapshot())).libraries.length, 1);
  assert.throws(() => parseSnapshot("{broken"), SyntaxError);
});
test("snapshot identity must encode its actual commit and exact UTC collection time", () => {
  for (const mutate of [
    (value) => { value.repository.commit = "a".repeat(40); },
    (value) => { value.collectedAt = "2026-10-02T12:00:00Z"; },
    (value) => { value.collectedAt = "2026-10-01T12:00:00.0000001Z"; },
  ]) {
    const value = snapshot();
    mutate(value);
    const before = structuredClone(value);
    assert.throws(() => acceptSnapshot(value), /identity.*disagree/);
    assert.deepEqual(value, before);
  }
});
test("identity accepts equivalent time zones and seven-digit fractions without truncating the measured time", () => {
  for (const date of ["2026-10-01T14:00:00+02:00", "2026-10-01T12:00:00.1234567Z", "2026-10-01T12:00:00.123456700Z"]) {
    const value = snapshot(undefined, date);
    assert.equal(acceptSnapshot(value), value);
  }
});
test("rejects prototype versions 0, 2 and 3 without converting their version or counts", () => {
  for (const schemaVersion of ["0.0", "2.0", "3.0"]) {
    const prior = { ...snapshot(), schemaVersion };
    const before = structuredClone(prior);
    assert.throws(() => acceptSnapshot(prior), /Invalid snapshot/);
    assert.throws(() => parseSnapshot(JSON.stringify(prior)), /Invalid snapshot/);
    assert.deepEqual(prior, before);
  }
});
test("legacy prototype 1.0 with its actual rules metadata and unknown bucket is not the initial sealed format", () => {
  const prior = legacySnapshot();
  const before = structuredClone(prior);
  assert.equal(prior.schemaVersion, "1.0");
  assert.equal(prior.metricDefinition.classification, "inferred-file-provenance-v1");
  assert.ok("unknownFiles" in prior.libraries[0].metrics);
  assert.throws(() => acceptSnapshot(prior), /Invalid snapshot/);
  assert.throws(() => parseSnapshot(JSON.stringify(prior)), /Invalid snapshot/);
  delete prior.metricDefinition;
  assert.throws(() => acceptSnapshot(prior), /Invalid snapshot/, "Unknown counts cannot be silently discarded.");
  assert.deepEqual({ ...prior, metricDefinition: before.metricDefinition }, before);
});
test("initial format audit evidence excludes actual linked core Shared paths from other libraries regardless of provenance", () => {
  for (const [provenance, evidence] of [
    ["custom", "no-generated-signal"], ["generated", "auto-generated-header"],
  ]) {
    const member = provenance === "custom" ? library("Azure.Consumer", 10) : library("Azure.Consumer", 0, 10);
    const value = snapshot([member]);
    member.files = [{ path: "sdk/core/Azure.Core/src/Shared/Helper.cs", lines: 10, provenance, evidence }];
    assert.throws(() => acceptSnapshot(value), /linked core Shared helper must be excluded/);
    member.projectPath = "sdk/core/Azure.OtherCore/src/Azure.OtherCore.csproj";
    assert.throws(() => acceptSnapshot(value), /linked core Shared helper must be excluded/,
      "Another core package is still a foreign consumer.");
  }
});
test("initial format retains a core library's own Shared evidence and linked source outside the exclusion", () => {
  for (const [projectPath, sourcePath] of [
    ["sdk/core/Azure.Core/src/Azure.Core.csproj", "sdk/core/Azure.Core/src/Shared/Helper.cs"],
    ["sdk/core/Azure.OtherCore/src/Azure.OtherCore.csproj", "sdk/core/Azure.OtherCore/src/Shared/Nested/Helper.cs"],
    ["sdk/alpha/Azure.Consumer/src/Azure.Consumer.csproj", "sdk/alpha/Azure.Other/src/Shared/Helper.cs"],
    ["sdk/alpha/Azure.Consumer/src/Azure.Consumer.csproj", "sdk/core/Azure.Core/src/Helper.cs"],
    ["sdk/alpha/Azure.Consumer/src/Azure.Consumer.csproj", "sdk/core/Azure.Core/src/SharedExtra/Helper.cs"],
  ]) {
    const member = library("Azure.Consumer", 10);
    member.projectPath = projectPath;
    member.files = [{ path: sourcePath, lines: 10, provenance: "custom", evidence: "no-generated-signal" }];
    const value = snapshot([member]);
    assert.equal(acceptSnapshot(value), value);
  }
});
test("rejects missing, unsupported, or extra schema fields", () => {
  for (const mutate of [
    (s) => { delete s.summary; },
    (s) => { s.schemaVersion = "0.0"; },
    (s) => { s.extra = true; },
    (s) => { s.summary.extra = true; },
    (s) => { s.metricDefinition = { version: "1.0" }; },
    (s) => { s.summary.unknownLines = 0; },
    (s) => { s.summary.unknownFiles = 0; },
  ]) {
    const data = snapshot(); mutate(data);
    assert.throws(() => acceptSnapshot(data), /Invalid snapshot/);
  }
});
test("rejects invalid calendar dates using the generated standalone validator", () => {
  const data = snapshot();
  data.collectedAt = "2026-02-31T12:00:00Z";
  assert.throws(() => acceptSnapshot(data), /Invalid snapshot/);
});
test("rejects negative and fractional counts, out-of-range ratios, and duplicate frameworks", () => {
  for (const mutate of [
    (s) => { s.summary.customLines = -1; },
    (s) => { s.summary.totalFiles = 1.5; },
    (s) => { s.summary.customRatio = 1.1; },
    (s) => { s.libraries[0].targetFrameworks = ["net8.0", "net8.0"]; },
  ]) {
    const data = snapshot(); mutate(data);
    assert.throws(() => acceptSnapshot(data), /Invalid snapshot/);
  }
});
test("rejects inaccurate integers beyond JavaScript's safe range", () => {
  const data = snapshot();
  data.libraries[0].metrics.customLines = Number.MAX_SAFE_INTEGER + 1;
  assert.throws(() => acceptSnapshot(data), /safe integer/);
});
test("rejects timestamps that JavaScript cannot represent rather than rendering invalid dates", () => {
  const data = snapshot(); data.collectedAt = "2026-12-31T23:59:60Z";
  assert.throws(() => acceptSnapshot(data), /representable/);
});
test("aggregation rejects overflow and does not mutate source metrics", () => {
  const first = library("Azure.One", Number.MAX_SAFE_INTEGER);
  const second = library("Azure.Two", 1);
  const before = structuredClone(first);
  assert.throws(() => aggregate([first, second]), /exact integer range/);
  assert.deepEqual(first, before);
});
test("empty cohorts have zero counts and null rather than zero-percent ratios", () => {
  const metric = aggregate([]);
  assert.equal(metric.libraryCount, 0);
  assert.equal(metric.totalLines, 0);
  assert.equal(metric.customRatio, null);
  assert.equal(acceptSnapshot(snapshot([library("Azure.Empty", 0)])).summary.customRatio, null);
});
test("requires null for zero denominators and correct nonempty ratios", () => {
  const empty = snapshot([library("Azure.Empty", 0)]);
  empty.summary.customRatio = 0;
  assert.throws(() => acceptSnapshot(empty), /null ratio/);
  const data = snapshot(); data.libraries[0].metrics.customRatio = 0.9;
  assert.throws(() => acceptSnapshot(data), /numerator and denominator/);
});
test("checks line/file provenance sums independently of schema types", () => {
  const lines = snapshot(); lines.libraries[0].metrics.totalLines++;
  assert.throws(() => acceptSnapshot(lines), /line provenance/);
  const files = snapshot(); files.libraries[0].metrics.totalFiles++;
  assert.throws(() => acceptSnapshot(files), /file provenance/);
});
test("rejects inconsistent repository, service, and category rollups", () => {
  const root = snapshot(); root.summary.customFiles++; root.summary.totalFiles++;
  assert.throws(() => acceptSnapshot(root), /rollup differs/);
  const service = snapshot(); service.services[0].metrics.libraryCount++;
  assert.throws(() => acceptSnapshot(service), /rollup differs/);
  const category = snapshot(); category.categories[0].metrics.libraryCount++;
  assert.throws(() => acceptSnapshot(category), /rollup differs/);
});
test("rejects duplicate library IDs, service IDs, and category entries", () => {
  const libs = snapshot([library("Azure.One", 1), library("Azure.One", 1)]);
  assert.throws(() => acceptSnapshot(libs), /Duplicate library/);
  const services = snapshot(); services.services.push(structuredClone(services.services[0]));
  assert.throws(() => acceptSnapshot(services), /Duplicate service/);
  const category = snapshot(); category.categories[2] = category.categories[0];
  assert.throws(() => acceptSnapshot(category), /categories must be distinct/);
});
test("requires a service rollup for every library", () => {
  const data = snapshot(); data.services = [];
  assert.throws(() => acceptSnapshot(data), /service is missing/);
});
test("validates optional file evidence and its provenance partition", () => {
  const data = snapshot([library("Azure.One", 2)]);
  data.libraries[0].files = [{
    path: "sdk/alpha/Azure.One/src/Source.cs", lines: 2,
    provenance: "custom", evidence: "no-generated-signal",
  }];
  acceptSnapshot(data);
  data.libraries[0].files[0].provenance = "generated";
  assert.throws(() => acceptSnapshot(data), /provenance differs/);
});
test("rejects missing, duplicate, and miscounted file evidence", () => {
  const data = snapshot([library("Azure.One", 2)]);
  data.libraries[0].files = [];
  assert.throws(() => acceptSnapshot(data), /file evidence count/);
  data.libraries[0].files = [{
    path: "sdk/alpha/Azure.One/src/Source.cs", lines: 1,
    provenance: "custom", evidence: "no-generated-signal",
  }];
  assert.throws(() => acceptSnapshot(data), /line total differs/);
  const duplicate = snapshot([library("Azure.One", 1, 1)]);
  duplicate.libraries[0].files = [data.libraries[0].files[0], data.libraries[0].files[0]];
  assert.throws(() => acceptSnapshot(duplicate), /duplicate file evidence/);
});
test("filters category, service, and case-insensitive trimmed name search together", () => {
  const libs = [library("Azure.One", 1), library("Azure.Two", 2, 0, "beta", "management")];
  assert.deepEqual(filterLibraries(libs, { category: "management", service: "beta", search: " azure.TWO " }), [libs[1]]);
  assert.equal(filterLibraries(libs, { ...filters, search: "not present" }).length, 0);
  assert.equal(libs.length, 2);
});
test("sorts numeric values correctly and always places N/A last", () => {
  const libs = [library("Azure.Empty", 0), library("Azure.Big", 100, 900), library("Azure.Small", 2)];
  assert.deepEqual(sortLibraries(libs, "customLines", true).map((item) => item.library), ["Azure.Big", "Azure.Small", "Azure.Empty"]);
  assert.deepEqual(sortLibraries(libs, "customRatio", true).map((item) => item.library), ["Azure.Small", "Azure.Big", "Azure.Empty"]);
  assert.deepEqual(sortLibraries(libs, "customRatio", false).map((item) => item.library), ["Azure.Big", "Azure.Small", "Azure.Empty"]);
  assert.equal(sortLibraries(libs, "library", false)[0].library, "Azure.Big");
  assert.equal(libs[0].library, "Azure.Empty");
});
test("breakdowns retain empty categories and weight source counts by library", () => {
  const libs = [library("Azure.One", 1), library("Azure.Two", 10, 90)];
  const rows = breakdown(libs, "category");
  assert.equal(rows.length, 3);
  assert.equal(rows[0].metrics.customRatio, 11 / 101);
  assert.equal(rows.find((item) => item.name === "management").metrics.customRatio, null);
  assert.equal(breakdown(libs, "service")[0].name, "alpha");
});
test("measurement compatibility is independent of JSON property order", () => {
  const data = snapshot();
  const reordered = structuredClone(data);
  reordered.repository = Object.fromEntries(Object.entries(reordered.repository).reverse());
  assert.equal(measurementKey(data), measurementKey(reordered));
});
test("deduplicates snapshot IDs, normalizes metadata order, and accepts added evidence", () => {
  const data = snapshot([library("Azure.One", 2)]);
  const detailed = structuredClone(data);
  detailed.repository = Object.fromEntries(Object.entries(detailed.repository).reverse());
  detailed.libraries[0].files = [{
    path: "sdk/alpha/Azure.One/src/Source.cs", lines: 2, provenance: "custom", evidence: "no-generated-signal",
  }];
  const merged = mergeSnapshots([data], [detailed]);
  assert.equal(merged.length, 1);
  assert.ok(merged[0].libraries[0].files);
});
test("deduplicates identical clean same-revision same-day retries, keeping latest", () => {
  const first = snapshot();
  const later = snapshot(first.libraries, "2026-10-01T19:00:00Z");
  assert.deepEqual(mergeSnapshots([first], [later]), [later]);
  assert.equal(mergeSnapshots([later], [first]).length, 1);
});
test("does not deduplicate different revisions or different observation days", () => {
  const first = snapshot();
  const nextRevision = snapshot(first.libraries, "2026-10-01T19:00:00Z", "2");
  const nextDay = snapshot(first.libraries, "2026-10-02T12:00:00Z");
  assert.equal(mergeSnapshots([first], [nextRevision, nextDay]).length, 3);
});
test("rejects conflicting snapshot IDs and non-identical same-revision retries", () => {
  const first = snapshot();
  const conflicting = snapshot([library("Azure.One", 9, 30)]);
  assert.throws(() => mergeSnapshots([first], [conflicting]), /Conflicting content/);
  conflicting.collectedAt = "2026-10-01T19:00:00Z";
  conflicting.snapshotId = snapshot(undefined, conflicting.collectedAt).snapshotId;
  assert.throws(() => mergeSnapshots([first], [conflicting]), /not identical retries/);
});
test("keeps distinct dirty observations without treating the HEAD SHA as source identity", () => {
  const first = snapshot(undefined, "2026-10-01T12:00:00Z", "1", true);
  const later = snapshot([library("Azure.One", 20)], "2026-10-01T19:00:00Z", "1", true);
  assert.equal(mergeSnapshots([first], [later]).length, 2);
});
test("fixed-cohort history isolates changes in shared library IDs", () => {
  const first = snapshot([library("Azure.One", 80, 20), library("Azure.Old", 10, 90)]);
  const later = snapshot([library("Azure.One", 60, 40), library("Azure.New", 5, 5)], "2026-10-02T12:00:00Z", "2");
  const result = history([first, later], later, filters, { fixed: true, includeDirty: false, libraryId: "" });
  assert.deepEqual([...result.fixedIds], ["Azure.One"]);
  assert.deepEqual(result.points.map((item) => item.metrics.customRatio), [0.8, 0.6]);
  assert.equal(result.distinctRevisions, 2);
});
test("observed-cohort history reports additions/removals and sums rather than averages", () => {
  const first = snapshot([library("Azure.One", 80, 20), library("Azure.Old", 10, 90)]);
  const later = snapshot([library("Azure.One", 60, 40), library("Azure.New", 5, 5)], "2026-10-02T12:00:00Z", "2");
  const result = history([first, later], later, filters, { fixed: false, includeDirty: false, libraryId: "" });
  assert.equal(result.points[1].added, 1);
  assert.equal(result.points[1].removed, 1);
  assert.equal(result.points[1].metrics.customRatio, 65 / 110);
});
test("excludes dirty and incompatible measurements from official history", () => {
  const first = snapshot();
  const dirty = snapshot(undefined, "2026-10-02T12:00:00Z", "2", true);
  const incompatible = snapshot(undefined, "2026-10-03T12:00:00Z", "3");
  incompatible.schemaVersion = "2.0";
  const result = history([first, dirty, incompatible], first, filters, { fixed: true, includeDirty: false, libraryId: "" });
  assert.equal(result.points.length, 1);
  assert.equal(result.excludedDirty, 1);
  assert.equal(result.excludedIncompatible, 1);
  assert.equal(history([first, dirty], first, filters, { fixed: false, includeDirty: true, libraryId: "" }).points.length, 2);
});
test("missing selected libraries and empty fixed cohorts produce N/A, not zeros", () => {
  const first = snapshot([library("Azure.Old", 1)]);
  const later = snapshot([library("Azure.New", 2)], "2026-10-02T12:00:00Z", "2");
  const options = { fixed: true, includeDirty: false, libraryId: "" };
  assert.equal(history([first, later], later, filters, options).fixedIds.size, 0);
  assert.ok(history([first, later], later, filters, options).points.every((point) => point.metrics.customRatio === null));
  const selected = history([first, later], later, filters, { ...options, fixed: false, libraryId: "Azure.Old" });
  assert.equal(selected.points[1].metrics.customRatio, null);
  assert.equal(selected.points[1].removed, 1);
});
test("keeps one-code-revision history a baseline and inserts null gaps for missing nightly days", () => {
  const first = snapshot();
  const later = snapshot(first.libraries, "2026-10-04T12:00:00Z");
  const result = history([first, later], later, filters, { fixed: true, includeDirty: false, libraryId: "" });
  assert.equal(result.distinctRevisions, 1);
  const series = trendSeries(result.points);
  assert.equal(series.length, 3);
  assert.equal(series[1].y, null);
  assert.equal(new Date(series[1].x).toISOString(), "2026-10-02T00:00:00.000Z");
});
test("loads URL indexes relative to their location without cookies or uploads", async () => {
  const requested = [];
  const source = snapshot();
  const fakeFetch = async (url, options) => {
    requested.push([String(url), options]);
    return new Response(JSON.stringify(requested.length === 1 ? { snapshots: ["day-1.json"] } : source));
  };
  const result = await loadIndex("https://example.test/metrics/index.json", fakeFetch);
  assert.equal(result.length, 1);
  assert.equal(requested[1][0], "https://example.test/metrics/day-1.json");
  assert.ok(requested.every(([, options]) => options.credentials === "omit" && options.method === undefined));
});
test("rejects unsafe URL schemes and invalid/empty index shapes before importing", async () => {
  const fake = async () => new Response(JSON.stringify({ snapshots: ["javascript:alert(1)"] }));
  await assert.rejects(loadIndex("file:///index.json", fake), /HTTP/);
  await assert.rejects(loadIndex("https://example.test/index.json", fake), /Unsupported snapshot URL/);
  await assert.rejects(loadIndex("https://example.test/index.json", async () => new Response('{"snapshots":[]}')), /at least one/);
  await assert.rejects(loadIndex("https://example.test/index.json", async () => new Response("{}")), /snapshots array/);
});
test("surfaces failed index and snapshot requests without returning an empty success", async () => {
  await assert.rejects(loadIndex("https://example.test/index.json", async () => new Response("error", { status: 503 })), /HTTP 503/);
  let count = 0;
  await assert.rejects(loadIndex("https://example.test/index.json", async () => {
    count++;
    return count === 1 ? new Response('{"snapshots":["one.json"]}') : new Response("error", { status: 404 });
  }), /HTTP 404/);
});
if (process.env.CUSTOM_CODE_METRICS_SNAPSHOT) {
  test("validates the supplied real collector observation against the full dashboard contract", async () => {
    const source = parseSnapshot(await readFile(process.env.CUSTOM_CODE_METRICS_SNAPSHOT, "utf8"));
    assert.equal(source.summary.libraryCount, source.libraries.length);
    assert.deepEqual(aggregate(source.libraries), {
      ...source.summary, customRatio: source.summary.totalLines ? source.summary.customLines / source.summary.totalLines : null,
    });
  });
  test("real initial-format provisioning CostManagement retains the measured helper exclusion counts", async () => {
    const source = parseSnapshot(await readFile(process.env.CUSTOM_CODE_METRICS_SNAPSHOT, "utf8"));
    const measured = source.libraries.find((library) => library.library === "Azure.Provisioning.CostManagement");
    assert.ok(measured, "The full real observation is missing provisioning CostManagement.");
    assert.equal(source.schemaVersion, "1.0");
    assert.equal(measured.metrics.customLines, 120);
    assert.equal(measured.metrics.generatedLines, 8697);
    assert.equal(measured.metrics.totalLines, 8817);
    assert.ok(Math.abs(measured.metrics.customRatio - 120 / 8817) <= 1e-12);
  });
}
