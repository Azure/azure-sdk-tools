import { IngestionError } from "./ingestion-error.js";

export const isTerminalReceipt = (row) => Boolean(row.expired_at) || ["succeeded", "failed"].includes(row.status);

export function createReceiptRecord({ id, contentHash, publisherId, manifest, blob }, now = Date.now()) {
  const time = new Date(now).toISOString();
  return {
    id, content_hash: contentHash, publisher_id: publisherId,
    manifest_json: JSON.stringify(manifest), blob_name: blob.name, blob_size: blob.size,
    blob_etag: blob.etag ?? null, accepted_at: time, updated_at: time,
    status: "queued", attempts: 0, next_attempt_ms: 0,
    error_code: null, run_id: null, expired_at: null,
  };
}

export function assertReceiptRetry(row, { contentHash, publisherId }) {
  if (row.publisher_id !== publisherId || row.content_hash !== contentHash) {
    throw new IngestionError(409, "submission_conflict", "This submission identity already has a different owner or content.");
  }
  if (row.expired_at) {
    throw new IngestionError(410, "submission_expired", "This submission has expired under the retention policy.");
  }
}