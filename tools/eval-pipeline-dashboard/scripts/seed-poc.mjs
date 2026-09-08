import { access, copyFile, mkdir, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import { publishPipelineBundle } from "../lib/local-pipeline-publisher.js";

const dashboardRoot = resolve(import.meta.dirname, "..");
const toolsRoot = process.env.POC_VALLY_SOURCE_ROOT
  ? resolve(process.env.POC_VALLY_SOURCE_ROOT)
  : resolve(dashboardRoot, "..", "..");
const blobRoot = join(dashboardRoot, "poc-blob");
const dataRoot = join(dashboardRoot, "poc-data");
const pipelineWorkspace = join(dataRoot, "pipeline-work");

const sources = {
  markdown: "artifacts/vally-local/markdown-token-optimizer/2026-07-17T18-39-00-974Z/results.jsonl",
  sensei: "artifacts/vally-local/sensei/2026-07-17T18-40-03-118Z/results.jsonl",
  authoring: "artifacts/vally-local/skill-authoring/2026-07-17T18-42-13-812Z/results.jsonl",
  release2: "artifacts/vally-results/sdk-release-trigger-fix2/2026-07-16T04-59-04-330Z/results.jsonl",
  release3: "artifacts/vally-results/sdk-release-trigger-fix3/2026-07-16T05-00-08-306Z/results.jsonl",
  config: "artifacts/vally-results/_cfgcheck/2026-06-17T23-50-06-549Z/results.jsonl",
};

const pipelines = [
  ["azure-sdk-tools", "skill-eval", "8178", sources.authoring, sources.markdown],
  ["azure-sdk-tools", "workflow-eval", "8210", sources.sensei, sources.config],
  ["azure-sdk", "skill-eval", "9101", sources.authoring, sources.markdown],
  ["azure-sdk-for-java", "skill-eval", "9102", sources.sensei, sources.config],
  ["azure-sdk-for-js", "skill-eval", "9103", sources.release2, sources.release3],
  ["azure-sdk-for-net", "skill-eval", "9104", sources.markdown, sources.sensei],
  ["azure-rest-api-specs", "skill-eval", "9105", sources.release2, sources.release3],
  ["azure-rest-api-specs", "arm-api-review-eval", "9106", sources.markdown, sources.sensei],
  ["azure-sdk-for-python", "skill-eval", "9107", sources.authoring, sources.config],
];

async function createPipelineOutput(outputDirectory, resultsPath, label) {
  await mkdir(join(outputDirectory, "junit"), { recursive: true });
  await copyFile(resultsPath, join(outputDirectory, "results.jsonl"));
  await writeFile(join(outputDirectory, "eval-summary.md"), `# ${label}\n\nPOC pipeline summary.\n`);
  await writeFile(
    join(outputDirectory, "junit", "eval-results.junit.xml"),
    `<testsuites><testsuite name="${label}"><testcase name="dashboard bundle" /></testsuite></testsuites>\n`
  );
}

for (const resultSource of Object.values(sources)) {
  const sourcePath = join(toolsRoot, resultSource);
  try {
    await access(sourcePath);
  } catch (error) {
    throw new Error(
      `Missing sample result '${sourcePath}'. Set POC_VALLY_SOURCE_ROOT to an azure-sdk-tools checkout containing the sample results.`,
      { cause: error }
    );
  }
}

await rm(blobRoot, { recursive: true, force: true });
await rm(dataRoot, { recursive: true, force: true });

let buildId = 10000;
for (const [repo, pipeline, definitionId, ...resultSources] of pipelines) {
  for (const resultSource of resultSources) {
    buildId++;
    const runId = `${repo}-${pipeline}-${buildId}`;
    const outputDirectory = join(pipelineWorkspace, runId);
    const manifest = {
      schemaVersion: 1,
      adoProject: "internal",
      repo,
      pipeline,
      pipelineDefinitionId: definitionId,
      buildId: String(buildId),
      summaryAttempt: 1,
      runId,
      branch: "refs/heads/main",
      sourceVersion: String(buildId).padStart(40, "0"),
      buildUrl: `https://dev.azure.com/azure-sdk/internal/_build/results?buildId=${buildId}`,
      runTimestamp: new Date(Date.UTC(2026, 8, 1, buildId % 24)).toISOString(),
    };

    await createPipelineOutput(
      outputDirectory,
      join(toolsRoot, resultSource),
      `${repo} / ${pipeline} / build ${buildId}`
    );
    await publishPipelineBundle({ blobRoot, inputDirectory: outputDirectory, manifest });
    await rm(outputDirectory, { recursive: true, force: true });
  }
}

await rm(pipelineWorkspace, { recursive: true, force: true });
console.log(`Published ${buildId - 10000} immutable bundles to fake Blob root ${blobRoot}`);