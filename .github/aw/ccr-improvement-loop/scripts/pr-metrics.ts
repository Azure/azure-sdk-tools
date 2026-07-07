/**
 * pr-metrics.ts — deterministic PR-level derived fields used by the metrics:
 * `ccrReviewed`, `cycleTimeHours`, and `iterations`. Kept pure + unit-tested so
 * the metric definitions are unambiguous.
 *
 * `iterations` is defined precisely as the number of commits authored after the
 * first human-or-CCR review event. A PR with no review (or no later commits)
 * reports 0; a squash-merged PR reports its pre-merge commit count after the
 * first review (typically 0 if the review came after the single commit).
 */
import type { PrType, PullRequestData } from "./types.ts";
import { hoursBetween } from "./utils.ts";

export interface PrRowOut {
    number: number;
    url: string;
    title: string;
    author: string | null;
    additions: number | null;
    deletions: number | null;
    createdAt: string | null;
    mergedAt: string | null;
    prType: PrType | null;
    prTypeSource: "label" | "title" | "issue" | "agent" | "unknown";
    classificationStatus: "complete" | "needs-agent" | "failed";
    ccrReviewed: boolean;
    cycleTimeHours: number | null;
    iterations: number;
}

function ts(value: string | null | undefined): number | null {
    if (!value) return null;
    const n = Date.parse(value);
    return Number.isNaN(n) ? null : n;
}

/** Earliest human-or-CCR review event timestamp (ms), or null if none. */
export function firstReviewEventMs(data: PullRequestData): number | null {
    const candidates: number[] = [];
    for (const r of data.reviews) {
        const t = ts(r.submittedAt);
        if (t != null) candidates.push(t);
    }
    for (const c of data.inline) {
        const t = ts(c.createdAt);
        if (t != null) candidates.push(t);
    }
    if (candidates.length === 0) return null;
    return Math.min(...candidates);
}

export function computeIterations(data: PullRequestData): number {
    const firstReview = firstReviewEventMs(data);
    if (firstReview == null) return 0;
    return data.commits.filter((c) => {
        const t = ts(c.committedAt);
        return t != null && t > firstReview;
    }).length;
}

export function isCcrLogin(
    login: string | undefined,
    ccrLogins: string[],
): boolean {
    if (!login) return false;
    const lower = login.toLowerCase();
    return ccrLogins.some((l) => l.toLowerCase() === lower);
}

export function computeCcrReviewed(
    data: PullRequestData,
    ccrLogins: string[],
): boolean {
    const inInline = data.inline.some((c) =>
        isCcrLogin(c.user?.login, ccrLogins),
    );
    const inReviews = data.reviews.some((r) =>
        isCcrLogin(r.user?.login, ccrLogins),
    );
    return inInline || inReviews;
}

export interface Classification {
    prType: PrType | null;
    prTypeSource: PrRowOut["prTypeSource"];
    classificationStatus: PrRowOut["classificationStatus"];
}

export function derivePrRow(
    data: PullRequestData,
    classification: Classification,
    ccrLogins: string[],
): PrRowOut {
    const pr = data.pr;
    return {
        number: pr.number,
        url: pr.url,
        title: pr.title,
        author: pr.author?.login ?? null,
        additions: pr.additions ?? null,
        deletions: pr.deletions ?? null,
        createdAt: pr.createdAt,
        mergedAt: pr.mergedAt,
        prType: classification.prType,
        prTypeSource: classification.prTypeSource,
        classificationStatus: classification.classificationStatus,
        ccrReviewed: computeCcrReviewed(data, ccrLogins),
        cycleTimeHours: hoursBetween(pr.createdAt, pr.mergedAt),
        iterations: computeIterations(data),
    };
}
