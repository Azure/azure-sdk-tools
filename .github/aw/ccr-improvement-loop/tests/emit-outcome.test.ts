/**
 * emit-outcome.test.ts — Gate 3 typed terminal outcome (Track B, Phase 3).
 *
 * The outcome is the loop's completion contract, so the classifier must map every
 * observable scenario to exactly one terminal value, and the written artifact
 * must carry a deterministic `artifact_name` and the observed cost. These tests
 * pin the full truth table (including the precedence that a hard failure with
 * zero PRs is `failed`, not `thin-noop`) and the artifact shape.
 */
import { describe, expect, it } from "vitest";

import {
    buildOutcome,
    classifyOutcome,
    outcomeFileName,
    type OutcomeSignals,
} from "../scripts/emit-outcome.ts";
import { OutcomeArtifactSchema } from "../scripts/pacer-schema.ts";
import { runArtifactBase } from "../scripts/window-id.ts";

const WINDOW_ID =
    "Azure_azure-sdk-for-go__2026-01-01__2026-01-31__uncapped__raw1.0";

function signals(over: Partial<OutcomeSignals> = {}): OutcomeSignals {
    return {
        prCount: 100,
        minPrs: 50,
        agentNoop: false,
        uploadConfirmed: true,
        hardFailure: false,
        ...over,
    };
}

describe("classifyOutcome — truth table", () => {
    it("confirmed run-artifact upload ⇒ produced", () => {
        expect(classifyOutcome(signals())).toBe("produced");
    });

    it("fewer than minPrs ⇒ thin-noop", () => {
        expect(classifyOutcome(signals({ prCount: 49 }))).toBe("thin-noop");
    });

    it("≥ minPrs but agent noop ⇒ signal-noop", () => {
        expect(classifyOutcome(signals({ agentNoop: true }))).toBe(
            "signal-noop",
        );
    });

    it("real work but upload NOT confirmed ⇒ failed (never produced)", () => {
        expect(classifyOutcome(signals({ uploadConfirmed: false }))).toBe(
            "failed",
        );
    });

    it("hard failure ⇒ failed, overriding a low PR count", () => {
        // A crashed run with prCount 0 must be failed (retryable), not thin-noop.
        expect(
            classifyOutcome(
                signals({
                    prCount: 0,
                    hardFailure: true,
                    uploadConfirmed: false,
                }),
            ),
        ).toBe("failed");
    });

    it("hard failure overrides an otherwise-produced run", () => {
        expect(classifyOutcome(signals({ hardFailure: true }))).toBe("failed");
    });

    it("boundary: prCount === minPrs is measurable (not thin)", () => {
        expect(classifyOutcome(signals({ prCount: 50, minPrs: 50 }))).toBe(
            "produced",
        );
    });
});

describe("buildOutcome — deterministic artifact", () => {
    it("derives artifact_name from window_id and validates against the schema", () => {
        const a = buildOutcome({
            windowId: WINDOW_ID,
            outcome: "produced",
            runId: "1234",
            attempt: 2,
            ts: "2026-05-20T12:00:00Z",
            cost: { rest: 101, graphql: 300 },
        });
        expect(a.artifact_name).toBe(runArtifactBase(WINDOW_ID));
        expect(a.artifact_name).toBe(`run-${WINDOW_ID}`);
        expect(a.cost).toEqual({ rest: 101, graphql: 300 });
        // Round-trips through the strict schema.
        expect(() => OutcomeArtifactSchema.parse(a)).not.toThrow();
    });

    it("is stable across attempts (artifact_name unchanged)", () => {
        const base = {
            windowId: WINDOW_ID,
            outcome: "failed" as const,
            runId: "1",
            ts: "2026-05-20T12:00:00Z",
            cost: { rest: 0, graphql: 0 },
        };
        const a1 = buildOutcome({ ...base, attempt: 1 });
        const a2 = buildOutcome({ ...base, attempt: 2 });
        expect(a1.artifact_name).toBe(a2.artifact_name);
    });

    it("names the outcome file deterministically from the window id", () => {
        expect(outcomeFileName(WINDOW_ID)).toBe(`outcome-${WINDOW_ID}.json`);
    });
});
