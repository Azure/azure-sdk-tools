import { parseArgs } from "node:util";
import { resolve } from "node:path";
import { initializeDatabase } from "@microsoft/vally-server";
import { initializeRunMetadata } from "../lib/run-metadata.js";
import { lockCache } from "../lib/cache-lock.js";
import { openStorage, readStorageConfig } from "../lib/storage-config.js";
import { pruneReceipts, reconcilePending, replayReceipts, retentionCutoff } from "../lib/maintenance.js";

const root = resolve(import.meta.dirname, "..");
const { values, positionals } = parseArgs({ allowPositionals: true, options: {
  output: { type: "string" }, "retention-days": { type: "string", default: "731" }, apply: { type: "boolean", default: false },
} });
const command = positionals[0];
if (positionals.length !== 1 || !["replay", "prune", "reconcile"].includes(command)) {
  throw new Error("Usage: maintenance.mjs replay --output <replacement.db> | prune [--apply] | reconcile [--apply] [--retention-days 731]");
}
const config = readStorageConfig(process.env, root);
const cutoff = retentionCutoff(Number(values["retention-days"]));
if (command === "replay" && !values.output) throw new Error("Replay requires --output pointing to a replacement cache. The active cache and journal are not deleted.");
if (command !== "replay" && values.output) throw new Error("--output is only valid for replay; retention always locks the configured active cache.");
const target = values.output ? resolve(values.output) : config.dbPath;
if (target.toLowerCase() === config.journalPath.toLowerCase()) throw new Error("The replay output must not be the receipt journal.");
const unlock = await lockCache(target);
let db;
let journal;
try {
  const storage = await openStorage(config);
  journal = storage.journal;
  let result;
  if (command === "reconcile") {
    result = await reconcilePending({ journal, apply: values.apply });
  } else {
    db = initializeDatabase(target);
    initializeRunMetadata(db);
    result = command === "replay"
      ? await replayReceipts({ db, journal, store: storage.store, stagingRoot: config.stagingRoot, cutoff })
      : await pruneReceipts({ db, journal, store: storage.store, cutoff, apply: values.apply });
  }
  console.log(JSON.stringify(result, null, 2));
  if (result.failures.length) process.exitCode = 1;
} finally {
  db?.close();
  await journal?.close();
  await unlock();
}