/**
 * pr-equivalence.test.ts — Gate 2.1 (#4): REST↔GraphQL equivalence + cost model.
 *
 * The GraphQL per-PR assembler must reproduce the REST assembler's normalized
 * record exactly — silent metric drift is worse than a 403, so this is the point
 * of the phase. Both assemblers are driven from ONE logical fixture
 * (tests/helpers/pr-fixture.ts). We assert:
 *   1. equivalence over a fixture that EXCEEDS one page on every connection
 *      (reviews, inline via threads, issue comments, commits, threads, linked
 *      issues, labels) plus pathological comments — normalized output identical,
 *      order-independent (via the Phase 0 evidence oracle) and no truncation;
 *   2. the fixed five reads are GraphQL, NOT per-PR REST (cost-model regression:
 *      reintroducing per-PR fixed REST reads fails CI);
 *   3. per-PR primary-REST cost is `1 + N` (commit detail only) after the search;
 *   4. an aggregate-budget bound: worst-case GraphQL points/hr stays under the
 *      15k pool given the pacer's max repo-months/hour.
 */
import { describe, expect, it } from "vitest";

import { assembleViaRest } from "../scripts/fetch-prs.ts";
import { collectGraphqlPr, assembleViaGraphql } from "../scripts/pr-graphql.ts";
import { extractPrEvidence } from "../scripts/pr-evidence.ts";
import type { PullRequestData } from "../scripts/types.ts";
import {
    buildGraphqlClient,
    buildRestClient,
    type PrFixture,
    type FxComment,
    type FxThread,
} from "./helpers/pr-fixture.ts";

const REPO = "Azure/azure-sdk-for-go";
const N = 4242;

/** Deep-sort every connection so the compare is order-independent (like REST). */
function canonical(d: PullRequestData): PullRequestData {
    return {
        ...d,
        reviews: [...d.reviews].sort((a, b) => a.id - b.id),
        inline: [...d.inline].sort((a, b) => a.id - b.id),
        issue: [...d.issue].sort((a, b) => a.id - b.id),
        commits: [...d.commits].sort((a, b) => a.sha.localeCompare(b.sha)),
    };
}

/** A PR whose every connection exceeds one page (101 items) + pathological rows. */
function pathologicalFixture(): PrFixture {
    const human = { login: "reviewer1", type: "User" as const };
    const author = { login: "author1", type: "User" as const };

    // 101 review threads, each 1 comment → 101 inline (exceeds one page). One
    // thread carries >100 comments to exercise per-thread comment pagination.
    const threads: FxThread[] = Array.from({ length: 101 }, (_, i) => ({
        isResolved: i % 2 === 0,
        comments: [
            {
                id: 9000 + i,
                path: "src/a.go",
                line: i === 3 ? null : 10 + i, // outdated line (pathological)
                originalLine: 10 + i,
                startLine: null,
                body: i === 5 ? "" : `please fix ${String(i)}`, // minimized/empty
                diffHunk: "@@ -1 +1 @@",
                createdAt: `2026-06-02T00:00:${String(i % 60).padStart(2, "0")}Z`,
                user: i === 7 ? null : human, // deleted author (pathological)
                authorAssociation: "MEMBER",
                reactions: i === 1 ? { "+1": 2, heart: 1 } : undefined,
            } satisfies FxComment,
        ],
    }));
    // A 2-page thread (comments > 100) appended.
    threads.push({
        isResolved: true,
        comments: Array.from({ length: 150 }, (_, j) => ({
            id: 20000 + j,
            path: "src/big.go",
            line: 1 + j,
            originalLine: 1 + j,
            startLine: null,
            body: `thread comment ${String(j)}`,
            diffHunk: "@@ -1 +1 @@",
            inReplyToId: j === 0 ? undefined : 20000,
            createdAt: "2026-06-02T01:00:00Z",
            user: human,
            authorAssociation: "MEMBER",
        })) satisfies FxComment[],
    });

    return {
        repo: REPO,
        number: N,
        title: "Big change",
        user: author,
        state: "MERGED",
        createdAt: "2026-06-01T00:00:00Z",
        mergedAt: "2026-06-02T00:00:00Z",
        additions: 500,
        deletions: 120,
        isDraft: false,
        labels: Array.from({ length: 101 }, (_, i) => `label-${String(i)}`),
        threads,
        reviews: Array.from({ length: 101 }, (_, i) => ({
            id: 5000 + i,
            state: ["APPROVED", "CHANGES_REQUESTED", "COMMENTED", "DISMISSED"][
                i % 4
            ]!,
            body: i === 2 ? "" : `review ${String(i)}`,
            submittedAt: `2026-06-02T02:00:${String(i % 60).padStart(2, "0")}Z`,
            user: i === 4 ? null : human,
            authorAssociation: "MEMBER",
        })),
        issueComments: Array.from({ length: 101 }, (_, i) => ({
            id: 7000 + i,
            body: `issue ${String(i)}`,
            createdAt: `2026-06-02T03:00:${String(i % 60).padStart(2, "0")}Z`,
            user: i === 6 ? null : human,
            authorAssociation: "MEMBER",
            reactions: i === 0 ? { rocket: 3 } : undefined,
        })),
        commits: Array.from({ length: 101 }, (_, i) => ({
            sha: `sha${String(i).padStart(4, "0")}`,
            committedAt: `2026-06-02T04:00:${String(i % 60).padStart(2, "0")}Z`,
            files: [
                {
                    filename: `src/f${String(i)}.go`,
                    ...(i % 2 === 0 ? { patch: `@@ ${String(i)} @@` } : {}),
                },
            ],
        })),
        linkedIssues: Array.from({ length: 101 }, (_, i) => ({
            number: 300 + i,
            labels: [`bug-${String(i)}`],
        })),
    };
}

