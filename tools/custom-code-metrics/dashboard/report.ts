import validateIndex from "./generated/validate-report-index.mjs";
import validateMonth from "./generated/validate-history-month.mjs";
import type { ReportIndex } from "./generated/report-index.js";
import type { HistoryMonth } from "./generated/history-month.js";
import {
  aggregate, assertMetric, COUNTS, measurementKey, parseSnapshot,
  type Observation, type Snapshot,
} from "./data.js";

export type { ReportIndex, HistoryMonth };
export type Report = { indexUrl: URL; index: ReportIndex; latest: Snapshot };
export type MonthCache = Map<string, HistoryMonth>;

function assert(condition: boolean, message: string): asserts condition {
  if (!condition) throw new Error(message);
}

export function compactObservation(snapshot: Observation): Observation {
  return {
    schemaVersion: snapshot.schemaVersion, snapshotId: snapshot.snapshotId,
    collectedAt: snapshot.collectedAt, repository: { ...snapshot.repository },
    libraries: snapshot.libraries.map(({ library, service, category, metrics }) =>
      ({ library, service, category, metrics: { ...metrics } })),
  };
}

function content(observation: Observation): string {
  return JSON.stringify([
    measurementKey(observation), observation.repository.commit, observation.repository.isDirty,
    [...observation.libraries].sort((a, b) => a.library.localeCompare(b.library))
      .map((library) => [library.library, library.service, library.category, COUNTS.map((key) => library.metrics[key])]),
  ]);
}

export function mergeObservations(observations: readonly Observation[]): Observation[] {
  const unique = new Map<string, Observation>();
  for (const observation of [...observations].sort((a, b) =>
    Date.parse(a.collectedAt) - Date.parse(b.collectedAt) || a.snapshotId.localeCompare(b.snapshotId))) {
    assert(Number.isFinite(Date.parse(observation.collectedAt)), "Invalid observation timestamp.");
    const day = new Date(observation.collectedAt).toISOString().slice(0, 10);
    const key = observation.repository.isDirty ? observation.snapshotId :
      `${measurementKey(observation)}|${observation.repository.commit}|${day}`;
    const prior = unique.get(key);
    if (prior) {
      assert(content(prior) === content(observation), `Conflicting measurements for ${day} (${observation.repository.commit.slice(0, 12)}).`);
      assert(prior.snapshotId !== observation.snapshotId ||
        Date.parse(prior.collectedAt) === Date.parse(observation.collectedAt), "Conflicting snapshot timestamps.");
    }
    unique.set(key, compactObservation(observation));
  }
  const result = [...unique.values()];
  assert(new Set(result.map((observation) => observation.snapshotId)).size === result.length, "Duplicate snapshot identity.");
  return result;
}

export function acceptReportIndex(value: unknown): ReportIndex {
  if (!validateIndex(value)) throw new Error(`Invalid report index: ${validateIndex.errors?.[0]?.message}.`);
  assert(new Set(value.history.map((reference) => reference.month)).size === value.history.length, "Duplicate history month.");
  for (const reference of value.history) {
    assert(reference.path.endsWith(`/${reference.month}.json`), "History path and month disagree.");
  }
  const match = /^snapshots\/(\d{4})(\d{2})/.exec(value.latest);
  assert(!!match && value.history.some((reference) => reference.month === `${match[1]}-${match[2]}`),
    "Latest snapshot's month is missing from history.");
  return value;
}

export function acceptHistoryMonth(value: unknown): HistoryMonth {
  if (!validateMonth(value)) throw new Error(`Invalid history month: ${validateMonth.errors?.[0]?.message}.`);
  for (const observation of value.observations) {
    assert(Number.isFinite(Date.parse(observation.collectedAt)), "Invalid history timestamp.");
    assert(new Date(observation.collectedAt).toISOString().slice(0, 7) === value.month, "Observation belongs to another UTC month.");
    assert(observation.snapshotId.endsWith(`-${observation.repository.commit}`), "Snapshot identity and revision disagree.");
    assert(observation.snapshotId.startsWith(new Date(observation.collectedAt).toISOString().slice(0, 19).replace(/[-:]/g, "")),
      "Snapshot identity and timestamp disagree.");
    const ids = new Set<string>();
    for (const library of observation.libraries) {
      assert(!ids.has(library.library), `Duplicate history library: ${library.library}.`);
      ids.add(library.library);
      assertMetric(library.metrics, library.library);
      assert(library.metrics.libraryCount === 1, "History libraryCount must be one.");
    }
    aggregate(observation.libraries);
  }
  mergeObservations(value.observations);
  return value;
}

