import assert from "node:assert/strict";
import { test } from "node:test";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { submitDashboardBundle, waitForReceipt } from "../lib/dashboard-client.js";
import { extractDashboardBundle } from "../lib/dashboard-bundle.js";
import { pipelineManifest, preparePipelineBundle, selectShardAttempts } from "../lib/pipeline-bundle.js";
import { bundle, manifest, trial } from "./helpers/submission.mjs";

test("publisher retries exact ZIP bytes, respects Retry-After, and does not retry conflicts", async (t) => {
  const root = await mkdtemp(join(tmpdir(), "publisher-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const path = join(root, "bundle.zip"); await writeFile(path, bundle());
  const uploads = [], waits = [];
  const id = "a".repeat(64);
  const options = { bundlePath: path, dashboardUrl: "https://dashboard.example", wait: async (ms) => { waits.push(ms); },
    fetchImpl: async (_url, request) => {
      const chunks = []; for await (const chunk of request.body) chunks.push(chunk);
      uploads.push(Buffer.concat(chunks));
      if (uploads.length === 1) return new Response("{}", { status: 503, headers: { "retry-after": "7" } });
      return Response.json({ id, status: "queued", statusUrl: `/api/ingestions/${id}` }, { status: 202 });
    } };
  assert.equal((await submitDashboardBundle(options)).id, id);
  assert.equal(uploads.length, 2); assert.deepEqual(uploads[0], uploads[1]); assert.deepEqual(waits, [7000]);
  let conflicts = 0;
  await assert.rejects(submitDashboardBundle({ ...options, fetchImpl: async (_url, request) => {
    for await (const _chunk of request.body) { /* consume */ }
    conflicts++; return Response.json({ error: { code: "submission_conflict" } }, { status: 409 });
  } }), /409/);
  assert.equal(conflicts, 1);
  const receipt = { id, status: "queued", statusUrl: `/api/ingestions/${id}` };
  assert.equal((await waitForReceipt({ receipt, dashboardUrl: options.dashboardUrl, wait: async () => {},
    fetchImpl: async () => Response.json({ ...receipt, status: "succeeded" }) })).status, "succeeded");
  await assert.rejects(waitForReceipt({ receipt: { ...receipt, status: "failed" }, dashboardUrl: options.dashboardUrl }), /failed/);
});

test("highest shard attempt is selected; incomplete and duplicate inputs fail", () => {
  const source = { schemaVersion: 1, expectedShards: ["a", "b"], attempts: [
    { shard: "a", attempt: 1, directory: "a1" }, { shard: "a", attempt: 2, directory: "a2" }, { shard: "b", attempt: 1, directory: "b1" },
  ] };
  assert.deepEqual(selectShardAttempts(source).map((row) => row.directory), ["a2", "b1"]);
  assert.throws(() => selectShardAttempts({ ...source, expectedShards: ["a", "b", "c"] }), /Missing/);
  assert.throws(() => selectShardAttempts({ ...source, attempts: [...source.attempts, source.attempts[0]] }), /Duplicate/);
});

test("pipeline bundle preserves one selected stream per shard and rejects experiments", async (t) => {
  const root = await mkdtemp(join(tmpdir(), "merge-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  for (const name of ["a1", "a2", "b1"]) {
    await mkdir(join(root, name, "junit"), { recursive: true });
    await writeFile(join(root, name, "results.jsonl"), JSON.stringify({ ...trial, itemId: name }) + "\n");
    await writeFile(join(root, name, "junit/results.xml"), "<testsuites />");
  }
  const shardInput = { schemaVersion: 1, expectedShards: ["a", "b"], attempts: [
    { shard: "a", attempt: 1, directory: "a1" }, { shard: "a", attempt: 2, directory: "a2" }, { shard: "b", attempt: 1, directory: "b1" },
  ] };
  const options = { shardInput, resultsRoot: root, manifest, outputPath: join(root, "bundle.zip") };
  assert.equal((await preparePipelineBundle(options)).trials, 2);
  const extracted = join(root, "extracted"); await extractDashboardBundle(options.outputPath, extracted);
  const results = (await readFile(join(extracted, "results.jsonl"), "utf8")).trim().split("\n").map(JSON.parse);
  assert.deepEqual(results.map((row) => row.itemId), ["a2", "b1"]);
  await writeFile(join(root, "a2", "results.jsonl"), JSON.stringify({ ...trial, experiment: { runId: "not-supported" } }));
  await assert.rejects(preparePipelineBundle(options), /plain-eval/);
});

test("ADO manifest derives stable identity from standard build variables", () => {
  const env = { SYSTEM_COLLECTIONURI: "https://dev.azure.com/azure-sdk/", SYSTEM_TEAMPROJECT: "internal",
    BUILD_REPOSITORY_NAME: "azure-sdk-tools", BUILD_DEFINITIONNAME: "skill-eval", SYSTEM_DEFINITIONID: "8178",
    BUILD_BUILDID: "12345", SYSTEM_JOBATTEMPT: "2", BUILD_SOURCEBRANCH: "refs/heads/main", BUILD_SOURCEVERSION: "a".repeat(40) };
  const result = pipelineManifest(env);
  assert.equal(result.adoOrganization, "azure-sdk"); assert.equal(result.summaryAttempt, 2);
  assert.throws(() => pipelineManifest({ ...env, SYSTEM_COLLECTIONURI: "https://unknown.example/azure-sdk/" }));
});