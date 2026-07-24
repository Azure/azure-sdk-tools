/**
 * pr-evidence.ts — the per-PR normalized evidence contract for the #4 fixed reads.
 *
 * This is the equivalence oracle target for the Phase 2 REST→GraphQL migration:
 * the five fixed REST reads (`pulls/{n}`, `.../reviews`, `.../comments`,
 * `issues/{n}/comments`, `pulls/{n}/commits`) plus thread `isResolved` and linked
 * issues/labels are collapsed onto one GraphQL query per PR. Whatever the SOURCE
 * (REST or GraphQL), the assembled evidence must normalize to the SAME record
 * here — so a golden snapshot of this over `pr-sample.json` (Phase 0) is what the
 * migration diffs against. It measures the normalized downstream evidence
 * contract, NOT raw REST bytes.
 *
 * Every list is deterministically ordered (by stable id/number) and author
 * identity is reduced to `login`, so ordering/shape differences between the two
 * fetch strategies cannot mask a real drift. Pure, no IO.
 */
import type {
    InlineComment,
    IssueComment,
    PullRequestData,
    ReviewSummary,
} from "./types.ts";

export interface NormalizedReview {
    id: number;
    state: string;
    body: string;
    submittedAt: string | null;
    author: string | null;
    authorAssociation: string | null;
}

export interface NormalizedInline {
    id: number;
    path: string | null;
    line: number | null;
    startLine: number | null;
    originalLine: number | null;
    body: string;
    diffHunk: string | null;
    inReplyToId: number | null;
    pullRequestReviewId: number | null;
    createdAt: string | null;
    author: string | null;
    authorAssociation: string | null;
    lineStale: boolean;
    /** Review-thread resolution state (`isResolved` in GraphQL terms). */
    threadResolved: boolean;
}

export interface NormalizedIssueComment {
    id: number;
    body: string;
    createdAt: string | null;
    author: string | null;
    authorAssociation: string | null;
}

export interface NormalizedCommit {
    sha: string;
    committedAt: string | null;
}

export interface PrEvidence {
    number: number;
    labels: string[];
    linkedIssues: { number: number; labels: string[] }[];
    reviews: NormalizedReview[];
    inline: NormalizedInline[];
    issue: NormalizedIssueComment[];
    /** Commit order (committedAt sequence) — the ordering #6/#4 depend on. */
    commitOrder: NormalizedCommit[];
}

function normReview(r: ReviewSummary): NormalizedReview {
    return {
        id: r.id,
        state: r.state,
        body: r.body,
        submittedAt: r.submittedAt,
        author: r.user?.login ?? null,
        authorAssociation: r.authorAssociation ?? null,
    };
}

function normInline(c: InlineComment): NormalizedInline {
    return {
        id: c.id,
        path: c.path ?? null,
        line: c.line ?? null,
        startLine: c.startLine ?? null,
        originalLine: c.originalLine ?? null,
        body: c.body,
        diffHunk: c.diffHunk ?? null,
        inReplyToId: c.inReplyToId ?? null,
        pullRequestReviewId: c.pullRequestReviewId ?? null,
        createdAt: c.createdAt ?? null,
        author: c.user?.login ?? null,
        authorAssociation: c.authorAssociation ?? null,
        lineStale: c.lineStale ?? false,
        threadResolved: c.threadResolved ?? false,
    };
}

function normIssue(c: IssueComment): NormalizedIssueComment {
    return {
        id: c.id,
        body: c.body,
        createdAt: c.createdAt,
        author: c.user?.login ?? null,
        authorAssociation: c.authorAssociation ?? null,
    };
}

/** Extract the deterministically-ordered #4 fixed-read evidence for one PR. */
export function extractPrEvidence(data: PullRequestData): PrEvidence {
    const byId = (a: { id: number }, b: { id: number }): number => a.id - b.id;
    return {
        number: data.pr.number,
        labels: [...(data.pr.labels ?? [])].sort(),
        linkedIssues: [...(data.pr.linkedIssues ?? [])]
            .map((li) => ({ number: li.number, labels: [...li.labels].sort() }))
            .sort((a, b) => a.number - b.number),
        reviews: [...data.reviews].map(normReview).sort(byId),
        inline: [...data.inline].map(normInline).sort(byId),
        issue: [...data.issue].map(normIssue).sort(byId),
        commitOrder: data.commits.map((c) => ({
            sha: c.sha,
            committedAt: c.committedAt,
        })),
    };
}
