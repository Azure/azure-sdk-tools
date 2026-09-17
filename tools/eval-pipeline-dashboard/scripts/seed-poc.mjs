import { access, copyFile, mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { submitPipelineBundle } from "../lib/dashboard-client.js";
import { loadPocPipelineConfig } from "../lib/poc-pipeline-config.js";

const dashboardRoot = resolve(import.meta.dirname, "..");
const toolsRoot = process.env.POC_VALLY_SOURCE_ROOT
  ? resolve(process.env.POC_VALLY_SOURCE_ROOT)
  : resolve(dashboardRoot, "..", "..");
const configPath = process.env.POC_PIPELINE_CONFIG
  ? resolve(process.env.POC_PIPELINE_CONFIG)
  : join(dashboardRoot, "poc-pipelines.json");
const dashboardUrl = process.env.POC_DASHBOARD_URL || "http://127.0.0.1:3201";
const { pipelines } = await loadPocPipelineConfig(configPath);

async function createPipelineOutput(outputDirectory, resultsPath, label) {
  await mkdir(join(outputDirectory, "junit"), { recursive: true });
  await copyFile(resultsPath, join(outputDirectory, "results.jsonl"));
  await writeFile(join(outputDirectory, "eval-summary.md"), `# ${label}\n\nPOC pipeline summary.\n`);
  await writeFile(
    join(outputDirectory, "junit", "eval-results.junit.xml"),
    `<testsuites><testsuite name="${label}"><testcase name="dashboard bundle" /></testsuite></testsuites>\n`
  );
}

for (const pipelineConfig of pipelines) {
  for (const resultSource of pipelineConfig.resultSources) {
    const sourcePath = join(toolsRoot, resultSource);
    try {
      await access(sourcePath);
    } catch (error) {
      throw new Error(
        `Missing sample result '${sourcePath}' configured by '${configPath}'. Set POC_VALLY_SOURCE_ROOT to an azure-sdk-tools checkout containing the sample results.`,
        { cause: error }
      );
    }
  }
}

const pipelineWorkspace = await mkdtemp(join(tmpdir(), "vally-demo-submit-"));
let buildId = Date.now();
let accepted = 0;
try {
  for (const pipelineConfig of pipelines) {
    const { adoProject, repository, pipeline, pipelineDefinitionId, resultSources } = pipelineConfig;
    for (const resultSource of resultSources) {
      buildId++;
      const runId = `${repository}-${pipeline}-${buildId}`;
      const outputDirectory = join(pipelineWorkspace, runId);
      const manifest = {
        schemaVersion: 1,
        adoOrganization: process.env.POC_ADO_ORGANIZATION || "azure-sdk",
        adoProject,
        repo: repository,
        pipeline,
        pipelineDefinitionId,
        buildId: String(buildId),
        summaryAttempt: 1,
        branch: "refs/heads/main",
        sourceVersion: String(buildId).padStart(40, "0"),
        runTimestamp: new Date().toISOString(),
      };

      await createPipelineOutput(
        outputDirectory,
        join(toolsRoot, resultSource),
        `${repository} / ${pipeline} / build ${buildId}`
      );
      const receipt = await submitPipelineBundle({
        dashboardUrl, inputDirectory: outputDirectory, manifest,
        accessToken: process.env.DASHBOARD_ACCESS_TOKEN,
      });
      accepted++;
      console.log(`Accepted ${repository}/${pipeline}: ${receipt.statusUrl}`);
      await rm(outputDirectory, { recursive: true, force: true });
    }
  }
} finally {
  await rm(pipelineWorkspace, { recursive: true, force: true });
}
console.log(`Submitted ${accepted} bundles to ${dashboardUrl}; refresh the dashboard after ingestion completes.`);