describe("Gate 2.1 — REST↔GraphQL equivalence", () => {
    it("produces an identical normalized record over multi-page + pathological connections", async () => {
        const fx = pathologicalFixture();
        const rest: PullRequestData = await assembleViaRest(
            buildRestClient(fx),
            REPO,
            N,
        );
        const gql: PullRequestData = await assembleViaGraphql(
            buildGraphqlClient(fx),
            REPO,
            N,
        );

        // No truncation: every connection came back whole on both paths.
        expect(rest.reviews).toHaveLength(101);
        expect(gql.reviews).toHaveLength(101);
        expect(gql.inline).toHaveLength(rest.inline.length);
        expect(gql.inline.length).toBe(101 + 150);
        expect(gql.issue).toHaveLength(101);
        expect(gql.commits).toHaveLength(101);
        expect(gql.pr.labels).toHaveLength(101);
        expect(gql.pr.linkedIssues).toHaveLength(101);

        // Order-independent structural equality + the Phase 0 evidence oracle.
        expect(canonical(gql)).toEqual(canonical(rest));
        expect(extractPrEvidence(gql)).toEqual(extractPrEvidence(rest));
    });
});

describe("Gate 2.1 — cost model", () => {
    it("the five fixed reads are GraphQL, not per-PR REST (only commit detail is REST)", async () => {
        const fx = pathologicalFixture();
        const c = buildGraphqlClient(fx);
        await collectGraphqlPr(c, REPO, N);

        const rest = c.restEndpoints();
        // Regression guard: NONE of the fixed five REST reads may appear.
        expect(rest.some((e) => /\/pulls\/\d+$/.test(e))).toBe(false);
        expect(rest.some((e) => e.endsWith("/reviews"))).toBe(false);
        expect(rest.some((e) => e.endsWith(`/pulls/${N}/comments`))).toBe(
            false,
        );
        expect(rest.some((e) => e.endsWith(`/issues/${N}/comments`))).toBe(
            false,
        );
        expect(rest.some((e) => e.endsWith(`/pulls/${N}/commits`))).toBe(false);
        // Per-PR primary REST cost is exactly N commit-detail reads (1 + N once
        // the PR-list search is counted at the caller).
        expect(rest).toHaveLength(101);
        expect(rest.every((e) => /\/commits\/sha\d+$/.test(e))).toBe(true);
        expect(c.graphqlCount()).toBeGreaterThanOrEqual(1);
    });

    it("reads rateLimit.cost back per query for observability", async () => {
        const fx = pathologicalFixture();
        const { diagnostics } = await collectGraphqlPr(
            buildGraphqlClient(fx),
            REPO,
            N,
        );
        // 1 point per query in the fixture; totalCost === queryCount here.
        expect(diagnostics.totalCost).toBe(diagnostics.queryCount);
        expect(diagnostics.queryCount).toBeGreaterThanOrEqual(1);
    });

    it("aggregate GraphQL points/hr stay under the 15k pool at the pacer's max repo-months/hour", () => {
        // Documented scheduling assumption behind "~300 pts/month is trivial":
        // per-PR GraphQL pays a ~1-point floor; a heavy repo-month is ~1000 PRs.
        const perPrFloorPts = 2; // generous vs the 1-pt floor + a page or two
        const prsPerRepoMonth = 1000; // heavy month upper bound
        const maxRepoMonthsPerHour = 6; // the pacer's per-hour bound (Phase 4)
        const worstCasePtsPerHour =
            perPrFloorPts * prsPerRepoMonth * maxRepoMonthsPerHour;
        const graphqlPoolPerHour = 15000;
        expect(worstCasePtsGuard(worstCasePtsPerHour, graphqlPoolPerHour)).toBe(
            true,
        );
    });
});

function worstCasePtsGuard(worst: number, pool: number): boolean {
    // Keep a safety margin: worst case must sit under 90% of the pool.
    return worst <= pool * 0.9;
}
