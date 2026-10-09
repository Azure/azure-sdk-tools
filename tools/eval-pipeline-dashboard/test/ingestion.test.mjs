import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { Hono } from "hono";
import { serve } from "@hono/node-server";
import { strToU8, zipSync } from "fflate";
import { createLocalJWKSet, exportJWK, generateKeyPair, SignJWT } from "jose";
import { initializeDatabase, DatabaseStore, createApp } from "@microsoft/vally-server";
import { openIngestionJournal } from "../lib/ingestion-journal.js";
import { createIngestionService } from "../lib/ingestion-service.js";
import { mountIngestionRoutes } from "../lib/ingestion-routes.js";
import { createPublisherAuthorizer, hostGuard } from "../lib/publisher-auth.js";
import { createSubmissionStore } from "../lib/submission-store.js";
import { initializeRunMetadata, getPipelineRuns, getPipelineSummaries } from "../lib/run-metadata.js";
import { submissionId, validateSubmissionManifest } from "../lib/submission-manifest.js";
import { mountPipelines } from "../pipelines.js";
import { submitDashboardBundle } from "../lib/dashboard-client.js";

const manifest = {
  schemaVersion: 1,
  adoOrganization: "azure-sdk",
  adoProject: "internal",
  repo: "azure-sdk-for-go",
  pipeline: "new-pipeline",
  pipelineDefinitionId: "9321",
  buildId: "1001",
  summaryAttempt: 1,
  branch: "refs/heads/main",
  runTimestamp: "2026-09-14T00:00:00Z",
};
const trial = {
  type: "trial-result", itemId: "trial-1", evalName: "sample",
  evalFilePath: "sample.eval.yaml", variant: "default", stimulus: "sample stimulus",
  model: "sample-model", status: "error", durationMs: 1,
  error: "Synthetic execution error", trajectory: null, gradeResult: null,
};

function bundle(overrides = {}, entries = {}) {
  return zipSync({
    "manifest.json": strToU8(JSON.stringify({ ...manifest, ...overrides })),
    "results.jsonl": strToU8(JSON.stringify(trial) + "\n"),
    "eval-summary.md": strToU8("# Sample\n"),
    "junit/result.junit.xml": strToU8('<testsuites><testsuite name="sample" /></testsuites>'),
    ...entries,
  }, { level: 6, mtime: new Date("2020-01-01T00:00:00Z") });
}

async function harness(t, options = {}) {
  const root = await mkdtemp(join(tmpdir(), "push-ingestion-test-"));
  const h = { root, store: createSubmissionStore(join(root, "archive")), now: Date.now() };
  const authorize = options.authorize ?? createPublisherAuthorizer({ anonymousLocal: true });
  const open = () => {
    h.db = initializeDatabase(join(root, "eval.db"));
    initializeRunMetadata(h.db);
    h.journal = openIngestionJournal(join(root, "ingestions.db"), { now: () => h.now });
    h.service = createIngestionService({
      db: h.db, journal: h.journal, store: h.store, stagingRoot: join(root, "staging"),
      retryDelays: [100, 200], log: () => {}, ...options.service,
    });
    h.app = new Hono();
    h.app.use("*", hostGuard());
    mountIngestionRoutes(h.app, {
      service: h.service, authorize, stagingRoot: join(root, "staging"), ...options.routes,
    });
    const vally = createApp(new DatabaseStore(h.db));
    mountPipelines(h.app, vally, h.db);
    h.app.route("/", vally);
  };
  open();
  h.request = (path, init = {}) => h.app.request(`http://localhost${path}`, {
    ...init, headers: { host: "localhost", ...init.headers },
  });
  h.post = (bytes, headers = {}) => h.request("/api/ingestions", {
    method: "POST", headers: { "content-type": "application/zip", ...headers }, body: bytes,
  });
  h.receipt = async (id, headers) => (await h.request(`/api/ingestions/${id}`, { headers })).json();
  h.restart = async () => {
    await h.service.stop(); h.journal.close(); h.db.close(); open();
  };
  t.after(async () => {
    await h.service.stop(); h.journal.close(); h.db.close();
    await rm(root, { recursive: true, force: true });
  });
  return h;
}

