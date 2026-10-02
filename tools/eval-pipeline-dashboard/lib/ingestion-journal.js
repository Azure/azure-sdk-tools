import Database from "better-sqlite3";
import { mkdirSync } from "node:fs";
import { dirname } from "node:path";
import { createReceiptRecord } from "./receipt-record.js";

export function openIngestionJournal(path, { now = Date.now } = {}) {
  if (path !== ":memory:") mkdirSync(dirname(path), { recursive: true });
  const db = new Database(path);
  db.pragma("journal_mode = WAL");
  db.pragma("synchronous = FULL");
  db.exec(`
    CREATE TABLE IF NOT EXISTS ingestions (
      id TEXT PRIMARY KEY,
      content_hash TEXT NOT NULL,
      publisher_id TEXT NOT NULL,
      manifest_json TEXT NOT NULL,
      blob_name TEXT NOT NULL UNIQUE,
      blob_size INTEGER NOT NULL,
      accepted_at TEXT NOT NULL,
      updated_at TEXT NOT NULL,
      status TEXT NOT NULL CHECK(status IN ('queued', 'processing', 'succeeded', 'failed')),
      attempts INTEGER NOT NULL DEFAULT 0,
      next_attempt_ms INTEGER NOT NULL DEFAULT 0,
      error_code TEXT,
      run_id TEXT,
      blob_etag TEXT,
      expired_at TEXT
    );
    CREATE INDEX IF NOT EXISTS idx_ingestions_pending ON ingestions(status, next_attempt_ms);
  `);
  const columns = db.prepare("PRAGMA table_info(ingestions)").all();
  for (const column of ["blob_etag", "expired_at"]) {
    if (!columns.some(({ name }) => name === column)) db.exec(`ALTER TABLE ingestions ADD COLUMN ${column} TEXT`);
  }
  const timestamp = () => new Date(now()).toISOString();
  const get = (id) => db.prepare("SELECT * FROM ingestions WHERE id = ?").get(id);
  return {
    get,
    insert(input) {
      const row = createReceiptRecord(input, now());
      db.prepare(`INSERT INTO ingestions
        (id, content_hash, publisher_id, manifest_json, blob_name, blob_size, blob_etag, accepted_at, updated_at, status)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 'queued')`).run(
        row.id, row.content_hash, row.publisher_id, row.manifest_json, row.blob_name, row.blob_size, row.blob_etag,
        row.accepted_at, row.updated_at
      );
      return get(row.id);
    },
    // The local row itself is the durable queued-work record.
    enqueue() {},
    assertOwnership() {},
    recover() {
      return db.prepare("UPDATE ingestions SET status = 'queued', next_attempt_ms = 0, updated_at = ? WHERE status = 'processing'")
        .run(timestamp()).changes;
    },
    claim() {
      return db.transaction(() => {
        const row = db.prepare("SELECT * FROM ingestions WHERE status = 'queued' AND next_attempt_ms <= ? ORDER BY accepted_at, id LIMIT 1").get(now());
        if (!row) return null;
        db.prepare("UPDATE ingestions SET status = 'processing', attempts = attempts + 1, updated_at = ? WHERE id = ?").run(timestamp(), row.id);
        return get(row.id);
      })();
    },
    succeed(id, runId) {
      db.prepare("UPDATE ingestions SET status = 'succeeded', run_id = ?, error_code = NULL, updated_at = ? WHERE id = ? AND status = 'processing'")
        .run(runId, timestamp(), id);
    },
    fail(id, code, delayMs = null) {
      db.prepare("UPDATE ingestions SET status = ?, error_code = ?, next_attempt_ms = ?, updated_at = ? WHERE id = ? AND status = 'processing'")
        .run(delayMs === null ? "failed" : "queued", code, delayMs === null ? 0 : now() + delayMs, timestamp(), id);
    },
    nextDelay() {
      const row = db.prepare("SELECT MIN(next_attempt_ms) AS next FROM ingestions WHERE status = 'queued'").get();
      return row.next === null ? null : Math.max(0, row.next - now());
    },
    counts() {
      return Object.fromEntries(db.prepare("SELECT status, COUNT(1) AS count FROM ingestions GROUP BY status").all()
        .map((row) => [row.status, row.count]));
    },
    async *list() {
      // Finish each SQLite read before yielding: maintenance can update a receipt
      // on this connection, which better-sqlite3 disallows during .iterate().
      let after = "";
      for (;;) {
        const rows = db.prepare("SELECT * FROM ingestions WHERE id > ? ORDER BY id LIMIT 500").all(after);
        if (!rows.length) return;
        for (const row of rows) yield row;
        after = rows.at(-1).id;
      }
    },
    expire(id, cutoff) {
      db.prepare(`UPDATE ingestions SET expired_at = COALESCE(expired_at, ?), updated_at = ?
        WHERE id = ? AND accepted_at < ? AND status IN ('succeeded', 'failed')`).run(timestamp(), timestamp(), id, cutoff);
      const row = get(id);
      return row?.expired_at ? row : null;
    },
    metrics() {
      const pending = db.prepare("SELECT COUNT(1) AS count, MIN(accepted_at) AS oldest FROM ingestions WHERE status IN ('queued', 'processing')").get();
      return { pending: pending.count, oldestPendingAt: pending.oldest };
    },
    check() { db.prepare("SELECT 1").get(); },
    close() { db.close(); },
  };
}