export function rangeStart(latest: Snapshot, days: number | null): number {
  if (days === null) return -Infinity;
  assert(Number.isInteger(days) && days >= 1 && days <= 366, "History range must be between 1 and 366 days.");
  return Math.floor(Date.parse(latest.collectedAt) / 86400000) * 86400000 - (days - 1) * 86400000;
}

export function isStale(snapshot: Snapshot, now = Date.now()): boolean {
  return now - Date.parse(snapshot.collectedAt) > 36 * 3600000;
}

export function inRange(observations: readonly Observation[], latest: Snapshot, days: number | null): Observation[] {
  if (days === null) return [...observations];
  const start = rangeStart(latest, days);
  const end = (Math.floor(Date.parse(latest.collectedAt) / 86400000) + 1) * 86400000;
  return observations.filter((observation) => Date.parse(observation.collectedAt) >= start && Date.parse(observation.collectedAt) < end);
}

async function get(url: URL, fetcher: typeof fetch): Promise<Response> {
  const response = await fetcher(url, { credentials: "omit", cache: "no-cache" });
  assert(response.ok, `Report request failed: HTTP ${response.status} (${url}).`);
  return response;
}

export async function loadReport(url: string, fetcher: typeof fetch = fetch, suppliedIndex?: unknown): Promise<Report> {
  const indexUrl = new URL(url);
  assert(["https:", "http:"].includes(indexUrl.protocol) && !indexUrl.username && !indexUrl.password,
    "Report index requires an HTTP(S) URL without credentials.");
  const index = acceptReportIndex(suppliedIndex === undefined ? await (await get(indexUrl, fetcher)).json() : suppliedIndex);
  const latest = parseSnapshot(await (await get(new URL(index.latest, indexUrl), fetcher)).text());
  assert(index.latest === `snapshots/${latest.snapshotId}.json`, "Latest snapshot identity differs from the index.");
  assert(!latest.repository.isDirty, "Official reports cannot contain a dirty latest snapshot.");
  return { indexUrl, index, latest };
}

export async function loadHistory(
  report: Report, days: number, cache: MonthCache = new Map(), fetcher: typeof fetch = fetch,
  anchor: Snapshot = report.latest,
): Promise<{ observations: Observation[]; cache: MonthCache }> {
  const start = new Date(rangeStart(anchor, days)).toISOString().slice(0, 7);
  const end = new Date(anchor.collectedAt).toISOString().slice(0, 7);
  const references = report.index.history.filter((reference) => reference.month >= start && reference.month <= end);
  const next = new Map(cache);
  const loaded: Observation[] = [];
  for (const reference of references) {
    const url = new URL(reference.path, report.indexUrl);
    let month = next.get(url.href);
    if (!month) {
      const source = await (await get(url, fetcher)).text();
      const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(source));
      const hash = [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
      assert(reference.path.split("/")[1] === hash, "History content does not match its immutable address.");
      month = acceptHistoryMonth(JSON.parse(source));
      assert(month.month === reference.month, "Loaded history month differs from the index.");
      next.set(url.href, month);
    }
    assert(month.observations.every((observation) => !observation.repository.isDirty), "Official history contains a dirty observation.");
    loaded.push(...month.observations);
  }
  const latestMonth = new Date(report.latest.collectedAt).toISOString().slice(0, 7);
  assert(!references.some((reference) => reference.month === latestMonth) ||
    loaded.some((observation) => observation.snapshotId === report.latest.snapshotId),
    "Latest snapshot is missing from its history month.");
  const observations = mergeObservations([...loaded, report.latest]);
  return { observations: inRange(observations, anchor, days), cache: next };
}
