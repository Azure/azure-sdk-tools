/**
 * backlog.test.ts — Gate 3 derive-pending (Track B, Phase 3).
 *
 * `pending = desired − done − skipped`, joined by `window_id`. These tests pin
 * the identity contract: a ledger record marks a window done ONLY when its
 * `window_id` matches AND its outcome is a DONE outcome — never by filename and
 * never when the record's own fields drift from its id. They also pin
 * termination (a fully-ledgered range → empty pending) and retry eligibility
 * (`failed` stays pending; `skipped` is removed).
 */
import { describe, expect, it } from "vitest";

import { derivePending } from "../scripts/backlog.ts";
import { genBacklog, type BacklogWindow } from "../scripts/gen-backlog.ts";
import {
    LedgerRecordSchema,
    type LedgerRecord,
    type Outcome,
    type Skipped,
} from "../scripts/pacer-schema.ts";
import { PacerConfigSchema } from "../scripts/pacer-schema.ts";

const REPO = "Azure/azure-sdk-for-go";
const NOW = Date.parse("2026-06-01T00:00:00Z"); // Jan–Apr settled

function desiredWindows(): BacklogWindow[] {
    const config = PacerConfigSchema.parse({
        repos: [REPO],
        start_date: "2026-01-01",
        granularity: "month",
    });
    return genBacklog(config, { now: NOW });
}

/** A schema-valid ledger record for a desired window with the given outcome. */
function ledgerFor(w: BacklogWindow, outcome: Outcome): LedgerRecord {
    return LedgerRecordSchema.parse({
        window_id: w.windowId,
        repo: w.repo,
        window_start: w.windowStart,
        window_end: w.windowEnd,
        cohort: w.cohort,
        schema_version: "1.1",
        outcome,
        attempt: 1,
        run_id: "1001",
        artifact_name: `run-${w.windowId}`,
        ts: "2026-05-20T00:00:00Z",
    });
}

const noSkips: Skipped = { windows: [] };

describe("derivePending — done/pending/skipped by identity", () => {
    it("empty ledger ⇒ everything pending", () => {
        const desired = desiredWindows();
        const d = derivePending(desired, [], noSkips);
        expect(d.pending).toHaveLength(desired.length);
        expect(d.done).toHaveLength(0);
        expect(d.skipped).toHaveLength(0);
    });

    it("a fully-ledgered range terminates (pending empty)", () => {
        const desired = desiredWindows();
        const ledger = desired.map((w) => ledgerFor(w, "produced"));
        const d = derivePending(desired, ledger, noSkips);
        expect(d.pending).toHaveLength(0);
        expect(d.done).toHaveLength(desired.length);
    });

    it("produced / thin-noop / signal-noop all count as done; failed does not", () => {
        const desired = desiredWindows();
        expect(desired.length).toBeGreaterThanOrEqual(4);
        const ledger = [
            ledgerFor(desired[0]!, "produced"),
            ledgerFor(desired[1]!, "thin-noop"),
            ledgerFor(desired[2]!, "signal-noop"),
            ledgerFor(desired[3]!, "failed"),
        ];
        const d = derivePending(desired, ledger, noSkips);
        const doneIds = new Set(d.done.map((w) => w.windowId));
        expect(doneIds.has(desired[0]!.windowId)).toBe(true);
        expect(doneIds.has(desired[1]!.windowId)).toBe(true);
        expect(doneIds.has(desired[2]!.windowId)).toBe(true);
        // failed ⇒ still pending (eligible for retry).
        expect(doneIds.has(desired[3]!.windowId)).toBe(false);
        expect(d.pending.map((w) => w.windowId)).toContain(
            desired[3]!.windowId,
        );
    });

    it("a ledger record for a NON-matching window_id does not mark any window done", () => {
        const desired = desiredWindows();
        // A produced record whose id belongs to a DIFFERENT repo/window.
        const foreign = ledgerFor(desired[0]!, "produced");
        foreign.window_id = foreign.window_id.replace(
            "azure-sdk-for-go",
            "azure-sdk-for-net",
        );
        const d = derivePending(desired, [foreign], noSkips);
        expect(d.done).toHaveLength(0);
        expect(d.pending).toHaveLength(desired.length);
    });

    it("skipped windows are removed and take precedence over pending", () => {
        const desired = desiredWindows();
        const skipped: Skipped = {
            windows: [
                {
                    window_id: desired[1]!.windowId,
                    reason: "exceeded retry cap",
                    ts: "2026-05-25T00:00:00Z",
                },
            ],
        };
        const d = derivePending(desired, [], skipped);
        expect(d.skipped.map((w) => w.windowId)).toEqual([
            desired[1]!.windowId,
        ]);
        expect(d.pending.map((w) => w.windowId)).not.toContain(
            desired[1]!.windowId,
        );
    });
});
