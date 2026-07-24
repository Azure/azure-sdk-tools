#!/usr/bin/env node
/**
 * backlog.ts — derive the PENDING backlog from desired windows + the durable
 * completion ledger (Track B, Phase 3).
 *
 *   pending = desired − done − skipped
 *
 * where the joins are by canonical `window_id`, NEVER by filename or by
 * repo/date/cohort fields:
 *
 *  - **done**    = a `ledger/<window_id>.json` exists whose `outcome` is one of
 *    {@link DONE_OUTCOMES} (`produced | thin-noop | signal-noop`). A `failed`
 *    record is NOT done — it stays pending so the pacer can retry it (up to the
 *    Phase 4 cap, after which it is retired into `skipped.json`).
 *  - **skipped** = the `window_id` is listed in `skipped.json`.
 *
 * Matching by identity (not filename) is what makes the ledger tamper-evident:
 * a record whose stored repo/start/cohort/schema disagrees with its own
 * `window_id` simply doesn't match any desired window and is ignored, rather than
 * silently marking the wrong window done.
 *
 * The pure {@link derivePending} takes already-parsed inputs; {@link readLedgerDir}
 * / {@link readSkippedFile} wrap the filesystem IO the pacer feeds it (in Phase 4
 * these come from the fetched `ccr-pacer-state` branch).
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { genBacklog, type BacklogWindow } from "./gen-backlog.ts";
import {
    DONE_OUTCOMES,
    parseLedgerRecord,
    parsePacerConfig,
    parseSkipped,
    type LedgerRecord,
    type Skipped,
} from "./pacer-schema.ts";

/** The derived split of a desired backlog against the ledger. */
export interface BacklogDerivation {
    /** Windows still needing a run (not done, not skipped). */
    pending: BacklogWindow[];
    /** Windows with a terminal DONE ledger record. */
    done: BacklogWindow[];
    /** Windows retired into skipped.json. */
    skipped: BacklogWindow[];
}

/**
 * Pure derive: split `desired` into pending/done/skipped by `window_id`.
 *
 * `done` is the set of window ids with a DONE-outcome ledger record; `skipped`
 * is the set listed in `skipped.json`. Skipped takes precedence over done only
 * incidentally — a window is at most one of the three, evaluated skipped → done →
 * pending, but a well-formed state never has a window both skipped and done.
 */
export function derivePending(
    desired: BacklogWindow[],
    ledger: LedgerRecord[],
    skipped: Skipped,
): BacklogDerivation {
    const doneIds = new Set(
        ledger
            .filter((r) => DONE_OUTCOMES.includes(r.outcome))
            .map((r) => r.window_id),
    );
    const skippedIds = new Set(skipped.windows.map((w) => w.window_id));

    const out: BacklogDerivation = { pending: [], done: [], skipped: [] };
    for (const w of desired) {
        if (skippedIds.has(w.windowId)) out.skipped.push(w);
        else if (doneIds.has(w.windowId)) out.done.push(w);
        else out.pending.push(w);
    }
    return out;
}

// ---------------------------------------------------------------------------
// IO wiring.
// ---------------------------------------------------------------------------

/** Read + validate every `*.json` ledger record in a directory (missing ⇒ []). */
export function readLedgerDir(dir: string): LedgerRecord[] {
    if (!fs.existsSync(dir)) return [];
    const records: LedgerRecord[] = [];
    for (const name of fs.readdirSync(dir)) {
        if (!name.endsWith(".json")) continue;
        const raw: unknown = JSON.parse(
            fs.readFileSync(path.join(dir, name), "utf8"),
        );
        records.push(parseLedgerRecord(raw));
    }
    return records;
}

/** Read + validate `skipped.json` (missing ⇒ empty). */
export function readSkippedFile(file: string): Skipped {
    if (!fs.existsSync(file)) return { windows: [] };
    const raw: unknown = JSON.parse(fs.readFileSync(file, "utf8"));
    return parseSkipped(raw);
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/backlog.ts --config pacer/config.json --state <dir> [options]",
        "",
        "Prints { pending, done, skipped } window-id arrays as JSON on stdout.",
        "`--state` is a checkout of the ccr-pacer-state branch containing",
        "`ledger/` and `skipped.json`.",
        "",
        "Options:",
        "  --config <path>      pacer/config.json (required)",
        "  --state <dir>        ccr-pacer-state checkout (ledger/ + skipped.json)",
        "  --settle-days <N>    Settle lag in days (default: 14)",
        "  --now-ms <N>         Override clock (epoch ms) for determinism",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            config: { type: "string" },
            state: { type: "string" },
            "settle-days": { type: "string" },
            "now-ms": { type: "string" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    const v = parsed.values;
    if (!v.config) throw new Error("--config is required");

    const config = parsePacerConfig(
        JSON.parse(fs.readFileSync(v.config, "utf8")),
    );
    const settleDays =
        v["settle-days"] === undefined
            ? undefined
            : Number.parseInt(v["settle-days"], 10);
    const now = v["now-ms"] === undefined ? Date.now() : Number(v["now-ms"]);

    const desired = genBacklog(config, { now, settleDays });
    const stateDir = v.state ?? "";
    const ledger = stateDir ? readLedgerDir(path.join(stateDir, "ledger")) : [];
    const skipped = stateDir
        ? readSkippedFile(path.join(stateDir, "skipped.json"))
        : { windows: [] };

    const derived = derivePending(desired, ledger, skipped);
    process.stdout.write(
        `${JSON.stringify(
            {
                pending: derived.pending.map((w) => w.windowId),
                done: derived.done.map((w) => w.windowId),
                skipped: derived.skipped.map((w) => w.windowId),
            },
            null,
            2,
        )}\n`,
    );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        process.stderr.write(
            `backlog: ${err instanceof Error ? err.message : String(err)}\n`,
        );
        process.exit(1);
    }
}
