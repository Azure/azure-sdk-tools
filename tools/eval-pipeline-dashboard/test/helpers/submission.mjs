import { strToU8, zipSync } from "fflate";

export const manifest = {
  schemaVersion: 1, adoOrganization: "azure-sdk", adoProject: "internal", repo: "azure-sdk-tools",
  pipeline: "cloud-test", pipelineDefinitionId: "8178", buildId: "1001", summaryAttempt: 1,
  runTimestamp: "2026-09-14T00:00:00Z",
};
export const trial = { type: "trial-result", itemId: "trial-1", evalName: "sample",
  evalFilePath: "sample.eval.yaml", variant: "default", stimulus: "sample", model: "model",
  status: "error", durationMs: 1, error: "Synthetic test error", trajectory: null, gradeResult: null };
export function bundle(overrides = {}) {
  return zipSync({ "manifest.json": strToU8(JSON.stringify({ ...manifest, ...overrides })),
    "results.jsonl": strToU8(JSON.stringify(trial) + "\n"), "eval-summary.md": strToU8("# Test\n"),
    "junit/results.xml": strToU8("<testsuites />") }, { level: 6, mtime: new Date("2020-01-01T00:00:00Z") });
}