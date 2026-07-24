/**
 * pr-assemble.test.ts — Gate 2.0 for the injectable request layer.
 *
 * `assembleViaRest` now issues every read through an injected {@link GhClient}.
 * These tests pin the current (pre-#4) request pattern via an in-process
 * {@link makeRecordingClient}: the five fixed REST reads, one GraphQL thread
 * read, and N per-commit REST reads — the exact shape Step 2.1 will change (and
 * whose change the cost-model regression test then guards). The assembled record
 * is also asserted field-for-field so the extraction stayed behavior-preserving.
 */
import { describe, expect, it } from "vitest";

import { assembleViaRest } from "../scripts/fetch-prs.ts";
import { makeRecordingClient } from "../scripts/gh-client.ts";
import type { PullRequestData } from "../scripts/types.ts";

const REPO = "Azure/azure-sdk-for-go";
const N = 100;

/** A REST fixture set for PR #100 with two commits and mixed comments. */
function restResponders() {
    const base = `repos/${REPO}`;
    const map: Record<string, unknown> = {
        [`${base}/pulls/${N}`]: {
            number: N,
            title: "Fix retry backoff",
            user: { login: "author1", type: "User" },
            html_url: `https://github.com/${REPO}/pull/${N}`,
            state: "closed",
            draft: false,
            additions: 12,
            deletions: 3,
            created_at: "2026-06-02T00:00:00Z",
            merged_at: "2026-06-03T00:00:00Z",
            labels: [{ name: "bug" }, { name: "go" }],
        },
        [`${base}/pulls/${N}/reviews`]: [
            {
                id: 5001,
                state: "APPROVED",
                body: "LGTM",
                submitted_at: "2026-06-02T10:00:00Z",
                user: { login: "reviewer1", type: "User" },
                author_association: "MEMBER",
            },
        ],
        [`${base}/pulls/${N}/comments`]: [
            {
                id: 9001,
                path: "retry.go",
                line: 42,
                original_line: 42,
                body: "Guard against negative delay here.",
                diff_hunk: "@@ -40,3 +40,5 @@",
                created_at: "2026-06-02T10:05:00Z",
                user: { login: "reviewer1", type: "User" },
                author_association: "MEMBER",
                reactions: { total_count: 1, "+1": 1 },
            },
            {
                id: 9002,
                path: "retry.go",
                line: 50,
                original_line: 50,
                body: "Thanks, fixed.",
                diff_hunk: "@@ -48,3 +48,5 @@",
                in_reply_to_id: 9001,
                created_at: "2026-06-02T11:00:00Z",
                user: { login: "author1", type: "User" },
                author_association: "OWNER",
            },
        ],
        [`${base}/issues/${N}/comments`]: [
            {
                id: 7001,
                body: "Please also add a test.",
                created_at: "2026-06-02T09:00:00Z",
                user: { login: "reviewer1", type: "User" },
                author_association: "MEMBER",
            },
        ],
        [`${base}/pulls/${N}/commits`]: [
            {
                sha: "aaa111",
                commit: { committer: { date: "2026-06-02T08:00:00Z" } },
            },
            {
                sha: "bbb222",
                commit: { committer: { date: "2026-06-02T12:00:00Z" } },
            },
        ],
        [`${base}/commits/aaa111`]: {
            files: [{ filename: "retry.go", patch: "@@ +1 @@" }],
        },
        [`${base}/commits/bbb222`]: {
            files: [{ filename: "retry_test.go" }],
        },
    };
    return map;
}

/** GraphQL thread response: comment 9001 sits in a resolved thread. */
const threadGraphql = {
    data: {
        repository: {
            pullRequest: {
                reviewThreads: {
                    nodes: [
                        {
                            isResolved: true,
                            comments: { nodes: [{ databaseId: 9001 }] },
                        },
                    ],
                },
                closingIssuesReferences: {
                    nodes: [
                        {
                            number: 555,
                            labels: { nodes: [{ name: "bug" }] },
                        },
                    ],
                },
            },
        },
    },
};

function client() {
    const map = restResponders();
    return makeRecordingClient({
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        rest: <T>(endpoint: string): T => {
            if (!(endpoint in map))
                throw new Error(`unexpected endpoint ${endpoint}`);
            return map[endpoint] as T;
        },
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        graphql: <T>(): T => threadGraphql as T,
    });
}

describe("Gate 2.0 — assembleViaRest request pattern", () => {
    it("issues exactly the five fixed REST reads + 1 GraphQL + N per-commit reads", async () => {
        const c = client();
        await assembleViaRest(c, REPO, N);

        const rest = c.restEndpoints();
        const base = `repos/${REPO}`;
        // Five fixed reads.
        expect(rest).toContain(`${base}/pulls/${N}`);
        expect(rest).toContain(`${base}/pulls/${N}/reviews`);
        expect(rest).toContain(`${base}/pulls/${N}/comments`);
        expect(rest).toContain(`${base}/issues/${N}/comments`);
        expect(rest).toContain(`${base}/pulls/${N}/commits`);
        // N=2 per-commit detail reads.
        expect(rest).toContain(`${base}/commits/aaa111`);
        expect(rest).toContain(`${base}/commits/bbb222`);
        // Fixed(5) + per-commit(2) = 7 REST; exactly 1 GraphQL.
        expect(rest).toHaveLength(7);
        expect(c.graphqlCount()).toBe(1);
    });

    it("assembles the normalized record with joined thread resolution and linked issues", async () => {
        const c = client();
        const data: PullRequestData = await assembleViaRest(c, REPO, N);

        expect(data.pr.number).toBe(N);
        expect(data.pr.labels).toEqual(["bug", "go"]);
        expect(data.pr.linkedIssues).toEqual([
            { number: 555, labels: ["bug"] },
        ]);
        expect(data.reviews).toHaveLength(1);
        expect(data.reviews[0]?.state).toBe("APPROVED");
        // Thread resolution joined to the first comment (9001) only.
        const c9001 = data.inline.find((i) => i.id === 9001);
        const c9002 = data.inline.find((i) => i.id === 9002);
        expect(c9001?.threadResolved).toBe(true);
        expect(c9002?.threadResolved).toBe(false);
        expect(c9001?.reactions).toEqual([{ content: "+1", user: null }]);
        expect(data.issue).toHaveLength(1);
        // Commit detail: patch retained where present, order preserved.
        expect(data.commits.map((x) => x.sha)).toEqual(["aaa111", "bbb222"]);
        expect(data.commits[0]?.patches).toEqual({ "retry.go": "@@ +1 @@" });
        expect(data.commits[1]?.patches).toBeUndefined();
    });

    it("runs the independent reads concurrently (peak concurrency > 1)", async () => {
        const c = client();
        await assembleViaRest(c, REPO, N);
        expect(c.peakConcurrency()).toBeGreaterThan(1);
    });
});
