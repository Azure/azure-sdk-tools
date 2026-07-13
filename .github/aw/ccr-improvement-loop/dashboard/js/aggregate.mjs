// @ts-check
/**
 * aggregate.mjs — pure, browser-loadable aggregation core for the CCR dashboard.
 *
 * This is a net-new re-implementation of the SEMANTICS of
 * `scripts/aggregate-runs.ts` (dedupe by run.id, skip bad files, null != 0,
 * time-ordered trends, latest-per-repo). It deliberately does NOT import the Node
 * module (that file imports `.ts` + `node:fs` and cannot load in a browser).
 *
 * No `node:*` imports, no `.ts` imports — runs unchanged in a browser and under
 * `node --test`.
 *
 * @typedef {Object} TrendPoint
 * @property {string} runId
 * @property {string} repo
 * @property {string} generatedAt
 * @property {string} windowEnd
 * @property {number|null} value
 *
 * @typedef {Object} SeverityTrendPoint
 * @property {string} runId
 * @property {string} repo
 * @property {string} generatedAt
 * @property {string} windowEnd
 * @property {Record<string, number|null>} bySeverity
 *
 * @typedef {Object} RepoRate
 * @property {string} repo
 * @property {number|null} value
 *
 * @typedef {Object} Aggregation
 * @property {number} runsScanned
 * @property {number} runsKept
 * @property {number} runsSkipped
 * @property {{earliest: string|null, latest: string|null}} dateSpan
 * @property {Record<string, TrendPoint[]>} ratesOverTime
 * @property {TrendPoint[]} ccrRecallRateOverTime
 * @property {TrendPoint[]} bugFixPrRateOverTime
 * @property {SeverityTrendPoint[]} addressedRateBySeverityOverTime
 * @property {RepoRate[]} bugFixPrRateByRepo
 */

/** Schema version this dashboard understands; mirrors `SCHEMA_VERSION`. */
export const SCHEMA_VERSION = "1.0";

/**
 * Ordered list of the rate keys shown in the per-run headline table and in the
 * per-rate trend charts. headline = the primary CCR-quality signals; the rest
 * are detail rates.
 */
export const HEADLINE_RATE_KEYS = [
  "ccrRecallRate",
  "ccrCoverage",
  "bugFixPrRate",
  "addressedRate",
  "rejectedRate",
  "ignoredRate",
  "criticalCatchRate",
  "humanCommentsPerPr",
  "prCycleTime",
  "iterationsPerPr",
];

/**
 * Fixed display order for PR-type slice categories (stable across charts).
 * Unknown types fall to the end, alphabetically.
 */
export const PR_TYPE_ORDER = [
  "bug-fix",
  "feature",
  "refactor",
  "test",
  "docs",
  "chore",
];

/** Fixed display order for severity slice categories. */
export const SEVERITY_ORDER = ["critical", "substantive", "nit"];

/**
 * Pool a rate's per-slice numerators/denominators across many runs, grouped by
 * one slice dimension. Summing raw counts (not averaging per-run rates) keeps the
 * pooled value denominator-weighted and gives usable sample sizes on dimensions
 * whose per-run cells are tiny. Presentation-only (dashboard charts); NOT part of
 * the mirrored trend `aggregate` core.
 *
 * @param {any[]} runs
 * @param {string} rateKey
 * @param {"prType"|"severity"} dim
 * @param {string[]} [order] optional category display order
 * @returns {{category: string, numerator: number, denominator: number, value: number|null}[]}
 */
export function poolSlices(runs, rateKey, dim, order) {
  /** @type {Map<string, {num: number, den: number}>} */
  const acc = new Map();
  for (const r of runs) {
    const slices = r?.metrics?.rates?.[rateKey]?.slices ?? [];
    for (const s of slices) {
      const cat = s?.[dim];
      if (cat == null) continue;
      const cur = acc.get(cat) ?? { num: 0, den: 0 };
      cur.num += s.numerator ?? 0;
      cur.den += s.denominator ?? 0;
      acc.set(cat, cur);
    }
  }
  const rank = (c) => {
    const i = (order ?? []).indexOf(c);
    return i === -1 ? Number.MAX_SAFE_INTEGER : i;
  };
  return [...acc.entries()]
    .map(([category, { num, den }]) => ({
      category,
      numerator: num,
      denominator: den,
      value: den > 0 ? num / den : null,
    }))
    .sort(
      (a, b) =>
        rank(a.category) - rank(b.category) ||
        a.category.localeCompare(b.category),
    );
}