test("202 persists a bundle and queued receipt; the worker adds a new pipeline without scanning", async (t) => {
  const h = await harness(t);
  const zip = bundle();
  const response = await h.post(zip);
  assert.equal(response.status, 202);
  const accepted = await response.json();
  assert.equal(accepted.status, "queued");
  assert.equal(response.headers.get("location"), accepted.statusUrl);
  const saved = h.journal.get(accepted.id);
  assert.deepEqual(new Uint8Array(await readFile(join(h.root, "archive", saved.blob_name))), zip);
  assert.equal(getPipelineSummaries(h.db).length, 0);
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).status, "succeeded");
  assert.deepEqual(getPipelineSummaries(h.db).map(({ repository, pipeline, run_count }) => ({ repository, pipeline, run_count })),
    [{ repository: manifest.repo, pipeline: manifest.pipeline, run_count: 1 }]);
  assert.deepEqual(await readdir(join(h.root, "staging")), []);
  const duplicate = await h.post(zip);
  assert.equal(duplicate.status, 200);
  assert.equal((await duplicate.json()).id, accepted.id);
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
  const index = await h.request("/");
  assert.equal(index.status, 200);
  assert.ok((await index.text()).includes(manifest.repo));
  const detail = await h.request(`/p/${manifest.repo}/${manifest.pipeline}`);
  assert.equal(detail.status, 200);
  assert.ok((await detail.text()).includes("durationChart"));
});

test("concurrent duplicate uploads share one receipt and different content conflicts", async (t) => {
  const h = await harness(t);
  const zip = bundle();
  const responses = await Promise.all([h.post(zip), h.post(zip)]);
  assert.deepEqual(responses.map((response) => response.status), [202, 202]);
  const receipts = await Promise.all(responses.map((response) => response.json()));
  assert.equal(receipts[0].id, receipts[1].id);
  assert.deepEqual(h.journal.counts(), { queued: 1 });
  const conflict = await h.post(bundle({}, { "eval-summary.md": strToU8("Changed content") }));
  assert.equal(conflict.status, 409);
  await h.service.start();
  assert.equal(getPipelineRuns(h.db, manifest.repo, manifest.pipeline).length, 1);
});

test("an accepted and interrupted receipt resumes after process restart", async (t) => {
  const h = await harness(t);
  const accepted = await (await h.post(bundle())).json();
  assert.equal(h.journal.claim().status, "processing");
  await h.restart();
  assert.equal((await h.receipt(accepted.id)).status, "processing");
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).status, "succeeded");
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 1);
});

test("receipt persistence failure never returns 202 and an exact retry repairs acceptance", async (t) => {
  const h = await harness(t);
  const insert = t.mock.method(h.journal, "insert", () => { throw new Error("simulated disk failure"); });
  const zip = bundle();
  const failed = await h.post(zip);
  assert.equal(failed.status, 503);
  assert.deepEqual(h.journal.counts(), {});
  insert.mock.restore();
  const accepted = await h.post(zip);
  assert.equal(accepted.status, 202);
  await h.service.start();
  assert.equal((await h.receipt((await accepted.json()).id)).status, "succeeded");
});

test("a failure after the result commit is retried without duplicating outcomes", async (t) => {
  const h = await harness(t);
  const original = h.journal.succeed;
  let failOnce = true;
  t.mock.method(h.journal, "succeed", (...args) => {
    if (failOnce) { failOnce = false; throw new Error("receipt write interrupted"); }
    return original(...args);
  });
  const accepted = await (await h.post(bundle())).json();
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).status, "queued");
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
  h.now += 101;
  await h.service.flush();
  assert.equal((await h.receipt(accepted.id)).status, "succeeded");
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
});

