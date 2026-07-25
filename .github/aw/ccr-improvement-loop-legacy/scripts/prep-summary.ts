/**
 * prep-summary.ts — deterministic audit of the normalized prep cache.
 *
 * `buildPrepSummary` is a pure function (no IO) so it can be unit-tested. It
 * cross-checks the artifacts the prep stages produced and surfaces evidence
 * gaps the agent judge would otherwise silently trip over:
 *
 *   - identity integrity: duplicate rowIds and duplicate judge-input ids are
 *     FATAL (they conflate two distinct rows); duplicate findingIds are allowed
 *     but reported, since findingId is a grouping key by design.
 *   - coverage: comment counts by authorKind, CCR counts by login and
 *     source/kind, missing diffHunk / postCommentDiff evidence, and gap
 *     candidates with no prior CCR comments.
 *   - pipeline health: classification status counts.
 */
import type {
    AttributedComment,
    JudgeInputItem,
    PullRequestData,
} from "./types.ts";

export interface PrepSummaryInput {
    prs: PullRequestData[];
    attributed: AttributedComment[];
    judgeInput: JudgeInputItem[];
    classified: {
        number: number;
        classificationStatus?: string;
    }[];
}

export interface PrepSummary {
    prCount: number;
    /** Total distinct files touched across all PRs' commit timelines. */
    prFileCount: number;
    commentCount: number;
    commentCountsByAuthorKind: Record<string, number>;
    /** CCR-authored comment counts keyed by login. */
    ccrCountsByLogin: Record<string, number>;
    /** CCR-authored comment counts keyed by `${source}:${kind}`. */
    ccrCountsBySourceKind: Record<string, number>;
    /** CCR inline comment count (the signal the methodology note depends on). */
    ccrInlineCount: number;
    duplicateRowIds: number;
    duplicateFindingIds: number;
    duplicateJudgeInputIds: number;
    judgeInputCount: number;
    missingDiffHunk: number;
    missingPostCommentDiff: number;
    gapCandidatesWithNoPriorCcr: number;
    classificationStatusCounts: Record<string, number>;
    /** True when any fatal audit check tripped. */
    fatal: boolean;
    fatalReasons: string[];
    /** Non-fatal advisories (e.g. findingId grouping collisions). */
    warnings: string[];
}

function countDuplicates<T>(items: T[], key: (item: T) => string): number {
    const seen = new Map<string, number>();
    for (const item of items) {
        const k = key(item);
        seen.set(k, (seen.get(k) ?? 0) + 1);
    }
    let dupes = 0;
    for (const n of seen.values()) if (n > 1) dupes += n - 1;
    return dupes;
}

function increment(counts: Record<string, number>, key: string): void {
    counts[key] = (counts[key] ?? 0) + 1;
}

export function buildPrepSummary(input: PrepSummaryInput): PrepSummary {
    const { prs, attributed, judgeInput, classified } = input;

    const distinctFiles = new Set<string>();
    for (const data of prs) {
        for (const commit of data.commits) {
            for (const file of commit.files) distinctFiles.add(file);
        }
    }

    const commentCountsByAuthorKind: Record<string, number> = {};
    const ccrCountsByLogin: Record<string, number> = {};
    const ccrCountsBySourceKind: Record<string, number> = {};
    let ccrInlineCount = 0;
    for (const c of attributed) {
        increment(commentCountsByAuthorKind, c.authorKind);
        if (c.authorKind === "ccr") {
            increment(ccrCountsByLogin, c.authorLogin ?? "unknown");
            increment(ccrCountsBySourceKind, `${c.source}:${c.kind}`);
            if (c.source === "inline") ccrInlineCount += 1;
        }
    }

    const classificationStatusCounts: Record<string, number> = {};
    for (const p of classified) {
        increment(
            classificationStatusCounts,
            p.classificationStatus ?? "unknown",
        );
    }

    const duplicateRowIds = countDuplicates(attributed, (c) => c.rowId);
    const duplicateFindingIds = countDuplicates(attributed, (c) => c.findingId);
    const duplicateJudgeInputIds = countDuplicates(judgeInput, (i) => i.id);

    let missingDiffHunk = 0;
    let missingPostCommentDiff = 0;
    let gapCandidatesWithNoPriorCcr = 0;
    for (const item of judgeInput) {
        if (!item.diffHunk) missingDiffHunk += 1;
        if (item.purpose === "ccr-comment" && !item.postCommentDiff) {
            missingPostCommentDiff += 1;
        }
        if (
            item.purpose === "gap-candidate" &&
            (item.ccrComments?.length ?? 0) === 0
        ) {
            gapCandidatesWithNoPriorCcr += 1;
        }
    }

    const fatalReasons: string[] = [];
    if (duplicateRowIds > 0) {
        fatalReasons.push(
            `${String(duplicateRowIds)} duplicate rowId(s) — rowId must be unique per comment`,
        );
    }
    if (duplicateJudgeInputIds > 0) {
        fatalReasons.push(
            `${String(duplicateJudgeInputIds)} duplicate judge-input id(s) — judge ids must be unique`,
        );
    }

    const warnings: string[] = [];
    if (duplicateFindingIds > 0) {
        warnings.push(
            `${String(duplicateFindingIds)} findingId grouping collision(s) — expected; findingId is a grouping key, not a row key`,
        );
    }

    return {
        prCount: prs.length,
        prFileCount: distinctFiles.size,
        commentCount: attributed.length,
        commentCountsByAuthorKind,
        ccrCountsByLogin,
        ccrCountsBySourceKind,
        ccrInlineCount,
        duplicateRowIds,
        duplicateFindingIds,
        duplicateJudgeInputIds,
        judgeInputCount: judgeInput.length,
        missingDiffHunk,
        missingPostCommentDiff,
        gapCandidatesWithNoPriorCcr,
        classificationStatusCounts,
        fatal: fatalReasons.length > 0,
        fatalReasons,
        warnings,
    };
}
