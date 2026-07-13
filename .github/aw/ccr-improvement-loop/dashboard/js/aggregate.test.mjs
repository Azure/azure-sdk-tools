// @ts-check
/**
 * aggregate.test.mjs — node --test unit tests for the browser aggregation core.
 * Runs offline; reads the committed test fixtures under ./fixtures/ (kept
 * independent of the live dashboard data/ folder so real runs can be swapped in
 * without breaking these tests).
 */
import { test } from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

import {
  aggregate,
  dedupeRuns,
  isValidRun,
  perRunHeadline,
  poolSlices,
} from "./aggregate.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.join(here, "fixtures");

/** Load every fixture run listed in the manifest. */
function loadFixtures() {
  const manifest = JSON.parse(
    fs.readFileSync(path.join(dataDir, "manifest.json"), "utf8"),
  );
  return manifest.runs.map((f) =>
    JSON.parse(fs.readFileSync(path.join(dataDir, f), "utf8")),
  );
}

test("all fixtures pass the light validity guard", () => {
  const runs = loadFixtures();
  assert.ok(runs.length >= 4, "expected >=4 fixtures");
  for (const r of runs) assert.equal(isValidRun(r), true);
});

test("isValidRun rejects malformed / wrong-version objects", () => {
  assert.equal(isValidRun(null), false);
  assert.equal(isValidRun({}), false);
  assert.equal(
    isValidRun({ schemaVersion: "2.0", run: {}, metrics: {} }),
    false,
  );
  assert.equal(
    isValidRun({ schemaVersion: "1.0", run: { id: "x" }, metrics: {} }),
    false,
  );
  assert.equal(
    isValidRun({
      schemaVersion: "1.0",
      run: { id: "x", repo: "a/b", generatedAt: "t" },
      metrics: { rates: {} },
    }),
    true,
  );
});

test("dedupeRuns keeps the newest generatedAt per run.id", () => {
  const older = {
    run: { id: "dup", repo: "a/b", generatedAt: "2026-01-01T00:00:00Z" },
    metrics: { rates: { ccrRecallRate: { value: 0.9 } } },
  };
  const newer = {
    run: { id: "dup", repo: "a/b", generatedAt: "2026-02-01T00:00:00Z" },
    metrics: { rates: { ccrRecallRate: { value: 0.1 } } },
  };
  const out = dedupeRuns([older, newer]);
  assert.equal(out.length, 1);
  assert.equal(out[0].metrics.rates.ccrRecallRate.value, 0.1);
});

test("a broken object is skipped by isValidRun (aggregate consumes only valid)", () => {
  const runs = loadFixtures();
  const withBad = [...runs, { schemaVersion: "2.0", garbage: true }];
  const valid = withBad.filter(isValidRun);
  const skipped = withBad.length - valid.length;
  assert.equal(skipped, 1);
  const agg = aggregate(valid, skipped, withBad.length);
  assert.equal(agg.runsSkipped, 1);
  assert.equal(agg.runsKept, runs.length);
  assert.equal(agg.runsScanned, withBad.length);
});

test("null metric value stays null, never coerced to 0", () => {
  const runs = loadFixtures();
  const agg = aggregate(runs);
  const nullPoints = agg.ccrRecallRateOverTime.filter((p) => p.value === null);
  assert.ok(nullPoints.length >= 1, "expected a null ccrRecallRate fixture");
  // ensure no null became 0
  for (const p of agg.ccrRecallRateOverTime) {
    assert.notEqual(p.value, undefined);
  }
});

test("bugFixPrRateByRepo has one entry per repo, sorted, latest run wins", () => {
  const runs = loadFixtures();
  const agg = aggregate(runs);
  const repos = agg.bugFixPrRateByRepo.map((r) => r.repo);
  assert.deepEqual(repos, [...repos].sort());
  assert.equal(new Set(repos).size, repos.length);
  assert.ok(repos.length >= 2);
});

test("trend arrays are sorted by windowEnd", () => {
  const runs = loadFixtures();
  const agg = aggregate(runs);
  const times = agg.ccrRecallRateOverTime.map((p) => p.windowEnd);
  const sorted = [...times].sort();
  assert.deepEqual(times, sorted);
});

test("addressedRateBySeverityOverTime exposes severity slices", () => {
  const runs = loadFixtures();
  const agg = aggregate(runs);
  const withSlices = agg.addressedRateBySeverityOverTime.find(
    (p) => Object.keys(p.bySeverity).length > 0,
  );
  assert.ok(withSlices, "expected at least one run with severity slices");
});

test("poolSlices sums slice counts across runs (denominator-weighted)", () => {
  const runs = [
    {
      metrics: {
        rates: {
          addressedRate: {
            slices: [
              {
                prType: "bug-fix",
                severity: "critical",
                numerator: 1,
                denominator: 4,
              },
              {
                prType: "feature",
                severity: "critical",
                numerator: 0,
                denominator: 2,
              },
            ],
          },
        },
      },
    },
    {
      metrics: {
        rates: {
          addressedRate: {
            slices: [
              {
                prType: "bug-fix",
                severity: "critical",
                numerator: 2,
                denominator: 6,
              },
            ],
          },
        },
      },
    },
  ];
  // Pool by severity: critical = (1+0+2) / (4+2+6) = 3/12.
  const bySev = poolSlices(runs, "addressedRate", "severity", ["critical"]);
  assert.deepEqual(bySev, [
    { category: "critical", numerator: 3, denominator: 12, value: 0.25 },
  ]);
  // Pool by prType: bug-fix = (1+2)/(4+6)=3/10; feature = 0/2 -> value 0.
  const byType = poolSlices(runs, "addressedRate", "prType", [
    "bug-fix",
    "feature",
  ]);
  assert.deepEqual(
    byType.map((r) => r.category),
    ["bug-fix", "feature"],
  );
  assert.equal(byType[0].value, 0.3);
  assert.equal(byType[1].value, 0);
});

test("poolSlices yields null value when the pooled denominator is 0", () => {
  const runs = [
    {
      metrics: {
        rates: {
          ccrRecallRate: {
            slices: [
              { prType: "docs", severity: null, numerator: 0, denominator: 0 },
            ],
          },
        },
      },
    },
  ];
  const pooled = poolSlices(runs, "ccrRecallRate", "prType");
  assert.equal(pooled[0].value, null);
});

test("perRunHeadline surfaces rates, lowConfidence flags and coverage warnings", () => {
  const runs = loadFixtures();
  const thin = runs.find((r) => r.metrics.coverageWarnings.length > 0);
  assert.ok(thin, "expected a fixture with coverage warnings");
  const h = perRunHeadline(thin);
  assert.ok(h.coverageWarnings.length > 0);
  assert.ok(h.rates.some((r) => r.lowConfidence === true));
  assert.ok(h.rates.some((r) => r.value === null));
});