test("malformed result data fails in isolation and does not block the next submission", async (t) => {
  const h = await harness(t);
  const bad = await (await h.post(bundle({}, { "results.jsonl": strToU8(JSON.stringify(trial) + "\nnot-json\n") }))).json();
  const good = await (await h.post(bundle({ buildId: "1002" }))).json();
  await h.service.start();
  assert.equal((await h.receipt(bad.id)).status, "failed");
  assert.equal((await h.receipt(bad.id)).errorCode, "invalid_results");
  assert.equal((await h.receipt(good.id)).status, "succeeded");
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 1);
  assert.deepEqual(await readdir(join(h.root, "staging")), []);
});

test("Vally dropping an invalid grader record does not publish partial results", async (t) => {
  const h = await harness(t);
  const badTrial = { ...trial, status: "success", gradeResult: { passed: true } };
  const accepted = await (await h.post(bundle({}, { "results.jsonl": strToU8(JSON.stringify(badTrial)) }))).json();
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).status, "failed");
  assert.equal(getPipelineSummaries(h.db).length, 0);
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 0);
});

test("a completed newer attempt supersedes the older one without discarding its receipt", async (t) => {
  const h = await harness(t);
  const first = await (await h.post(bundle())).json();
  await h.service.start();
  const second = await (await h.post(bundle({ summaryAttempt: 2 }))).json();
  await h.service.flush();
  assert.notEqual(first.id, second.id);
  assert.equal((await h.receipt(first.id)).status, "succeeded");
  assert.equal((await h.receipt(second.id)).status, "succeeded");
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 2);
  const visible = getPipelineRuns(h.db, manifest.repo, manifest.pipeline);
  assert.equal(visible.length, 1);
  assert.equal(visible[0].id, `ingestion-${second.id}`);
  const invalid = await (await h.post(bundle({ summaryAttempt: 3 }, { "results.jsonl": strToU8("not-json") }))).json();
  await h.service.flush();
  assert.equal((await h.receipt(invalid.id)).status, "failed");
  assert.equal(getPipelineRuns(h.db, manifest.repo, manifest.pipeline)[0].id, visible[0].id);
});

test("stable IDs ignore display names but separate organizations, definitions, builds, and attempts", () => {
  const id = (overrides) => submissionId(validateSubmissionManifest({ ...manifest, ...overrides }));
  assert.equal(id({}), id({ pipeline: "renamed", repo: "Azure/renamed-repo" }));
  assert.equal(new Set([id({}), id({ adoOrganization: "another-org" }), id({ pipelineDefinitionId: "9322" }), id({ buildId: "1002" }), id({ summaryAttempt: 2 })]).size, 5);
});

test("uploads reject invalid manifests, corrupt ZIPs, traversal, case-duplicate entries, and mismatched checksums", async (t) => {
  const h = await harness(t);
  for (const zip of [
    bundle({ schemaVersion: 9 }), bundle({ summaryAttempt: 0 }), bundle({ pipelineDefinitionId: "../evil" }),
    bundle({ adoOrganization: undefined }), bundle({ runTimestamp: "bad" }), strToU8("not a zip"),
    bundle({}, { "../escape": strToU8("bad") }),
    bundle({}, { "junit/RESULT.junit.xml": strToU8("duplicate") }),
    bundle({}, { "junit/AUX.xml": strToU8("device alias") }),
  ]) {
    assert.equal((await h.post(zip)).status, 422);
  }
  assert.equal((await h.post(bundle(), { "x-content-sha256": "0".repeat(64) })).status, 400);
  assert.deepEqual(h.journal.counts(), {});
  assert.deepEqual(await readdir(join(h.root, "staging")), []);
});

test("size limits are enforced for declared and undeclared body length", async (t) => {
  const h = await harness(t, { routes: { maxBytes: 64 } });
  assert.equal((await h.post(bundle(), { "content-length": "1000000" })).status, 413);
  assert.equal((await h.post(bundle())).status, 413);
  assert.equal((await h.post(bundle(), { "content-type": "text/plain" })).status, 415);
  assert.deepEqual(h.journal.counts(), {});
});

