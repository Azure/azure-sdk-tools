import { execFileSync } from "node:child_process";
import { readFileSync, mkdtempSync, rmSync, copyFileSync } from "node:fs";
import * as os from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it, afterEach } from "vitest";

import { buildMeta } from "../scripts/prep-run.ts";
import { buildPrepSummary } from "../scripts/prep-summary.ts";
import type { PrepSummaryInput } from "../scripts/prep-summary.ts";
import type {
    AttributedComment,
    JudgeInputItem,
    VerifiedMiss,
} from "../scripts/types.ts";

const here = dirname(fileURLToPath(import.meta.url));
const scripts = join(here, "..", "scripts");
const fixture = join(here, "fixtures", "pr-sample.json");

let cid = 1;
function mkComment(over: Partial<AttributedComment>): AttributedComment {
    const id = cid++;
    return {
        pr: 1,
        externalId: id,
        url: undefined,
        rowId: `1:inline:${String(id)}`,
        findingId: `1:alice:src/a.ts:10-10`,
        authorKind: "human",
        authorLogin: "alice",
        kind: "ask",
        source: "inline",
        path: "src/a.ts",
        lineStart: 10,
        lineEnd: 10,
        lineStale: false,
        createdAt: "2026-06-10T01:00:00Z",
        ccrSawCode: true,
        pathExcluded: false,
        isSubstantive: null,
        diffDetectable: null,
        severity: null,
        category: null,
        confidence: null,
        judgeStatus: null,
        judgeError: null,
        ccrOutcome: null,
        ccrAddressedConcern: null,
        isGap: null,
        theme: null,
        body: "x",
        ...over,
    };
}

function mkJudgeItem(over: Partial<JudgeInputItem>): JudgeInputItem {
    return {
        id: `1:inline:${String(cid++)}`,
        body: "x",
        diffHunk: "@@ -1 +1 @@",
        path: "src/a.ts",
        lineStart: 10,
        lineEnd: 10,
        lineStale: false,
        purpose: "gap-candidate",
        ...over,
    };
}

function baseInput(over: Partial<PrepSummaryInput>): PrepSummaryInput {
    return {
        prs: [],
        attributed: [],
        judgeInput: [],
        classified: [],
        traced: [],
        ...over,
    };
}

