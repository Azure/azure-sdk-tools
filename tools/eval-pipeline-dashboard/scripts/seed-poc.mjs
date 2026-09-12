import { access, copyFile, mkdir, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { publishPipelineBundle } from "../lib/local-pipeline-publisher.js";
import { loadPocPipelineConfig } from "../lib/poc-pipeline-config.js";

const dashboardRoot = resolve(import.meta.dirname, "..");
const toolsRoot = process.env.POC_VALLY_SOURCE_ROOT
  ? resolve(process.env.POC_VALLY_SOURCE_ROOT)
  : resolve(dashboardRoot, "..", "..");
const configPath = process.env.POC_PIPELINE_CONFIG
  ? resolve(process.env.POC_PIPELINE_CONFIG)
  : join(dashboardRoot, "poc-pipelines.json");
const blobRoot = join(dashboardRoot, "poc-blob");
const dataRoot = join(dashboardRoot, "poc-data");
const pipelineWorkspace = join(dataRoot, "pipeline-work");
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

await rm(blobRoot, { recursive: true, force: true });
await rm(dataRoot, { recursive: true, force: true });

let buildId = 10000;
for (const pipelineConfig of pipelines) {
  const { adoProject, repository, pipeline, pipelineDefinitionId, resultSources } = pipelineConfig;
  for (const resultSource of resultSources) {
    buildId++;
    const runId = `${repository}-${pipeline}-${buildId}`;
    const outputDirectory = join(pipelineWorkspace, runId);
    const manifest = {
      schemaVersion: 1,
      adoProject,
      repo: repository,
      pipeline,
      pipelineDefinitionId,
      buildId: String(buildId),
      summaryAttempt: 1,
      runId,
      branch: "refs/heads/main",
      sourceVersion: String(buildId).padStart(40, "0"),
      buildUrl: `https://dev.azure.com/azure-sdk/${encodeURIComponent(adoProject)}/_build/results?buildId=${buildId}`,
      runTimestamp: new Date(Date.UTC(2026, 8, 1, buildId % 24)).toISOString(),
    };

    await createPipelineOutput(
      outputDirectory,
      join(toolsRoot, resultSource),
      `${repository} / ${pipeline} / build ${buildId}`
    );
    await publishPipelineBundle({ blobRoot, inputDirectory: outputDirectory, manifest });
    await rm(outputDirectory, { recursive: true, force: true });
  }
}

await rm(pipelineWorkspace, { recursive: true, force: true });
console.log(`Published ${buildId - 10000} immutable bundles to fake Blob root ${blobRoot}`);