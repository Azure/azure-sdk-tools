import { randomUUID } from "node:crypto";
import { IngestionError } from "./ingestion-error.js";
import { assertReceiptRetry, createReceiptRecord, isTerminalReceipt } from "./receipt-record.js";
import { submissionBlobName, submissionId, validateSubmissionManifest } from "./submission-manifest.js";

export const RECEIPT_PREFIX = "_receipts/v1/";
const validId = (id) => /^[a-f0-9]{64}$/.test(id);
const visibilitySeconds = 120;

// Blob receipts are durable state; Queue messages contain only receipt IDs.
// Leases protect mutable receipt transitions and are renewed while processing.
export function createAzureIngestionJournal({ containerClient, queueClient, now = Date.now,
  pollMs = 5000, heartbeatMs = 20_000, log = console.error }) {
  const claims = new Map();
  const timestamp = () => new Date(now()).toISOString();
  const blobFor = (id) => {
    if (!validId(id)) throw new Error("Invalid receipt ID.");
    return containerClient.getBlockBlobClient(`${RECEIPT_PREFIX}${id}.json`);
  };
  const report = (event, error) => log(JSON.stringify({ component: "ingestion-journal", event, code: error?.code ?? "storage_error" }));

  async function read(blob, id) {
    const properties = await blob.getProperties();
    if (!properties.contentLength || properties.contentLength > 64 * 1024) throw new Error("Invalid stored receipt size.");
    const bytes = await blob.downloadToBuffer(0, properties.contentLength, { conditions: { ifMatch: properties.etag } });
    const row = JSON.parse(bytes.toString("utf8"));
    const source = JSON.parse(row.manifest_json);
    const manifest = validateSubmissionManifest({ ...source, branch: source.branch ?? undefined, sourceVersion: source.sourceVersion ?? undefined });
    if (row.id !== id || submissionId(manifest) !== id || submissionBlobName(manifest) !== row.blob_name ||
        !/^[a-f0-9]{64}$/.test(row.content_hash) || typeof row.publisher_id !== "string" || !row.publisher_id ||
        !["queued", "processing", "succeeded", "failed"].includes(row.status) ||
        !Number.isSafeInteger(row.attempts) || row.attempts < 0 || !Number.isFinite(Date.parse(row.accepted_at))) {
      throw new Error("Stored receipt failed validation.");
    }
    return { row, etag: properties.etag };
  }
  async function write(blob, row, conditions) {
    const body = JSON.stringify(row);
    return blob.upload(body, Buffer.byteLength(body), {
      conditions, blobHTTPHeaders: { blobContentType: "application/json" },
    });
  }
  async function get(id) {
    try { return (await read(blobFor(id), id)).row; }
    catch (error) { if (error.statusCode === 404) return undefined; throw error; }
  }
  async function defer(message, seconds) {
    const updated = await queueClient.updateMessage(message.messageId, message.popReceipt, undefined,
      Math.min(604800, Math.max(1, Math.ceil(seconds))));
    message.popReceipt = updated.popReceipt;
  }
  async function release(lease) {
    try { await lease.releaseLease(); } catch (error) { report("lease_release_failed", error); }
  }
  async function acknowledge(message) {
    // Completion is already durable. A failed ack must not revert it to queued;
    // a redelivered message will observe the terminal receipt and be discarded.
    try { await queueClient.deleteMessage(message.messageId, message.popReceipt); }
    catch (error) { report("message_ack_failed", error); }
  }
  function renew(claim) {
    const operation = claim.renewal.then(async () => {
      if (claim.error) throw claim.error;
      await claim.lease.renewLease();
      await defer(claim.message, visibilitySeconds);
    });
    claim.renewal = operation.catch((error) => { claim.error = error; report("ownership_renewal_failed", error); });
    return operation;
  }
  async function stopHeartbeat(claim) {
    clearInterval(claim.timer);
    await claim.renewal;
    if (claim.error) throw new IngestionError(503, "ownership_lost", "Receipt ownership must be recovered before processing can continue.");
  }
  async function finish(id, status, runId, errorCode, delayMs) {
    const claim = claims.get(id);
    if (!claim) throw new Error("Receipt is not owned by this worker.");
    try {
      await stopHeartbeat(claim);
      await claim.lease.renewLease();
      const row = { ...claim.row, status, updated_at: timestamp(), run_id: runId,
        error_code: errorCode, next_attempt_ms: delayMs === null ? 0 : now() + delayMs };
      await write(claim.blob, row, { leaseId: claim.lease.leaseId, ifMatch: claim.etag });
      if (status === "queued") await defer(claim.message, delayMs / 1000);
      else await acknowledge(claim.message);
    } finally {
      clearInterval(claim.timer);
      claims.delete(id);
      await release(claim.lease);
    }
  }

  return {
    get,
    async insert(input) {
      const row = createReceiptRecord(input, now());
      try { await write(blobFor(row.id), row, { ifNoneMatch: "*" }); return row; }
      catch (error) {
        if (![409, 412].includes(error.statusCode)) throw error;
        const existing = await get(row.id);
        if (!existing) throw error;
        assertReceiptRetry(existing, input);
        return existing;
      }
    },
    async enqueue(row) {
      if (!isTerminalReceipt(row)) {
        // Non-expiring messages: an acknowledged run must not age out of the queue.
        // Duplicate enqueue repairs receipt-saved / queue-send-failed acceptance.
        await queueClient.sendMessage(row.id, { messageTimeToLive: -1 });
      }
    },
    recover() {}, // Queue redelivery plus expiring Blob leases recover interrupted work.
    async claim() {
      // Drain a bounded number of stale/duplicate messages per worker wake-up.
      for (let n = 0; n < 16; n++) {
        const { receivedMessageItems } = await queueClient.receiveMessages({ numberOfMessages: 1, visibilityTimeout: visibilitySeconds });
        const message = receivedMessageItems[0];
        if (!message) return null;
        if (!validId(message.messageText)) {
          report("invalid_queue_message");
          await acknowledge(message);
          continue;
        }
        const id = message.messageText;
        const blob = blobFor(id);
        const lease = blob.getBlobLeaseClient(randomUUID());
        try { await lease.acquireLease(60); }
        catch (error) {
          if ([404, 409, 412].includes(error.statusCode)) { await defer(message, 30); continue; }
          throw error;
        }
        let retained = false;
        try {
          const { row, etag } = await read(blob, id);
          if (isTerminalReceipt(row)) { await acknowledge(message); continue; }
          if (row.next_attempt_ms > now()) { await defer(message, (row.next_attempt_ms - now()) / 1000); continue; }
          const processing = { ...row, status: "processing", attempts: row.attempts + 1, updated_at: timestamp(), next_attempt_ms: 0 };
          const updated = await write(blob, processing, { leaseId: lease.leaseId, ifMatch: etag });
          const claim = { blob, lease, message, row: processing, etag: updated.etag, renewal: Promise.resolve(), error: null };
          claim.timer = setInterval(() => { void renew(claim).catch(() => {}); }, heartbeatMs);
          claim.timer.unref();
          claims.set(id, claim);
          retained = true;
          return processing;
        } finally { if (!retained) await release(lease); }
      }
      return null;
    },
    async assertOwnership(id) {
      const claim = claims.get(id);
      if (!claim) throw new IngestionError(503, "ownership_lost", "Receipt is no longer owned by this worker.");
      await renew(claim);
    },
    succeed(id, runId) { return finish(id, "succeeded", runId, null, null); },
    fail(id, code, delayMs = null) { return finish(id, delayMs === null ? "failed" : "queued", null, code, delayMs); },
    nextDelay() { return pollMs; }, // Receive work messages, never scan archive blobs.
    async metrics() {
      const properties = await queueClient.getProperties();
      return { pendingApproximate: properties.approximateMessagesCount };
    },
    async check() { await Promise.all([containerClient.getProperties(), queueClient.getProperties()]); },
    async *list() {
      // Explicit maintenance/replay only; normal startup never calls this method.
      for await (const blob of containerClient.listBlobsFlat({ prefix: RECEIPT_PREFIX })) {
        const id = blob.name.slice(RECEIPT_PREFIX.length, -5);
        if (!blob.name.endsWith(".json") || !validId(id)) continue;
        const row = await get(id);
        if (row) yield row;
      }
    },
    async expire(id, cutoff) {
      const blob = blobFor(id);
      const lease = blob.getBlobLeaseClient(randomUUID());
      await lease.acquireLease(60);
      try {
        const { row, etag } = await read(blob, id);
        if (!["succeeded", "failed"].includes(row.status) || row.accepted_at >= cutoff) return null;
        if (row.expired_at) return row;
        const expired = { ...row, expired_at: timestamp(), updated_at: timestamp() };
        await write(blob, expired, { leaseId: lease.leaseId, ifMatch: etag });
        return expired;
      } finally { await release(lease); }
    },
    async close() {
      for (const claim of claims.values()) {
        clearInterval(claim.timer);
        await claim.renewal;
        await release(claim.lease);
      }
      claims.clear();
    },
  };
}