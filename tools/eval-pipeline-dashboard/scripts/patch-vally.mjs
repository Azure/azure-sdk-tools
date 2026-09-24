// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Post-install performance patch for @microsoft/vally-server.
//
// Why: DatabaseStore.listOutcomes() runs `SELECT o.*`, which reads the large
// `raw_json` trajectory blob from disk for every outcome row even though the
// run-list / outcomes-table summaries never use it. On a database with many
// runs this dominates query time (a global outcomes fetch took ~40s) and grows
// linearly as more result folders are ingested. The dashboard's "All" and
// per-category views fan this query out once per run, compounding the cost.
//
// This script rewrites that one query to select only the summary columns the
// code actually consumes, leaving raw_json on disk. getOutcome()/getTrajectory()
// still read raw_json via their own targeted queries, so nothing else changes.
//
// It runs as an npm `postinstall` hook so it survives Oryx rebuilding
// node_modules on every Azure deploy. It is idempotent: if the target string is
// already gone (already patched or upstream changed), it exits cleanly.
import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const target = resolve(
  __dirname,
  "..",
  "node_modules",
  "@microsoft",
  "vally-server",
  "dist",
  "data",
  "database-store.js",
);

const NEEDLE = "`SELECT o.* FROM outcomes o ${where} ORDER BY o.id LIMIT ? OFFSET ?`";
const REPLACEMENT = [
  "`SELECT o.id, o.run_id, o.stimulus_name, o.model, o.trial_index, o.status,",
  "          o.passed, o.score, o.duration_ms, o.turn_count, o.stimulus_turns,",
  "          o.tool_call_count, o.error_count,",
  "          o.input_tokens, o.output_tokens, o.cache_read_tokens, o.cache_write_tokens,",
  "          o.llm_call_count, o.cost_provider, o.cost_unit, o.cost_amount,",
  "          o.experiment_name, o.experiment_run_id, o.variant, o.baseline,",
  "          o.eval_file, o.eval_hash, o.config_hash, o.shard_key, o.end_reason, o.error_text",
  "        FROM outcomes o ${where} ORDER BY o.id LIMIT ? OFFSET ?`",
].join("\n");

function main() {
  if (!existsSync(target)) {
    console.warn(`[patch-vally] target not found, skipping: ${target}`);
    return;
  }
  const src = readFileSync(target, "utf8");
  if (!src.includes(NEEDLE)) {
    if (src.includes("o.cache_write_tokens,\n") && src.includes("FROM outcomes o ${where} ORDER BY o.id")) {
      console.log("[patch-vally] listOutcomes already patched, skipping.");
    } else {
      console.warn("[patch-vally] expected query not found; upstream may have changed. Skipping.");
    }
    return;
  }
  writeFileSync(target, src.replace(NEEDLE, REPLACEMENT), "utf8");
  console.log("[patch-vally] applied listOutcomes raw_json performance patch.");
}

main();
