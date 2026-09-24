import assert from "node:assert/strict";
import { test } from "node:test";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { randomUUID } from "node:crypto";
import { ContainerClient, StorageSharedKeyCredential } from "@azure/storage-blob";
import { QueueClient, StorageSharedKeyCredential as QueueCredential } from "@azure/storage-queue";
import { Hono } from "hono";
import { initializeDatabase } from "@microsoft/vally-server";
import { createAzureBlobStore } from "../lib/azure-blob-store.js";
import { createAzureIngestionJournal } from "../lib/azure-ingestion-journal.js";
import { createIngestionService } from "../lib/ingestion-service.js";
import { mountIngestionRoutes } from "../lib/ingestion-routes.js";
import { initializeRunMetadata } from "../lib/run-metadata.js";
import { readStorageConfig, storageUrl } from "../lib/storage-config.js";
import { replayReceipts, pruneReceipts } from "../lib/maintenance.js";
import { bundle } from "./helpers/submission.mjs";
import { startAzurite } from "./helpers/azurite.mjs";

test("Azure configuration is explicit and never accepts SAS credentials or implicit local fallback", () => {
  assert.equal(readStorageConfig({}, process.cwd()).provider, "local");
  assert.throws(() => readStorageConfig({ VALLY_STORAGE_PROVIDER: "azure" }, process.cwd()), /URL/);
  for (const value of ["http://account.blob.core.windows.net/data", "https://account.blob.core.windows.net/", "https://account.blob.core.windows.net/data?sig=secret", "https://user:secret@account.blob.core.windows.net/data", "https://account.blob.core.windows.net/data/path"]) {
    assert.throws(() => storageUrl(value, "test"));
  }
  assert.equal(storageUrl("https://account.blob.core.windows.net/data/", "test"), "https://account.blob.core.windows.net/data");
});