/**
 * Light validity guard mirroring `loadRun`'s skip logic: accept only objects with
 * `schemaVersion === "1.0"` and the required top-level shape (run.id / run.repo /
 * run.generatedAt strings and a metrics.rates object). Anything else is skipped.
 *
 * @param {unknown} obj
 * @returns {boolean}
 */
export function isValidRun(obj) {
  if (obj === null || typeof obj !== "object") return false;
  const o = /** @type {Record<string, unknown>} */ (obj);
  if (o.schemaVersion !== SCHEMA_VERSION) return false;
  const run = o.run;
  if (run === null || typeof run !== "object") return false;
  const r = /** @type {Record<string, unknown>} */ (run);
  if (typeof r.id !== "string") return false;
  if (typeof r.repo !== "string") return false;
  if (typeof r.generatedAt !== "string") return false;
  const metrics = o.metrics;
  if (metrics === null || typeof metrics !== "object") return false;
  const m = /** @type {Record<string, unknown>} */ (metrics);
  if (m.rates === null || typeof m.rates !== "object") return false;
  return true;
}

/**
 * De-duplicate runs by `run.id`, keeping the one with the latest `generatedAt`.
 * Mirrors `dedupeRuns` in aggregate-runs.ts.
 *
 * @param {any[]} runs
 * @returns {any[]}
 */
export function dedupeRuns(runs) {
  /** @type {Map<string, any>} */
  const byId = new Map();
  for (const r of runs) {
    const existing = byId.get(r.run.id);
    if (
      !existing ||
      Date.parse(r.run.generatedAt) >= Date.parse(existing.run.generatedAt)
    ) {
      byId.set(r.run.id, r);
    }
  }
  return [...byId.values()];
}

/**
 * Order runs for display along the time axis. A run measures a *window*, so the
 * trend x-axis is keyed on `run.windowEnd` (when the measured window closed),
 * NOT `generatedAt` (when the script happened to run). This keeps backfilled
 * runs — all generated on the same day — spread across their real windows.
 * De-dup/supersede still keys on `generatedAt` (see `dedupeRuns`).
 *
 * @param {any[]} runs
 * @returns {any[]}
 */
function sortByTime(runs) {
  return [...runs].sort(
    (a, b) =>
      String(a.run.windowEnd).localeCompare(String(b.run.windowEnd)) ||
      String(a.run.id).localeCompare(String(b.run.id)),
  );
}

/**
 * Read a rate `value` (may be null) from a run's metrics, tolerating a missing key.
 *
 * @param {any} run
 * @param {string} key
 * @returns {number|null}
 */
function rateValue(run, key) {
  const rate = run.metrics.rates[key];
  if (rate == null) return null;
  return rate.value ?? null;
}

/**
 * Build the trend aggregation from a set of already-validated runs. Pure. De-dups
 * by `run.id`, orders time series by `windowEnd`, and reports the latest run per
 * repo for the by-repo bug-fix rate. Mirrors `aggregate` in aggregate-runs.ts.
 * `value` passthrough may be `null` and is NEVER coerced to 0.
 *
 * @param {any[]} runs
 * @param {number} [skipped]
 * @param {number} [scanned]
 * @returns {Aggregation}
 */
