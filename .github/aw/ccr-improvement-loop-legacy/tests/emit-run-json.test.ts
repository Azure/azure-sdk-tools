import { describe, expect, it } from "vitest";

import {
    buildRunJson,
    runIdOf,
    ownerRepoOf,
} from "../scripts/emit-run-json.ts";
import type { BuildRunInput, RunMetaInput } from "../scripts/emit-run-json.ts";
import type { PrRowOut } from "../scripts/pr-metrics.ts";
import type { AttributedComment } from "../scripts/types.ts";
import { parseRun, SCHEMA_VERSION } from "../scripts/run-schema.ts";

const META: RunMetaInput = {
    repo: "Azure/azure-sdk-for-go",
    windowStart: "2026-06-01",
    windowEnd: "2026-06-18",
    windowLagDays: 14,
    prState: "merged",
    model: "openai/gpt-4o",
    modelTool: "gh models",
    temperature: 0,
    matchedCcrLogin: "copilot-pull-request-reviewer[bot]",
    promptHashes: { judge: "sha256:abc" },
    vocabularyHash: "sha256:def",
    toolVersion: "1.0",
    ccrEnabledSince: null,
};

function prRow(number: number): PrRowOut {
    return {
        number,
        url: `https://x/pr/${String(number)}`,
        title: `pr ${String(number)}`,
        author: "alice",
        additions: 10,
        deletions: 2,
        createdAt: "2026-06-10T00:00:00Z",
        mergedAt: "2026-06-11T00:00:00Z",
        prType: "feature",
        prTypeSource: "label",
        classificationStatus: "complete",
        ccrReviewed: true,
        cycleTimeHours: 24,
        iterations: 1,
    };
}

function ccrComment(id: number): AttributedComment {
    return {
        pr: 1,
        externalId: id,
        url: `https://x/c/${String(id)}`,
        rowId: `1:inline:${String(id)}`,
        findingId: `1:bot:src/a.ts:${String(id)}-${String(id)}`,
        authorKind: "ccr",
        authorLogin: "copilot-pull-request-reviewer[bot]",
        kind: "ask",
        source: "inline",
        path: "src/a.ts",
        lineStart: id,
        lineEnd: id,
        lineStale: false,
        createdAt: "2026-06-10T01:00:00Z",
        ccrSawCode: false,
        pathExcluded: false,
        ccrOutcome: "addressed",
        ccrAddressedConcern: null,
        isSubstantive: true,
        diffDetectable: true,
        severity: "substantive",
        category: "error-handling",
        confidence: 0.9,
        judgeStatus: "ok",
        judgeError: null,
        isGap: null,
        theme: "error-handling",
        body: "raw body that should be stripped on emit",
    };
}

function baseInput(generatedAt: string): BuildRunInput {
    return {
        meta: META,
        prs: [prRow(1)],
        comments: [ccrComment(1)],
        themes: [],
        proposedEdits: [],
        experiment: null,
        automationLogins: [],
        generatedAt,
    };
}

describe("emit-run-json buildRunJson", () => {
    it("derives run.id from <window-end>_<owner>_<repo>", () => {
        expect(runIdOf("2026-06-18", "Azure/azure-sdk-for-go")).toBe(
            "2026-06-18_Azure_azure-sdk-for-go",
        );
    });

    it("rejects a malformed repo", () => {
        expect(() => ownerRepoOf("nope")).toThrow();
    });

    it("produces a schema-valid run with computed metrics", () => {
        const run = buildRunJson(baseInput("2026-06-18T00:00:00Z"));
        expect(run.schemaVersion).toBe(SCHEMA_VERSION);
        expect(run.run.prCount).toBe(1);
        expect(run.metrics.rates.addressedRate?.value).toBeCloseTo(1);
        // round-trips through the schema cleanly
        expect(() => parseRun(run)).not.toThrow();
    });

    it("strips the comment body (not in the schema) on emit", () => {
        const run = buildRunJson(baseInput("2026-06-18T00:00:00Z"));
        const row = run.comments[0] as Record<string, unknown>;
        expect("body" in row).toBe(false);
        expect(row.findingId).toBe("1:bot:src/a.ts:1-1");
        expect(row.rowId).toBe("1:inline:1");
    });

    it("re-emission is content-stable except generatedAt", () => {
        const a = buildRunJson(baseInput("2026-06-18T00:00:00Z"));
        const b = buildRunJson(baseInput("2026-06-19T11:22:33Z"));
        const strip = (r: typeof a): unknown => {
            const copy = structuredClone(r);
            copy.run.generatedAt = "<volatile>";
            return copy;
        };
        expect(strip(a)).toEqual(strip(b));
        expect(a.run.generatedAt).not.toBe(b.run.generatedAt);
    });

    it("emits a valid run for an empty (no-PR) window", () => {
        const run = buildRunJson({
            ...baseInput("2026-06-18T00:00:00Z"),
            prs: [],
            comments: [],
        });
        expect(run.run.prCount).toBe(0);
        expect(run.metrics.rates.addressedRate?.value).toBeNull();
        expect(() => parseRun(run)).not.toThrow();
    });
});