test("local ingestion rejects browser-origin requests and unsafe host configuration", async (t) => {
  assert.throws(() => createPublisherAuthorizer({ anonymousLocal: true, bindHost: "0.0.0.0" }), /loopback/);
  assert.throws(() => createPublisherAuthorizer(), /Configure/);
  const h = await harness(t);
  assert.equal((await h.post(bundle(), { origin: "https://attacker.example" })).status, 403);
  assert.equal((await h.post(bundle(), { host: "attacker.example" })).status, 403);
  assert.equal((await h.post(bundle(), { "sec-fetch-site": "cross-site" })).status, 403);
});

test("dashboard viewers need no login when publisher authentication is enabled", async (t) => {
  const authorize = createPublisherAuthorizer({
    tenantId: "00000000-0000-4000-8000-000000000001",
    audience: "dashboard-api",
    clientIds: ["publisher-app"],
    jwks: () => { throw new Error("Requests without a token must not resolve signing keys."); },
  });
  const h = await harness(t, { authorize });
  const browserHeaders = { origin: "http://localhost", "sec-fetch-site": "same-origin" };
  for (const path of ["/", "/all", "/api/runs", `/api/dashboard/pipelines/${manifest.repo}/${manifest.pipeline}/runs`]) {
    const response = await h.request(path, { headers: browserHeaders });
    assert.equal(response.status, 200, path);
    assert.equal(response.headers.get("location"), null, path);
    assert.equal(response.headers.get("www-authenticate"), null, path);
  }
  assert.equal((await h.post(bundle())).status, 401);
  assert.equal((await h.request(`/api/ingestions/${"0".repeat(64)}`)).status, 401);
  assert.deepEqual(h.journal.counts(), {});
});

test("receipts are visible only to the accepting publisher", async (t) => {
  const h = await harness(t, { authorize: async (context) => ({ id: context.req.header("x-test-publisher") || "one" }) });
  const accepted = await (await h.post(bundle())).json();
  assert.equal((await h.request(accepted.statusUrl, { headers: { "x-test-publisher": "two" } })).status, 404);
  assert.equal((await h.post(bundle(), { "x-test-publisher": "two" })).status, 409);
});

test("Entra authorization verifies signatures, issuer, audience, expiry, app ID and role", async (t) => {
  const tenantId = "00000000-0000-4000-8000-000000000001";
  const audience = "dashboard-api";
  const clientId = "publisher-app";
  const { privateKey, publicKey } = await generateKeyPair("RS256");
  const key = { ...await exportJWK(publicKey), kid: "test-key", alg: "RS256", use: "sig" };
  const authorize = createPublisherAuthorizer({ tenantId, audience, clientIds: [clientId], jwks: createLocalJWKSet({ keys: [key] }) });
  const sign = (overrides = {}, { issuer = `https://login.microsoftonline.com/${tenantId}/v2.0`, aud = audience, expired = false } = {}) =>
    new SignJWT({ tid: tenantId, azp: clientId, roles: ["Dashboard.Ingest"], ...overrides })
      .setProtectedHeader({ alg: "RS256", kid: "test-key" }).setIssuedAt().setIssuer(issuer).setAudience(aud)
      .setExpirationTime(expired ? 1 : Math.floor(Date.now() / 1000) + 300).sign(privateKey);
  const h = await harness(t, { authorize });
  assert.equal((await h.post(bundle())).status, 401);
  for (const token of [await sign({}, { aud: "wrong" }), await sign({}, { issuer: "https://wrong.example" }), await sign({}, { expired: true }), "invalid.jwt.token"]) {
    assert.equal((await h.post(bundle(), { authorization: `Bearer ${token}` })).status, 401);
  }
  for (const token of [await sign({ roles: [] }), await sign({ azp: "other-app" }), await sign({ scp: "delegated" })]) {
    assert.equal((await h.post(bundle(), { authorization: `Bearer ${token}` })).status, 403);
  }
  assert.equal((await h.post(bundle(), { authorization: `Bearer ${await sign()}` })).status, 202);
});

