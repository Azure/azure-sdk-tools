import { ingestSubmission } from "./ingest-submission.js";

export function retentionCutoff(days = 731, now = Date.now()) {
  if (!Number.isSafeInteger(days) || days < 1 || days > 36500) throw new Error("Retention days must be a positive integer (1–36500).");
  return new Date(now - days * 24 * 60 * 60 * 1000).toISOString();
}

export async function replayReceipts({ db, journal, store, stagingRoot, cutoff = null }) {
  const result = { restored: 0, skipped: 0, failures: [] };
  for await (const receipt of journal.list()) {
    if (receipt.status !== "succeeded" || receipt.expired_at || (cutoff && receipt.accepted_at < cutoff)) { result.skipped++; continue; }
    try {
      // The same ingestion path verifies the saved hash and avoids duplicate outcomes.
      await ingestSubmission({ db, store, stagingRoot, receipt });
      result.restored++;
    } catch (error) {
      result.failures.push({ id: receipt.id, code: error.code ?? "replay_failed" });
    }
  }
  return result;
}

function removeRun(db, runId) {
  db.transaction(() => {
    db.prepare("DELETE FROM grader_results WHERE outcome_id IN (SELECT id FROM outcomes WHERE run_id = ?)").run(runId);
    db.prepare("DELETE FROM tool_calls WHERE outcome_id IN (SELECT id FROM outcomes WHERE run_id = ?)").run(runId);
    db.prepare("DELETE FROM outcomes WHERE run_id = ?").run(runId);
    db.prepare("DELETE FROM run_metadata WHERE run_id = ?").run(runId);
    db.prepare("DELETE FROM runs WHERE id = ?").run(runId);
  })();
}

export async function pruneReceipts({ db, journal, store, cutoff, apply = false }) {
  if (!cutoff || !Number.isFinite(Date.parse(cutoff))) throw new Error("A valid retention cutoff is required.");
  const result = { dryRun: !apply, cutoff, candidates: 0, expired: 0, removedArchives: 0, failures: [] };
  for await (const receipt of journal.list()) {
    if (!["succeeded", "failed"].includes(receipt.status) || receipt.accepted_at >= cutoff) continue;
    result.candidates++;
    if (!apply) continue;
    try {
      // Tombstone first: a delayed duplicate cannot reintroduce expired results.
      // Re-running after interruption completes cache/archive deletion safely.
      const expired = await journal.expire(receipt.id, cutoff);
      if (!expired) continue;
      removeRun(db, expired.run_id || `ingestion-${expired.id}`);
      if (await store.delete(expired.blob_name, expired.content_hash)) result.removedArchives++;
      result.expired++;
    } catch (error) {
      result.failures.push({ id: receipt.id, code: error.code ?? "prune_failed" });
    }
  }
  if (apply) db.pragma("wal_checkpoint(TRUNCATE)");
  return result;
}

export async function reconcilePending({ journal, apply = false }) {
  const result = { dryRun: !apply, pending: 0, enqueued: 0, failures: [] };
  for await (const receipt of journal.list()) {
    if (!["queued", "processing"].includes(receipt.status) || receipt.expired_at) continue;
    result.pending++;
    if (!apply) continue;
    try { await journal.enqueue(receipt); result.enqueued++; }
    catch (error) { result.failures.push({ id: receipt.id, code: error.code ?? "enqueue_failed" }); }
  }
  return result;
}