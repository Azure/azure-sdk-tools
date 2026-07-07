import { describe, expect, it } from "vitest";

import {
    blameConfidenceOf,
    gradeOpportunity,
    removedPreFixRanges,
    rangesIntersect,
    resolveIntroducingPr,
    type CcrActivity,
    type LineRange,
} from "../scripts/trace-bug-origin.ts";

describe("removedPreFixRanges", () => {
    it("returns empty for a missing patch", () => {
        expect(removedPreFixRanges(undefined)).toEqual([]);
    });

    it("returns empty for a pure-addition hunk", () => {
        const patch = ["@@ -0,0 +1,2 @@", "+line a", "+line b"].join("\n");
        expect(removedPreFixRanges(patch)).toEqual([]);
    });

    it("maps a single removed line to its old-file coordinate", () => {
        const patch = [
            "@@ -10,3 +10,2 @@",
            " context",
            "-removed at 11",
            " more context",
        ].join("\n");
        // header oldStart=10: line 10 context, line 11 removed, line 12 context
        expect(removedPreFixRanges(patch)).toEqual<LineRange[]>([
            { start: 11, end: 11 },
        ]);
    });

    it("treats a -/+ change block as one contiguous old-file range", () => {
        const patch = [
            "@@ -5,4 +5,4 @@",
            " ctx",
            "-old line 6",
            "-old line 7",
            "+new line 6",
            "+new line 7",
            " ctx",
        ].join("\n");
        expect(removedPreFixRanges(patch)).toEqual<LineRange[]>([
            { start: 6, end: 7 },
        ]);
    });

    it("handles multiple hunks independently", () => {
        const patch = [
            "@@ -1,2 +1,1 @@",
            "-first",
            " ctx",
            "@@ -20,2 +19,1 @@",
            "-twentieth",
            " ctx",
        ].join("\n");
        expect(removedPreFixRanges(patch)).toEqual<LineRange[]>([
            { start: 1, end: 1 },
            { start: 20, end: 20 },
        ]);
    });

    it("flushes a removed run on a context line", () => {
        const patch = [
            "@@ -3,5 +3,4 @@",
            "-three",
            "-four",
            " five",
            "-six",
        ].join("\n");
        expect(removedPreFixRanges(patch)).toEqual<LineRange[]>([
            { start: 3, end: 4 },
            { start: 6, end: 6 },
        ]);
    });
});

describe("rangesIntersect", () => {
    it("detects overlap and non-overlap", () => {
        expect(
            rangesIntersect({ start: 1, end: 5 }, { start: 5, end: 9 }),
        ).toBe(true);
        expect(
            rangesIntersect({ start: 1, end: 4 }, { start: 5, end: 9 }),
        ).toBe(false);
    });
});

function ccr(
    inline: { path: string; range: LineRange }[],
    hasSummary = false,
): CcrActivity {
    return { inline, hasSummary };
}

describe("gradeOpportunity ladder", () => {
    const ranges: LineRange[] = [{ start: 10, end: 12 }];

    it("ccrCommentedOnLines: caught, not a miss", () => {
        const v = gradeOpportunity(
            "a.ts",
            ranges,
            ccr([{ path: "a.ts", range: { start: 11, end: 11 } }]),
        );
        expect(v.ccrOpportunity).toBe("ccrCommentedOnLines");
        expect(v.verifiedMiss).toBe(false);
        expect(v.ccrCommentedOnLines).toBe(true);
    });

    it("ccrCommentedOnFile: inline on file but off the lines is a miss", () => {
        const v = gradeOpportunity(
            "a.ts",
            ranges,
            ccr([{ path: "a.ts", range: { start: 50, end: 50 } }]),
        );
        expect(v.ccrOpportunity).toBe("ccrCommentedOnFile");
        expect(v.verifiedMiss).toBe(true);
    });

    it("ccrActiveOnPr: inline on another file is a miss", () => {
        const v = gradeOpportunity(
            "a.ts",
            ranges,
            ccr([{ path: "other.ts", range: { start: 1, end: 1 } }]),
        );
        expect(v.ccrOpportunity).toBe("ccrActiveOnPr");
        expect(v.verifiedMiss).toBe(true);
    });

    it("ccrSummaryOnly: summary without inline is NOT a miss", () => {
        const v = gradeOpportunity("a.ts", ranges, ccr([], true));
        expect(v.ccrOpportunity).toBe("ccrSummaryOnly");
        expect(v.verifiedMiss).toBe(false);
        expect(v.ccrActiveOnIntroducingPr).toBe(false);
    });

    it("ccrInactive: no activity is NOT a miss", () => {
        const v = gradeOpportunity("a.ts", ranges, ccr([], false));
        expect(v.ccrOpportunity).toBe("ccrInactive");
        expect(v.verifiedMiss).toBe(false);
    });
});

describe("resolveIntroducingPr", () => {
    it("resolves a single PR", () => {
        expect(resolveIntroducingPr([42, 42])).toEqual({
            introducedByPr: 42,
            traceOutcome: "resolved",
        });
    });

    it("is ambiguous across multiple PRs", () => {
        expect(resolveIntroducingPr([42, 43])).toEqual({
            introducedByPr: null,
            traceOutcome: "ambiguous-multiple-prs",
        });
    });

    it("is unresolved with no PR", () => {
        expect(resolveIntroducingPr([])).toEqual({
            introducedByPr: null,
            traceOutcome: "unresolved-no-pr",
        });
    });
});

describe("blameConfidenceOf", () => {
    it("is high for a single commit + author", () => {
        expect(blameConfidenceOf(1, 1)).toBe("high");
    });
    it("is medium for multiple commits, one author", () => {
        expect(blameConfidenceOf(3, 1)).toBe("medium");
    });
    it("is low for multiple authors", () => {
        expect(blameConfidenceOf(3, 2)).toBe("low");
    });
});
