import assert from "node:assert/strict";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { loadCases, replayCase, runE2e } from "./run-e2e.mjs";
import { compareComplianceRollout } from "./compare-compliance-rollout.mjs";

test("replays all 12 historical assessments and retains HTML", () => {
  const output = mkdtempSync(join(tmpdir(), "typespec-assessment-e2e-"));
  const summary = runE2e({ case: "all", output });

  assert.equal(loadCases().length, 12);
  assert.equal(summary.caseCount, 12);
  assert.equal(summary.results.length, 12);
  assert.ok(summary.results.every((item) => Number.isInteger(item.elapsedMs)));
  const comparison = compareComplianceRollout({ output });
  assert.equal(comparison.caseCount, 12);
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

test("replay retains an explicit matching downstream graph and rejects mismatches", () => {
  const root = mkdtempSync(join(tmpdir(), "typespec-assessment-graph-"));
  const testCase = loadCases().find(({ pr }) => pr === 42435);
  const canonical = join(root, "assessments", String(testCase.pr));
  mkdirSync(canonical, { recursive: true });
  const assessment = JSON.parse(readFileSync(
    new URL("../assessments/42435/assessment.json", import.meta.url), "utf8",
  ));
  const findings = assessment.dimensions.downstream.findings;
  const downstreamInput = {
    facts: Object.fromEntries(findings.flatMap((finding) =>
      finding.evidence.map((fact) => [fact.id, fact]))),
    candidates: findings.map(({ id, rule, crossLanguageDefinitionId, evidenceFactIds }) =>
      ({ id, rule, crossLanguageDefinitionId, evidenceFactIds })),
    rootCauses: assessment.dimensions.downstream.rootCauses,
  };
  writeFileSync(join(canonical, "assessment.json"), JSON.stringify(assessment));
  writeFileSync(join(canonical, "downstream-input.json"), JSON.stringify(downstreamInput));
  const output = join(root, "outputs");
  replayCase(testCase, { root, output });
  assert.deepEqual(JSON.parse(readFileSync(
    join(output, String(testCase.pr), "downstream-input.json"), "utf8",
  )), downstreamInput);

  downstreamInput.facts[findings[0].evidenceFactIds[0]].name = "mismatched";
  writeFileSync(join(canonical, "downstream-input.json"), JSON.stringify(downstreamInput));
  assert.throws(() => replayCase(testCase, { root, output }), /snapshot mismatch/);
});
