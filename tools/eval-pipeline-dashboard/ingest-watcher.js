// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Watches a vally results directory and incrementally ingests new runs into the
// live SQLite database so the dashboard reflects them after a browser refresh.
//
// ingestDirectory() is idempotent: it skips runs already present (keyed by the
// run folder name) and only inserts new ones, so re-running it is cheap.
import { watch } from "node:fs";
import { ingestDirectory } from "@microsoft/vally-server";

/**
 * Start watching `resultsDir` and ingest new runs into `db`.
 *
 * @param {object}  opts
 * @param {import("better-sqlite3").Database} opts.db   Open database handle.
 * @param {string}  opts.resultsDir                     Directory to watch/ingest.
 * @param {number} [opts.debounceMs=3000]  Quiet period after the last fs event
 *                                         before ingesting (lets in-progress
 *                                         writes/copies finish).
 * @param {number} [opts.pollMs=30000]     Safety-net re-scan interval (covers
 *                                         platforms where fs.watch misses events,
 *                                         e.g. some network/Azure Files mounts).
 * @param {(msg: string) => void} [opts.log=console.error]
 * @returns {() => void} stop function
 */
export function startIngestWatcher({
  db,
  resultsDir,
  debounceMs = 3000,
  pollMs = 30000,
  log = console.error,
}) {
  let timer = null;
  let running = false;
  let rerun = false;

  async function ingest(reason) {
    if (running) {
      // Coalesce: remember that another pass is needed once this one finishes.
      rerun = true;
      return;
    }
    running = true;
    try {
      const results = await ingestDirectory(db, resultsDir);
      const added = results.filter((r) => !r.alreadyIngested);
      if (added.length > 0) {
        const outcomes = added.reduce((s, r) => s + r.outcomeCount, 0);
        log(
          `[ingest] +${added.length} new run(s), ${outcomes} outcome(s) [${reason}]`,
        );
      }
    } catch (err) {
      log(`[ingest] error: ${err instanceof Error ? err.message : String(err)}`);
    } finally {
      running = false;
      if (rerun) {
        rerun = false;
        schedule("coalesced");
      }
    }
  }

  function schedule(reason) {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = null;
      void ingest(reason);
    }, debounceMs);
  }

  // Ingest immediately so the dashboard has data on first load.
  void ingest("startup");

  // React to filesystem changes (recursive where supported).
  let watcher;
  try {
    watcher = watch(resultsDir, { recursive: true }, () => schedule("fs-change"));
    watcher.on("error", (e) => log(`[ingest] watch error: ${e.message}`));
    log(`[ingest] watching ${resultsDir} for new run results`);
  } catch (e) {
    log(
      `[ingest] recursive watch unavailable (${e.message}); using ${pollMs}ms polling only`,
    );
  }

  // Safety-net poll (cheap: already-ingested runs are skipped).
  const poll = setInterval(() => schedule("poll"), pollMs);

  return () => {
    if (timer) clearTimeout(timer);
    clearInterval(poll);
    watcher?.close();
  };
}
