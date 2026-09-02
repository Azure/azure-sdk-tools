import assert from "node:assert/strict";
import { mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { loadCases, runE2e } from "./run-e2e.mjs";
import { compareComplianceRollout } from "./compare-compliance-rollout.mjs";

test("replays all 11 historical assessments and retains HTML", () => {
  const output = mkdtempSync(join(tmpdir(), "typespec-assessment-e2e-"));
  const summary = runE2e({ case: "all", output });

  assert.equal(loadCases().length, 11);
  assert.equal(summary.caseCount, 11);
  assert.equal(summary.results.length, 11);
  assert.ok(summary.results.every((item) => Number.isInteger(item.elapsedMs)));
  const comparison = compareComplianceRollout({ output });
  assert.equal(comparison.caseCount, 11);
  assert.equal(comparison.allFindingTitlesPreserved, true);
  for (const pr of summary.prs) {
    const html = readFileSync(join(output, String(pr), "assessment.html"), "utf8");
    assert.match(html, /<!doctype html>/i);
    assert.match(html, /TypeSpec assessment/i);
  }
});

test("rejects an unknown PR case", () => {
  assert.throws(
    () => runE2e({ case: "99999", output: tmpdir() }),
    /Unknown assessment case/,
  );
});
