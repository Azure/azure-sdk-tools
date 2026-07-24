/**
 * backlog-io.test.ts — Gate 3 ledger/skip IO round-trip (Track B, Phase 3).
 *
 * `readLedgerDir` / `readSkippedFile` are what the pacer feeds from a checkout of
 * the durable `ccr-pacer-state` branch. This exercises them against a seeded
 * on-disk `ledger/` + `skipped.json`: every record is schema-validated on read,
 * a missing branch degrades to an empty backlog, and the derived pending matches
 * what the pure derive produced from the same data.
 */
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
    derivePending,
    readLedgerDir,
    readSkippedFile,
} from "../scripts/backlog.ts";
import { genBacklog, type BacklogWindow } from "../scripts/gen-backlog.ts";
import { PacerConfigSchema } from "../scripts/pacer-schema.ts";

const REPO = "Azure/azure-sdk-for-go";
const NOW = Date.parse("2026-06-01T00:00:00Z");

let root: string;
beforeEach(() => {
    root = mkdtempSync(join(tmpdir(), "ccr-state-"));
});
afterEach(() => {
    rmSync(root, { recursive: true, force: true });
});

function desired(): BacklogWindow[] {
    return genBacklog(
        PacerConfigSchema.parse({
            repos: [REPO],
            start_date: "2026-01-01",
            granularity: "month",
        }),
        { now: NOW },
    );
}

function seedLedger(w: BacklogWindow, outcome: string): void {
    const dir = join(root, "ledger");
    mkdirSync(dir, { recursive: true });
    writeFileSync(
        join(dir, `${w.windowId}.json`),
        JSON.stringify({
            window_id: w.windowId,
            repo: w.repo,
            window_start: w.windowStart,
            window_end: w.windowEnd,
            cohort: w.cohort,
            schema_version: "1.1",
            outcome,
            attempt: 1,
            run_id: "77",
            artifact_name: `run-${w.windowId}`,
            ts: "2026-05-20T00:00:00Z",
        }),
    );
}

describe("ledger/skip IO", () => {
    it("a missing state dir yields an empty ledger and no skips", () => {
        expect(readLedgerDir(join(root, "nope", "ledger"))).toEqual([]);
        expect(readSkippedFile(join(root, "nope", "skipped.json"))).toEqual({
            windows: [],
        });
    });

    it("reads + validates seeded records and derives pending correctly", () => {
        const windows = desired();
        seedLedger(windows[0]!, "produced");
        seedLedger(windows[1]!, "failed"); // not done → stays pending
        writeFileSync(
            join(root, "skipped.json"),
            JSON.stringify({
                windows: [
                    {
                        window_id: windows[2]!.windowId,
                        reason: "retry cap",
                        ts: "2026-05-25T00:00:00Z",
                    },
                ],
            }),
        );

        const ledger = readLedgerDir(join(root, "ledger"));
        const skipped = readSkippedFile(join(root, "skipped.json"));
        expect(ledger).toHaveLength(2);

        const d = derivePending(windows, ledger, skipped);
        expect(d.done.map((w) => w.windowId)).toEqual([windows[0]!.windowId]);
        expect(d.skipped.map((w) => w.windowId)).toEqual([
            windows[2]!.windowId,
        ]);
        // window[1] (failed) is pending; window[3+] untouched are pending.
        expect(d.pending.map((w) => w.windowId)).toContain(
            windows[1]!.windowId,
        );
        expect(d.pending.map((w) => w.windowId)).not.toContain(
            windows[0]!.windowId,
        );
    });

    it("rejects a drifted ledger record on read (strict schema)", () => {
        const dir = join(root, "ledger");
        mkdirSync(dir, { recursive: true });
        writeFileSync(
            join(dir, "bad.json"),
            JSON.stringify({ window_id: "x", outcome: "produced" }), // missing fields
        );
        expect(() => readLedgerDir(dir)).toThrow();
    });
});
