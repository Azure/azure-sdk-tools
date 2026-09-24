import assert from "node:assert/strict";
import { test } from "node:test";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { initializeDatabase } from "@microsoft/vally-server";
import { Hono } from "hono";
import { lockCache } from "../lib/cache-lock.js";
import { openIngestionJournal } from "../lib/ingestion-journal.js";
import { createSubmissionStore, fileHash } from "../lib/submission-store.js";
import { createIngestionService } from "../lib/ingestion-service.js";
import { initializeRunMetadata } from "../lib/run-metadata.js";
import { replayReceipts, pruneReceipts, reconcilePending, retentionCutoff } from "../lib/maintenance.js";
import { mountOperationalRoutes } from "../lib/operational-routes.js";
import { readDeploymentTarget, validateDeploymentTarget } from "../lib/deployment-check.js";
import { bundle } from "./helpers/submission.mjs";

test("exclusive cache lock prevents a second writer or maintenance process", async (t) => {
  const root = await mkdtemp(join(tmpdir(), "cache-lock-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const path = join(root, "eval.db");
  const release = await lockCache(path);
  try { await assert.rejects(lockCache(path), /in use/); } finally { await release(); }
  await (await lockCache(path))();
});

test("local replay/retention preserve receipts, honor cutoff, and never expire pending work", async (t) => {
  const root = await mkdtemp(join(tmpdir(), "maintenance-test-"));
  let now = Date.parse("2024-09-13T00:00:00Z");
  const journal = openIngestionJournal(join(root, "ingestions.db"), { now: () => now });
  const db = initializeDatabase(join(root, "eval.db")); initializeRunMetadata(db);
  const store = createSubmissionStore(join(root, "archive"));
  const service = createIngestionService({ db, journal, store, stagingRoot: join(root, "staging"), log: () => {} });
  const accept = async (buildId) => {
    const path = join(root, `${buildId}.zip`); await writeFile(path, bundle({ buildId }));
    return service.accept({ bundlePath: path, contentHash: await fileHash(path), publisher: { id: "test" } });
  };
  t.after(async () => { await service.stop(); journal.close(); db.close(); await rm(root, { recursive: true, force: true }); });
  const old = await accept("1001");
  now = Date.parse("2026-09-14T00:00:00Z");
  await accept("1002"); await service.start(); await service.stop();
  const source = journal.get(old.receipt.id);
  // A receipt accepted before the cutoff but never processed must be retained.
  const currentTime = now;
  now = Date.parse("2024-09-01T00:00:00Z");
  const pending = journal.insert({ id: "f".repeat(64), contentHash: "c".repeat(64), publisherId: "test", manifest: {},
    blob: { name: "pending.zip", size: 1 } });
  now = currentTime;
  const replacement = initializeDatabase(join(root, "replacement.db")); initializeRunMetadata(replacement);
  try {
    const result = await replayReceipts({ db: replacement, journal, store, stagingRoot: join(root, "replay") });
    assert.equal(result.restored, 2); assert.deepEqual(result.failures, []);
    const cutoff = retentionCutoff(731, now);
    assert.equal(cutoff, "2024-09-13T00:00:00.000Z");
    assert.equal((await pruneReceipts({ db: replacement, journal, store, cutoff })).candidates, 0);
    const after = "2024-09-14T00:00:00.000Z";
    const dry = await pruneReceipts({ db: replacement, journal, store, cutoff: after });
    assert.equal(dry.candidates, 1); assert.equal(journal.get(source.id).expired_at, null);
    const removed = await pruneReceipts({ db: replacement, journal, store, cutoff: after, apply: true });
    assert.equal(removed.expired, 1); assert.deepEqual(removed.failures, []);
    assert.equal(replacement.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 1);
    assert.ok(journal.get(source.id).expired_at);
    assert.equal(journal.get(pending.id).status, "queued");
    assert.equal(journal.get(pending.id).expired_at, null);
    assert.equal((await replayReceipts({ db: replacement, journal, store, stagingRoot: root })).restored, 1);
    assert.equal((await reconcilePending({ journal })).pending, 1);
  } finally { replacement.close(); }
  assert.throws(() => retentionCutoff(0));
});

test("operational endpoints return useful status without leaking storage exceptions", async () => {
  const app = new Hono();
  let fail = false;
  mountOperationalRoutes(app, { provider: "azure", service: {
    async checkReady() { if (fail) throw new Error("sensitive storage URL"); },
    async status() { return { accepted: 2, queue: { pendingApproximate: 1 } }; },
  } });
  assert.equal((await app.request("/api/readiness")).status, 200);
  const response = await app.request("/api/dashboard/status");
  assert.equal((await response.json()).queue.pendingApproximate, 1);
  fail = true;
  const unavailable = await app.request("/api/readiness");
  assert.equal(unavailable.status, 503);
  assert.ok(!(await unavailable.text()).includes("sensitive"));
});

test("deployment validation fails closed before publishing to a public or login-gated app", () => {
  const site = { name: "test-app", identity: { type: "SystemAssigned" }, properties: { publicNetworkAccess: "Disabled", httpsOnly: true } };
  const auth = { properties: { platform: { enabled: false } } };
  const endpoints = { value: [{ properties: { privateLinkServiceConnectionState: { status: "Approved" } } }] };
  assert.equal(validateDeploymentTarget(site, auth, endpoints).viewerLogin, false);
  assert.throws(() => validateDeploymentTarget({ ...site, properties: {} }, auth, endpoints), /public/);
  assert.throws(() => validateDeploymentTarget(site, { properties: { platform: { enabled: true } } }, endpoints), /Easy Auth/);
  assert.throws(() => validateDeploymentTarget(site, {}, endpoints), /Easy Auth/);
  assert.throws(() => validateDeploymentTarget({ ...site, properties: { ...site.properties, httpsOnly: false } }, auth, endpoints), /HTTPS/);
  assert.throws(() => validateDeploymentTarget(site, auth, { value: [] }), /private endpoint/);
  assert.throws(() => validateDeploymentTarget({ ...site, identity: null }, auth, endpoints), /managed identity/);
});

test("deployment preflight uses the documented read-only ARM method for each configuration endpoint", async () => {
  const calls = [];
  await readDeploymentTarget({ subscription: "subscription", resourceGroup: "group", app: "dashboard", token: "synthetic-test-token",
    fetchImpl: async (url, options) => {
      calls.push({ path: new URL(url).pathname, method: options.method });
      return Response.json({});
    } });
  assert.equal(calls.find((call) => call.path.endsWith("/authsettingsV2/list")).method, "GET");
  assert.equal(calls.find((call) => call.path.endsWith("/appsettings/list")).method, "POST");
  assert.equal(calls.find((call) => call.path.endsWith("/privateEndpointConnections")).method, "GET");
  assert.equal(calls.length, 4);
});