export function aggregate(runs, skipped = 0, scanned = runs.length + skipped) {
  const deduped = sortByTime(dedupeRuns(runs));

  /**
   * One trend series per rate key, aligned by run window. `value` passthrough
   * may be `null` and is NEVER coerced to 0.
   * @type {Record<string, TrendPoint[]>}
   */
  const ratesOverTime = {};
  for (const key of HEADLINE_RATE_KEYS) {
    ratesOverTime[key] = deduped.map((r) => ({
      runId: r.run.id,
      repo: r.run.repo,
      generatedAt: r.run.generatedAt,
      windowEnd: r.run.windowEnd,
      value: rateValue(r, key),
    }));
  }

  // Kept as named aliases for existing callers/tests.
  const ccrRecallRateOverTime = ratesOverTime.ccrRecallRate;
  const bugFixPrRateOverTime = ratesOverTime.bugFixPrRate;

  /** @type {SeverityTrendPoint[]} */
  const addressedRateBySeverityOverTime = deduped.map((r) => {
    const metric = r.metrics.rates.addressedRate;
    /** @type {Record<string, number|null>} */
    const bySeverity = {};
    for (const slice of metric?.slices ?? []) {
      if (slice.severity != null) {
        bySeverity[slice.severity] = slice.value ?? null;
      }
    }
    return {
      runId: r.run.id,
      repo: r.run.repo,
      generatedAt: r.run.generatedAt,
      windowEnd: r.run.windowEnd,
      bySeverity,
    };
  });

  // Latest run per repo (deduped is time-sorted) drives the by-repo bug-fix rate.
  /** @type {Map<string, any>} */
  const latestByRepo = new Map();
  for (const r of deduped) latestByRepo.set(r.run.repo, r);
  /** @type {RepoRate[]} */
  const bugFixPrRateByRepo = [...latestByRepo.entries()]
    .map(([repo, r]) => ({ repo, value: rateValue(r, "bugFixPrRate") }))
    .sort((a, b) => a.repo.localeCompare(b.repo));

  const times = deduped.map((r) => r.run.windowEnd);
  return {
    runsScanned: scanned,
    runsKept: deduped.length,
    runsSkipped: skipped,
    dateSpan: {
      earliest: times[0] ?? null,
      latest: times.at(-1) ?? null,
    },
    ratesOverTime,
    ccrRecallRateOverTime,
    bugFixPrRateOverTime,
    addressedRateBySeverityOverTime,
    bugFixPrRateByRepo,
  };
}

/**
 * @typedef {Object} HeadlineRate
 * @property {string} key
 * @property {number|null} value
 * @property {"up"|"down"|null} direction
 * @property {boolean} lowConfidence
 * @property {Array<{severity: string|null, prType: string|null, value: number|null}>} slices
 *
 * @typedef {Object} RunHeadline
 * @property {string} runId
 * @property {string} repo
 * @property {string} generatedAt
 * @property {string} windowEnd
 * @property {number|null} prCount
 * @property {string[]} coverageWarnings
 * @property {boolean} hasExperiment
 * @property {HeadlineRate[]} rates
 */

/**
 * Extract the per-run headline view (rates + low-confidence flags + coverage
 * warnings) for the detail table. Missing rates are skipped; `value` may be null.
 *
 * @param {any} run
 * @returns {RunHeadline}
 */
export function perRunHeadline(run) {
  /** @type {HeadlineRate[]} */
  const rates = [];
  for (const key of HEADLINE_RATE_KEYS) {
    const metric = run.metrics.rates[key];
    if (metric == null) continue;
    rates.push({
      key,
      value: metric.value ?? null,
      direction: metric.direction ?? null,
      lowConfidence: metric.lowConfidence === true,
      slices: (metric.slices ?? []).map((/** @type {any} */ s) => ({
        severity: s.severity ?? null,
        prType: s.prType ?? null,
        value: s.value ?? null,
      })),
    });
  }
  return {
    runId: run.run.id,
    repo: run.run.repo,
    generatedAt: run.run.generatedAt,
    windowEnd: run.run.windowEnd,
    prCount: run.metrics.counts?.prCount ?? null,
    coverageWarnings: run.metrics.coverageWarnings ?? [],
    hasExperiment: run.experiment != null,
    rates,
  };
}
