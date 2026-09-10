import assert from "node:assert/strict";
import { appendFile, mkdir, readdir, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { test } from "node:test";
import { initializeDatabase } from "@microsoft/vally-server";
import { syncArtifactRuns } from "../lib/artifact-sync.js";
import { createDashboardBundle } from "../lib/dashboard-bundle.js";
import { getLocalBlobPath, publishLocalBlob } from "../lib/local-blob-store.js";
import { parsePocPipelineConfig } from "../lib/poc-pipeline-config.js";
import {
  getPipelineRuns,
  getPipelineSummaries,
  initializeRunMetadata,
} from "../lib/run-metadata.js";

test("loads pipeline definitions from configuration", () => {
  assert.deepEqual(
    parsePocPipelineConfig({
      adoProject: "internal",
      sources: { go: "artifacts/go/results.jsonl" },
      pipelines: [{
        repository: "azure-sdk-for-go",
        pipeline: "language-eval",
        pipelineDefinitionId: "9321",
        resultSources: ["go"],
      }],
    }),
    {
      pipelines: [{
        adoProject: "internal",
        repository: "azure-sdk-for-go",
        pipeline: "language-eval",
        pipelineDefinitionId: "9321",
        resultSources: ["artifacts/go/results.jsonl"],
      }],
    }
  );
});

test("downloads and ingests each immutable bundle once", async (context) => {
  const root = await context.mock.method(process, "cwd")() || process.cwd();
  const temporaryRoot = join(root, `.test-blob-sync-${process.pid}-${Date.now()}`);
  const inputDirectory = join(temporaryRoot, "input");
  const blobRoot = join(temporaryRoot, "blob");
  const stagingRoot = join(temporaryRoot, "staging");
  const bundlePath = join(temporaryRoot, "bundle.zip");
  const databasePath = join(temporaryRoot, "eval.db");
  const manifest = {
    schemaVersion: 1,
    adoProject: "internal",
    repo: "azure-sdk-for-go",
    pipeline: "language-eval",
    pipelineDefinitionId: "9321",
    buildId: "1001",
    summaryAttempt: 1,
    runId: "test-run-1001",
    branch: "refs/heads/main",
  };

  await mkdir(join(inputDirectory, "junit"), { recursive: true });
  await writeFile(
    join(inputDirectory, "results.jsonl"),
    `${JSON.stringify({
      type: "trial-result",
      itemId: "item-1",
      evalName: "test-eval",
      evalFilePath: "test.eval.yaml",
      variant: "default",
      stimulus: "test-stimulus",
      status: "error",
      durationMs: 10,
      error: "synthetic test error",
      gradeResult: null,
      trajectory: null,
    })}\n`,
    "utf8"
  );
  await writeFile(join(inputDirectory, "eval-summary.md"), "# Test summary\n", "utf8");
  await writeFile(
    join(inputDirectory, "junit", "test.junit.xml"),
    '<testsuites><testsuite name="test"><testcase name="test-stimulus" /></testsuite></testsuites>\n',
    "utf8"
  );

  let db;
  try {
    await createDashboardBundle({ inputDirectory, outputPath: bundlePath, manifest });
    await publishLocalBlob({ blobRoot, bundlePath, manifest });
    db = initializeDatabase(databasePath);
    initializeRunMetadata(db);

    assert.deepEqual(await syncArtifactRuns({ db, blobRoot, stagingRoot }), {
      listed: 1,
      downloaded: 1,
      ingested: 1,
      skipped: 0,
    });
    assert.deepEqual(await syncArtifactRuns({ db, blobRoot, stagingRoot }), {
      listed: 1,
      downloaded: 0,
      ingested: 0,
      skipped: 1,
    });
    assert.equal(db.prepare("SELECT COUNT(*) AS count FROM runs").get().count, 1);
    assert.equal(db.prepare("SELECT COUNT(*) AS count FROM run_metadata").get().count, 1);
    assert.deepEqual(
      getPipelineSummaries(db).map(({ repository, pipeline, run_count }) => ({
        repository,
        pipeline,
        run_count,
      })),
      [{ repository: "azure-sdk-for-go", pipeline: "language-eval", run_count: 1 }]
    );
    assert.equal(getPipelineRuns(db, "azure-sdk-for-go", "language-eval").length, 1);
    const metadata = db
      .prepare("SELECT blob_name, blob_etag FROM run_metadata")
      .get();
    assert.match(metadata.blob_name, /dashboard-bundle\.zip$/);
    assert.match(metadata.blob_etag, /^[a-f0-9]{64}$/);
    assert.deepEqual(await readdir(stagingRoot), []);

    await appendFile(getLocalBlobPath(blobRoot, manifest), new Uint8Array([0]));
    await assert.rejects(
      syncArtifactRuns({ db, blobRoot, stagingRoot }),
      /Immutable Blob .* changed from ETag/
    );
    assert.deepEqual(await readdir(stagingRoot), []);
  } finally {
    db?.close();
    await rm(temporaryRoot, { force: true, recursive: true });
  }
});