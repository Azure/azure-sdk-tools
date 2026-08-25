import assert from "node:assert/strict";
import test from "node:test";

import { summarizeBenchmark } from "./benchmark-assessment.mjs";

function draft(totalMs, preparationMs, hits, misses) {
  return {
    assessmentDuration: {
      totalMs,
      preparationMs,
      deterministicAnalysisMs: 25,
      documentationEvidenceMs: 100,
    },
    artifactCache: { hits, misses },
    checkoutCache: {
      reused: { base: hits > 0, head: hits > 0 },
    },
  };
}

test("benchmark summaries compare complete cold and warm runs", () => {
  const summary = summarizeBenchmark(
    draft(600000, 500000, 0, 4),
    draft(240000, 140000, 4, 0),
  );
  assert.equal(summary.improvement.totalMs, 360000);
  assert.equal(summary.improvement.percent, 60);
  assert.equal(summary.cold.artifactCacheMisses, 4);
  assert.equal(summary.warm.artifactCacheHits, 4);
  assert.deepEqual(summary.warm.checkoutCacheReused, {
    base: true,
    head: true,
  });
  assert.match(summary.note, /documentation search/);
});