test("Azure Blob and Queue adapters against a loopback Azurite instance", { timeout: 180_000 }, async (t) => {
  const emulator = await startAzurite(t);
  async function harness(child) {
    const name = `test-${randomUUID()}`;
    const options = { retryOptions: { maxTries: 1 }, allowInsecureConnection: true };
    const container = new ContainerClient(`${emulator.blob}/${emulator.account}/${name}`,
      new StorageSharedKeyCredential(emulator.account, emulator.key), options);
    const queue = new QueueClient(`${emulator.queue}/${emulator.account}/${name}`,
      new QueueCredential(emulator.account, emulator.key), options);
    await container.create(); await queue.create();
    const root = await mkdtemp(join(tmpdir(), "cloud-ingestion-test-"));
    const db = initializeDatabase(join(root, "eval.db")); initializeRunMetadata(db);
    const store = createAzureBlobStore(container);
    const openJournal = () => createAzureIngestionJournal({ containerClient: container, queueClient: queue, log: () => {} });
    const h = { container, queue, root, db, store, journal: openJournal(), openJournal };
    h.service = createIngestionService({ db, store, journal: h.journal, stagingRoot: join(root, "staging"), log: () => {} });
    const app = new Hono();
    mountIngestionRoutes(app, { service: h.service, stagingRoot: join(root, "staging"), authorize: async () => ({ id: "publisher" }) });
    h.post = (bytes = bundle()) => app.request("http://localhost/api/ingestions", { method: "POST", headers: { "content-type": "application/zip" }, body: bytes });
    child.after(async () => { await h.service.stop(); await h.journal.close(); db.close(); await container.delete(); await queue.delete(); await rm(root, { recursive: true, force: true }); });
    return h;
  }
  await t.test("202 survives a new adapter instance; hashes and ETags remain distinct; no listing on normal work", async (child) => {
    const h = await harness(child);
    child.mock.method(h.container, "listBlobsFlat", () => { throw new Error("Normal operation must not enumerate blobs."); });
    const accepted = await (await h.post()).json();
    const persisted = await h.openJournal().get(accepted.id);
    assert.equal(persisted.status, "queued");
    assert.notEqual(persisted.blob_etag, persisted.content_hash);
    await h.service.start();
    const receipt = await h.service.get(accepted.id, { id: "publisher" });
    assert.equal(receipt.status, "succeeded");
    const metadata = h.db.prepare("SELECT blob_etag, blob_sha256 FROM run_metadata").get();
    assert.equal(metadata.blob_sha256, persisted.content_hash);
    assert.equal(metadata.blob_etag, persisted.blob_etag);
    assert.equal((await h.post()).status, 200);
    assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
  });
  await t.test("queue send failure returns 503 and exact-byte retry repairs saved receipt", async (child) => {
    const h = await harness(child);
    const send = child.mock.method(h.queue, "sendMessage", async () => { throw new Error("queue unavailable"); });
    const failed = await h.post(); assert.equal(failed.status, 503);
    const rows = []; for await (const row of h.journal.list()) rows.push(row);
    assert.equal(rows.length, 1); assert.equal(rows[0].status, "queued");
    send.mock.restore();
    const retry = await h.post(); assert.equal(retry.status, 202);
    assert.equal((await retry.json()).id, rows[0].id);
    await h.service.start();
    assert.equal((await h.journal.get(rows[0].id)).status, "succeeded");
  });
  await t.test("processing recovers through queue redelivery after a worker restart", async (child) => {
    const h = await harness(child);
    const accepted = await (await h.post()).json();
    const receive = h.queue.receiveMessages.bind(h.queue);
    let message;
    child.mock.method(h.queue, "receiveMessages", async (...args) => {
      const response = await receive(...args); message = response.receivedMessageItems[0] ?? message; return response;
    });
    assert.equal((await h.journal.claim()).status, "processing");
    await h.service.stop(); await h.journal.close();
    // Make the previously invisible message available without waiting for timeouts.
    await h.queue.updateMessage(message.messageId, message.popReceipt, undefined, 0);
    h.journal = h.openJournal();
    h.service = createIngestionService({ db: h.db, store: h.store, journal: h.journal,
      stagingRoot: join(h.root, "staging"), log: () => {} });
    await h.service.start();
    assert.equal((await h.journal.get(accepted.id)).status, "succeeded");
    assert.equal((await h.journal.get(accepted.id)).attempts, 2);
    assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
  });
  await t.test("immutable archive conflicts reject changed bytes", async (child) => {
    const h = await harness(child);
    const accepted = await (await h.post()).json();
    const row = await h.journal.get(accepted.id);
    const changed = join(h.root, "changed.zip"); await writeFile(changed, bundle({ pipeline: "renamed" }));
    const { fileHash } = await import("../lib/submission-store.js");
    await assert.rejects(h.store.put(row.blob_name, changed, await fileHash(changed)), { status: 409 });
    const downloaded = join(h.root, "downloaded.zip"); await h.store.download(row.blob_name, downloaded, row.content_hash);
    assert.deepEqual(new Uint8Array(await readFile(downloaded)), bundle());
  });
  await t.test("a lost queue acknowledgement does not reverse durable success", async (child) => {
    const h = await harness(child);
    const accepted = await (await h.post()).json();
    const ack = child.mock.method(h.queue, "deleteMessage", async () => { throw new Error("ack lost"); });
    await h.service.start();
    assert.equal((await h.journal.get(accepted.id)).status, "succeeded");
    ack.mock.restore();
    await h.queue.sendMessage(accepted.id, { messageTimeToLive: -1 });
    await h.service.flush();
    assert.equal(h.db.prepare("SELECT COUNT(1) AS n FROM outcomes").get().n, 1);
    assert.equal((await h.journal.get(accepted.id)).attempts, 1);
  });
  await t.test("cloud receipts replay into an empty cache and retention keeps tombstones", async (child) => {
    const h = await harness(child);
    const accepted = await (await h.post()).json(); await h.service.start(); await h.service.stop();
    const row = await h.journal.get(accepted.id);
    const replacement = initializeDatabase(join(h.root, "replacement.db")); initializeRunMetadata(replacement);
    try {
      const replay = await replayReceipts({ db: replacement, journal: h.journal, store: h.store, stagingRoot: join(h.root, "replay") });
      assert.equal(replay.restored, 1); assert.deepEqual(replay.failures, []);
      const options = { db: replacement, journal: h.journal, store: h.store, cutoff: new Date(Date.now() + 60_000).toISOString() };
      assert.equal((await pruneReceipts(options)).candidates, 1);
      assert.equal((await h.journal.get(accepted.id)).expired_at, null);
      const pruned = await pruneReceipts({ ...options, apply: true }); assert.deepEqual(pruned.failures, []);
      assert.equal(pruned.removedArchives, 1);
      assert.equal(replacement.prepare("SELECT COUNT(1) AS n FROM runs").get().n, 0);
      assert.ok((await h.journal.get(accepted.id)).expired_at);
      assert.equal(await h.container.getBlobClient(row.blob_name).exists(), false);
      assert.equal((await replayReceipts({ db: replacement, journal: h.journal, store: h.store, stagingRoot: h.root })).restored, 0);
    } finally { replacement.close(); }
  });
});