describe("buildPrepSummary", () => {
    it("reports duplicate rowIds as fatal", () => {
        const dup = "1:inline:99";
        const summary = buildPrepSummary(
            baseInput({
                attributed: [
                    mkComment({ rowId: dup }),
                    mkComment({ rowId: dup }),
                ],
            }),
        );
        expect(summary.duplicateRowIds).toBe(1);
        expect(summary.fatal).toBe(true);
        expect(summary.fatalReasons.join(" ")).toContain("rowId");
    });

    it("allows duplicate findingIds as non-fatal warnings", () => {
        const summary = buildPrepSummary(
            baseInput({
                attributed: [
                    mkComment({ rowId: "1:inline:1", findingId: "shared" }),
                    mkComment({ rowId: "1:inline:2", findingId: "shared" }),
                ],
            }),
        );
        expect(summary.duplicateFindingIds).toBe(1);
        expect(summary.duplicateRowIds).toBe(0);
        expect(summary.fatal).toBe(false);
        expect(summary.warnings.join(" ")).toContain("findingId");
    });

    it("flags duplicate judge-input ids as fatal", () => {
        const summary = buildPrepSummary(
            baseInput({
                judgeInput: [
                    mkJudgeItem({ id: "dup" }),
                    mkJudgeItem({ id: "dup" }),
                ],
            }),
        );
        expect(summary.duplicateJudgeInputIds).toBe(1);
        expect(summary.fatal).toBe(true);
    });

    it("reports CCR inline counts by login and source/kind", () => {
        const summary = buildPrepSummary(
            baseInput({
                attributed: [
                    mkComment({
                        authorKind: "ccr",
                        authorLogin: "Copilot",
                        source: "inline",
                        kind: "ask",
                        rowId: "1:inline:1",
                    }),
                    mkComment({
                        authorKind: "ccr",
                        authorLogin: "Copilot",
                        source: "review",
                        kind: "summary",
                        rowId: "1:review:2",
                    }),
                    mkComment({ authorKind: "human", rowId: "1:inline:3" }),
                ],
            }),
        );
        expect(summary.ccrInlineCount).toBe(1);
        expect(summary.ccrCountsByLogin.Copilot).toBe(2);
        expect(summary.ccrCountsBySourceKind["inline:ask"]).toBe(1);
        expect(summary.commentCountsByAuthorKind.human).toBe(1);
        expect(summary.commentCountsByAuthorKind.ccr).toBe(2);
    });

    it("reports missing evidence counts", () => {
        const summary = buildPrepSummary(
            baseInput({
                judgeInput: [
                    mkJudgeItem({
                        id: "a",
                        purpose: "gap-candidate",
                        diffHunk: "",
                        ccrComments: [],
                    }),
                    mkJudgeItem({
                        id: "b",
                        purpose: "ccr-comment",
                        postCommentDiff: "",
                    }),
                    mkJudgeItem({
                        id: "c",
                        purpose: "gap-candidate",
                        ccrComments: [
                            {
                                path: "src/a.ts",
                                lineStart: 10,
                                lineEnd: 10,
                                body: "prior ccr note",
                            },
                        ],
                    }),
                ],
            }),
        );
        expect(summary.missingDiffHunk).toBe(1);
        expect(summary.missingPostCommentDiff).toBe(1);
        expect(summary.gapCandidatesWithNoPriorCcr).toBe(1);
    });

    it("counts classification status and trace outcomes", () => {
        const traced: VerifiedMiss[] = [
            {
                fixPr: 2,
                fixUrl: undefined,
                path: "src/a.ts",
                introducedByPr: 1,
                introducedUrl: undefined,
                introducingCommit: "abc",
                traceOutcome: "resolved",
                ccrOpportunity: "ccrCommentedOnLines",
                ccrActiveOnIntroducingPr: true,
                ccrCommentedOnLines: false,
                verifiedMiss: true,
                theme: null,
                blameConfidence: "high",
            },
        ];
        const summary = buildPrepSummary(
            baseInput({
                classified: [
                    { number: 1, classificationStatus: "complete" },
                    { number: 2, classificationStatus: "needs-agent" },
                ],
                traced,
            }),
        );
        expect(summary.classificationStatusCounts.complete).toBe(1);
        expect(summary.classificationStatusCounts["needs-agent"]).toBe(1);
        expect(summary.traceCount).toBe(1);
        expect(summary.verifiedMissCount).toBe(1);
        expect(summary.traceOutcomeCounts.resolved).toBe(1);
    });

    it("produces a stable summary for identical input", () => {
        const input = baseInput({
            attributed: [mkComment({ rowId: "1:inline:1" })],
        });
        const a = buildPrepSummary(input);
        const b = buildPrepSummary(input);
        expect(JSON.stringify(a)).toBe(JSON.stringify(b));
    });
});

describe("buildMeta", () => {
    it("emits the minimal run metadata shape", () => {
        const meta = buildMeta({
            repo: "Acme/widget",
            windowEnd: "2026-07-07",
            windowLagDays: 14,
            prState: "merged",
            matchedCcrLogin: "copilot-pull-request-reviewer[bot]",
            ccrEnabledSince: null,
        });
        expect(meta.repo).toBe("Acme/widget");
        expect(meta.windowEnd).toBe("2026-07-07");
        expect(meta.matchedCcrLogin).toBe("copilot-pull-request-reviewer[bot]");
        expect(meta.toolVersion).toBe("1.0");
    });
});

describe("prep-run orchestrator (offline fixture)", () => {
    let cache: string | undefined;
    afterEach(() => {
        if (cache) rmSync(cache, { recursive: true, force: true });
        cache = undefined;
    });

    it("runs stages and writes meta.json + prep-summary.json", () => {
        cache = mkdtempSync(join(os.tmpdir(), "ccr-prep-"));
        copyFileSync(fixture, join(cache, "pr-100.json"));

        execFileSync(
            "node",
            [
                join(scripts, "prep-run.ts"),
                "--repo",
                "Acme/widget",
                "--cache-dir",
                cache,
                "--skip-fetch",
            ],
            { stdio: ["ignore", "ignore", "pipe"] },
        );

        const summary = JSON.parse(
            readFileSync(join(cache, "prep-summary.json"), "utf8"),
        ) as {
            prCount: number;
            ccrInlineCount: number;
            duplicateRowIds: number;
            fatal: boolean;
        };
        const meta = JSON.parse(
            readFileSync(join(cache, "meta.json"), "utf8"),
        ) as { repo: string };

        expect(meta.repo).toBe("Acme/widget");
        expect(summary.prCount).toBe(1);
        expect(summary.ccrInlineCount).toBeGreaterThanOrEqual(1);
        expect(summary.duplicateRowIds).toBe(0);
        expect(summary.fatal).toBe(false);
    });
});