test("the HTTP client submits saved ZIP bytes and safely retries the same upload", async (t) => {
  const h = await harness(t);
  const zipPath = join(h.root, "submitted.zip");
  await writeFile(zipPath, bundle());
  const server = serve({ fetch: h.app.fetch, hostname: "127.0.0.1", port: 0 });
  await new Promise((resolveListening, reject) => {
    if (server.listening) resolveListening();
    else server.once("listening", resolveListening);
    server.once("error", reject);
  });
  try {
    const dashboardUrl = `http://127.0.0.1:${server.address().port}`;
    const receipt = await submitDashboardBundle({ bundlePath: zipPath, dashboardUrl });
    assert.equal(receipt.status, "queued");
    await h.service.start();
    const status = await (await fetch(new URL(receipt.statusUrl, dashboardUrl))).json();
    assert.equal(status.status, "succeeded");
    const duplicate = await submitDashboardBundle({ bundlePath: zipPath, dashboardUrl });
    assert.equal(duplicate.id, receipt.id);
    assert.equal(duplicate.duplicate, true);
    await assert.rejects(submitDashboardBundle({ bundlePath: zipPath, dashboardUrl: "http://untrusted.example" }), /HTTPS/);
  } finally {
    await new Promise((resolveClose) => server.close(resolveClose));
  }
});

test("declared expanded ZIP sizes are checked before decompression", async (t) => {
  const h = await harness(t);
  const zip = Buffer.from(bundle());
  const directory = zip.indexOf(Buffer.from([0x50, 0x4b, 0x01, 0x02]));
  assert.ok(directory > 0);
  zip.writeUInt32LE(128 * 1024 * 1024 + 1, directory + 24);
  assert.equal((await h.post(zip)).status, 422);
  assert.deepEqual(h.journal.counts(), {});
});

test("concurrent upload backpressure releases slots after acceptance", { timeout: 5000 }, async (t) => {
  const h = await harness(t);
  const entered = Promise.withResolvers();
  const release = Promise.withResolvers();
  const accept = h.service.accept;
  let arrivals = 0;
  t.mock.method(h.service, "accept", async (input) => {
    if (++arrivals === 2) entered.resolve();
    await release.promise;
    return accept(input);
  });
  const uploads = Promise.all([h.post(bundle({ buildId: "2001" })), h.post(bundle({ buildId: "2002" }))]);
  try {
    await entered.promise;
    const busy = await h.post(bundle({ buildId: "2003" }));
    assert.equal(busy.status, 429);
    assert.equal(busy.headers.get("retry-after"), "5");
    assert.deepEqual(h.journal.counts(), {});
  } finally {
    release.resolve();
  }
  assert.deepEqual((await uploads).map((response) => response.status), [202, 202]);
  assert.equal((await h.post(bundle({ buildId: "2003" }))).status, 202);
  assert.deepEqual(h.journal.counts(), { queued: 3 });
  assert.deepEqual(await readdir(join(h.root, "staging")), []);
});

test("transient worker retries stop at the configured attempt limit", async (t) => {
  const h = await harness(t, { service: {
    processSubmission: async () => { throw new Error("simulated transient failure"); },
  } });
  const accepted = await (await h.post(bundle())).json();
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).attempts, 1);
  for (const delay of [100, 200]) {
    assert.equal((await h.receipt(accepted.id)).status, "queued");
    h.now += delay;
    await h.service.flush();
  }
  assert.equal((await h.receipt(accepted.id)).status, "failed");
  h.now += 100000;
  await h.service.flush();
  assert.equal((await h.receipt(accepted.id)).attempts, 3);
  assert.deepEqual(h.journal.counts(), { failed: 1 });
});

test("unsupported experiment records fail without publishing a partial run", async (t) => {
  const h = await harness(t);
  const record = { ...trial, experiment: { runId: "experiment-1", variant: "first" } };
  const accepted = await (await h.post(bundle({}, { "results.jsonl": strToU8(JSON.stringify(record)) }))).json();
  await h.service.start();
  assert.equal((await h.receipt(accepted.id)).status, "failed");
  assert.equal((await h.receipt(accepted.id)).errorCode, "invalid_results");
  assert.equal(getPipelineSummaries(h.db).length, 0);
  assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 0);
});