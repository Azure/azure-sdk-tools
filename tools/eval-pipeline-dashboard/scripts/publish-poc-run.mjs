import { copyFile, mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { submitPipelineBundle } from "../lib/dashboard-client.js";

const dashboardRoot = resolve(import.meta.dirname, "..");
const toolsRoot = process.env.POC_VALLY_SOURCE_ROOT
  ? resolve(process.env.POC_VALLY_SOURCE_ROOT)
  : resolve(dashboardRoot, "..", "..");
const buildId = String(Date.now());
const repo = process.argv[2] ?? "azure-sdk-tools";
const pipeline = process.argv[3] ?? "skill-eval";
const definitionId = process.argv[4] ?? "8178";
const runId = `${repo}-${pipeline}-${buildId}`;
const outputDirectory = await mkdtemp(join(tmpdir(), "vally-demo-publish-"));
const sourcePath = resolve(
  toolsRoot,
  "artifacts/vally-local/markdown-token-optimizer/2026-07-17T18-39-00-974Z/results.jsonl"
);
const manifest = {
  schemaVersion: 1,
  adoOrganization: process.env.POC_ADO_ORGANIZATION || "azure-sdk",
  adoProject: "internal",
  repo,
  pipeline,
  pipelineDefinitionId: definitionId,
  buildId,
  summaryAttempt: 1,
  runId,
  branch: "refs/heads/main",
  sourceVersion: buildId.padStart(40, "0"),
  buildUrl: `https://dev.azure.com/azure-sdk/internal/_build/results?buildId=${buildId}`,
  runTimestamp: new Date().toISOString(),
};

try {
  await mkdir(join(outputDirectory, "junit"), { recursive: true });
  await copyFile(sourcePath, join(outputDirectory, "results.jsonl"));
  await writeFile(
    join(outputDirectory, "eval-summary.md"),
    `# ${repo} / ${pipeline} / build ${buildId}\n`
  );
  await writeFile(
    join(outputDirectory, "junit", "eval-results.junit.xml"),
    '<testsuites><testsuite name="poc-live"><testcase name="continuous publish" /></testsuite></testsuites>\n'
  );
  const receipt = await submitPipelineBundle({
    dashboardUrl: process.env.POC_DASHBOARD_URL || "http://127.0.0.1:3201",
    accessToken: process.env.DASHBOARD_ACCESS_TOKEN,
    inputDirectory: outputDirectory,
    manifest,
  });
  console.log(JSON.stringify(receipt, null, 2));
} finally {
  await rm(outputDirectory, { recursive: true, force: true });
}