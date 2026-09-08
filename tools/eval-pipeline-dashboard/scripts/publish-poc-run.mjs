import { copyFile, mkdir, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { publishPipelineBundle } from "../lib/local-pipeline-publisher.js";

const dashboardRoot = resolve(import.meta.dirname, "..");
const toolsRoot = process.env.POC_VALLY_SOURCE_ROOT
  ? resolve(process.env.POC_VALLY_SOURCE_ROOT)
  : resolve(dashboardRoot, "..", "..");
const buildId = String(Date.now());
const repo = process.argv[2] ?? "azure-sdk-tools";
const pipeline = process.argv[3] ?? "skill-eval";
const definitionId = process.argv[4] ?? "8178";
const runId = `${repo}-${pipeline}-${buildId}`;
const outputDirectory = join(dashboardRoot, "poc-data", "pipeline-work", runId);
const sourcePath = resolve(
  toolsRoot,
  "artifacts/vally-local/markdown-token-optimizer/2026-07-17T18-39-00-974Z/results.jsonl"
);
const manifest = {
  schemaVersion: 1,
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
  const blob = await publishPipelineBundle({
    blobRoot: join(dashboardRoot, "poc-blob"),
    inputDirectory: outputDirectory,
    manifest,
  });
  console.log(`Published ${blob.name}`);
} finally {
  await rm(outputDirectory, { recursive: true, force: true });
}