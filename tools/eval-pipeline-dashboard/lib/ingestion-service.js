import { readDashboardBundleManifest } from "./dashboard-bundle.js";
import { IngestionError } from "./ingestion-error.js";
import { ingestSubmission } from "./ingest-submission.js";
import { submissionBlobName, submissionId, validateSubmissionManifest } from "./submission-manifest.js";
import { assertReceiptRetry } from "./receipt-record.js";

function publicReceipt(row) {
  return {
    id: row.id,
    status: row.expired_at ? "expired" : row.status,
    statusUrl: `/api/ingestions/${row.id}`,
    acceptedAt: row.accepted_at,
    updatedAt: row.updated_at,
    attempts: row.attempts,
    errorCode: row.error_code,
    runId: row.run_id,
    expiredAt: row.expired_at ?? null,
  };
}

export function createIngestionService({
  db, journal, store, stagingRoot,
  retryDelays = [1000, 5000, 30000],
  processSubmission = ingestSubmission,
  log = console.error,
}) {
  let started = false;
  let stopped = false;
  let active = null;
  let retryTimer = null;
  let wakeAgain = false;
  let accepting = Promise.resolve();
  let needsRecovery = false;
  const metrics = { since: new Date().toISOString(), accepted: 0, duplicates: 0, succeeded: 0,
    failed: 0, retries: 0, lastSuccessAt: null, lastErrorCode: null };

  async function processQueue() {
    if (needsRecovery) { await journal.recover(); needsRecovery = false; }
    while (!stopped) {
      const receipt = await journal.claim();
      if (!receipt) return;
      try {
        const runId = await processSubmission({ db, store, stagingRoot, receipt,
          beforeCommit: () => journal.assertOwnership(receipt.id) });
        await journal.succeed(receipt.id, runId);
        metrics.succeeded++;
        metrics.lastSuccessAt = new Date().toISOString();
        log(`[ingestion] ${receipt.id} succeeded`);
      } catch (error) {
        const permanent = error instanceof IngestionError && error.status < 500;
        const delay = permanent ? null : retryDelays[receipt.attempts - 1] ?? null;
        metrics.lastErrorCode = error instanceof IngestionError ? error.code : "processing_failed";
        try { await journal.fail(receipt.id, metrics.lastErrorCode, delay); }
        catch (persistenceError) { needsRecovery = true; throw persistenceError; }
        if (delay === null) metrics.failed++; else metrics.retries++;
        log(`[ingestion] ${receipt.id} ${delay === null ? "failed" : "will retry"}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
  }

  function wake() {
    if (!started || stopped) return Promise.resolve();
    clearTimeout(retryTimer);
    if (active) {
      wakeAgain = true;
      return active;
    }
    let delay = null;
    active = (async () => {
      try {
        do { wakeAgain = false; await processQueue(); } while (wakeAgain && !stopped);
        if (!stopped) delay = await journal.nextDelay();
      } catch (error) {
        metrics.lastErrorCode = "worker_unavailable";
        log(`[ingestion] worker unavailable: ${error.message}`);
        delay = 1000;
      }
    })().finally(() => {
      active = null;
      if (stopped) return;
      if (wakeAgain) {
        wakeAgain = false;
        void wake();
      } else if (delay !== null) {
        retryTimer = setTimeout(() => void wake(), Math.max(10, delay));
        retryTimer.unref();
      }
    });
    return active;
  }

  return {
    async accept({ bundlePath, contentHash, publisher }) {
      if (stopped) throw new IngestionError(503, "ingestion_stopping", "The service is stopping. Retry the same bundle.");
      let manifest;
      try {
        manifest = validateSubmissionManifest(await readDashboardBundleManifest(bundlePath));
      } catch (error) {
        if (error instanceof IngestionError) throw error;
        throw new IngestionError(422, "invalid_bundle", "Bundle is corrupt, unsafe, too large, or missing a required file.");
      }
      const operation = accepting.then(async () => {
        const id = submissionId(manifest);
        const existing = await journal.get(id);
        if (existing) {
          assertReceiptRetry(existing, { contentHash, publisherId: publisher.id });
          await journal.enqueue(existing);
          metrics.duplicates++;
          return { receipt: publicReceipt(existing), duplicate: true };
        }
        const blob = await store.put(submissionBlobName(manifest), bundlePath, contentHash);
        const row = await journal.insert({ id, contentHash, publisherId: publisher.id, manifest, blob });
        await journal.enqueue(row);
        metrics.accepted++;
        return { receipt: publicReceipt(row), duplicate: false };
      });
      accepting = operation.catch(() => {});
      try {
        const result = await operation;
        void wake();
        return result;
      } catch (error) {
        if (error instanceof IngestionError) throw error;
        log(`[ingestion] submission could not be durably accepted: ${error.message}`);
        throw new IngestionError(503, "acceptance_unavailable", "Submission could not be durably accepted. Retry the same bundle.");
      }
    },
    async get(id, publisher) {
      if (!/^[a-f0-9]{64}$/.test(id)) throw new IngestionError(404, "not_found", "Submission not found.");
      const row = await journal.get(id);
      if (!row || row.publisher_id !== publisher.id) throw new IngestionError(404, "not_found", "Submission not found.");
      return publicReceipt(row);
    },
    async start() {
      if (!started) {
        await journal.recover();
        started = true;
      }
      return wake();
    },
    flush: wake,
    async status() { return { ...metrics, workerActive: Boolean(active), queue: await journal.metrics() }; },
    async checkReady() { await Promise.all([store.check(), journal.check()]); },
    async stop() {
      stopped = true;
      clearTimeout(retryTimer);
      await accepting;
      await active;
    },
  };
}