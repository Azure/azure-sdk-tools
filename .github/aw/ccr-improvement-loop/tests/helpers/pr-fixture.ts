/**
 * pr-fixture.ts — one logical PR fixture that renders into BOTH the REST reads
 * `assembleViaRest` consumes and the GraphQL responses `assembleViaGraphql`
 * consumes, so Gate 2.1 can prove the two assemblers produce the same normalized
 * record. Because both views derive from a single source of truth, any mismatch
 * the equivalence test reports is a real normalization drift, not a fixture skew.
 *
 * Connections can be made to exceed one page (>100) to exercise per-connection
 * cursor pagination, and individual comments/threads can be marked pathological
 * (deleted author, minimized/empty body, outdated line, mixed review states).
 *
 * Both client builders wrap {@link makeRecordingClient} so tests can also assert
 * the request SHAPE (REST-vs-GraphQL, call counts, summed GraphQL cost).
 */
import {
    makeRecordingClient,
    type RecordingClient,
} from "../../scripts/gh-client.ts";

export interface FxUser {
    login: string;
    type: "User" | "Bot";
}
export interface FxReview {
    id: number;
    state: string;
    body: string;
    submittedAt: string | null;
    user: FxUser | null;
    authorAssociation?: string;
}
export interface FxComment {
    id: number;
    path?: string;
    line?: number | null;
    startLine?: number | null;
    originalLine?: number | null;
    body: string;
    diffHunk?: string;
    inReplyToId?: number;
    reviewId?: number;
    createdAt?: string;
    user: FxUser | null;
    authorAssociation?: string;
    /** REST-style reaction summary, e.g. { "+1": 2, heart: 1 }. */
    reactions?: Record<string, number>;
}
export interface FxThread {
    isResolved: boolean;
    comments: FxComment[];
}
export interface FxIssueComment {
    id: number;
    body: string;
    createdAt: string;
    user: FxUser | null;
    authorAssociation?: string;
    reactions?: Record<string, number>;
}
export interface FxCommit {
    sha: string;
    committedAt: string | null;
    files: { filename: string; patch?: string }[];
}
export interface FxLinkedIssue {
    number: number;
    labels: string[];
}
export interface PrFixture {
    repo: string;
    number: number;
    title: string;
    user: FxUser | null;
    state: "OPEN" | "CLOSED" | "MERGED";
    createdAt: string | null;
    mergedAt: string | null;
    additions: number;
    deletions: number;
    isDraft: boolean;
    labels: string[];
    reviews: FxReview[];
    threads: FxThread[];
    issueComments: FxIssueComment[];
    commits: FxCommit[];
    linkedIssues: FxLinkedIssue[];
}

const PAGE = 100;

// REST reaction summary key → GraphQL reactionGroup content enum.
const REACTION_ENUM: Record<string, string> = {
    "+1": "THUMBS_UP",
    "-1": "THUMBS_DOWN",
    laugh: "LAUGH",
    hooray: "HOORAY",
    confused: "CONFUSED",
    heart: "HEART",
    rocket: "ROCKET",
    eyes: "EYES",
};

function reactionGroups(
    summary: Record<string, number> | undefined,
): { content: string; users: { totalCount: number } }[] {
    return Object.entries(summary ?? {}).map(([k, n]) => ({
        content: REACTION_ENUM[k] ?? k,
        users: { totalCount: n },
    }));
}

function page<T>(
    arr: T[],
    offset: number,
): {
    nodes: T[];
    pageInfo: { hasNextPage: boolean; endCursor: string | null };
} {
    const nodes = arr.slice(offset, offset + PAGE);
    const nextOffset = offset + PAGE;
    return {
        nodes,
        pageInfo: {
            hasNextPage: nextOffset < arr.length,
            endCursor: nextOffset < arr.length ? String(nextOffset) : null,
        },
    };
}

function actor(u: FxUser | null): { login: string; __typename: string } | null {
    return u ? { login: u.login, __typename: u.type } : null;
}

// ---------------------------------------------------------------------------
// GraphQL node builders.
// ---------------------------------------------------------------------------

function gqlReview(r: FxReview): unknown {
    return {
        databaseId: r.id,
        state: r.state,
        body: r.body,
        submittedAt: r.submittedAt,
        authorAssociation: r.authorAssociation ?? null,
        author: actor(r.user),
    };
}
function gqlThreadComment(c: FxComment): unknown {
    return {
        databaseId: c.id,
        path: c.path ?? null,
        line: c.line ?? null,
        startLine: c.startLine ?? null,
        originalLine: c.originalLine ?? null,
        body: c.body,
        diffHunk: c.diffHunk ?? null,
        createdAt: c.createdAt ?? null,
        authorAssociation: c.authorAssociation ?? null,
        author: actor(c.user),
        pullRequestReview: c.reviewId ? { databaseId: c.reviewId } : null,
        replyTo: c.inReplyToId ? { databaseId: c.inReplyToId } : null,
        reactionGroups: reactionGroups(c.reactions),
    };
}
function gqlThread(t: FxThread, idx: number): unknown {
    return {
        id: `thread:${idx}`,
        isResolved: t.isResolved,
        comments: page(t.comments.map(gqlThreadComment), 0),
    };
}
function gqlIssueComment(c: FxIssueComment): unknown {
    return {
        databaseId: c.id,
        body: c.body,
        createdAt: c.createdAt,
        authorAssociation: c.authorAssociation ?? null,
        author: actor(c.user),
        reactionGroups: reactionGroups(c.reactions),
    };
}
function gqlCommit(c: FxCommit): unknown {
    return {
        commit: {
            oid: c.sha,
            committedDate: c.committedAt,
            authoredDate: null,
        },
    };
}
function gqlLinkedIssue(i: FxLinkedIssue): unknown {
    return {
        number: i.number,
        labels: page(
            i.labels.map((name) => ({ name })),
            0,
        ),
    };
}

/** Route a GraphQL request (base, focused pagination, or node-comments) to a page. */
function graphqlResponse(fx: PrFixture, fields: string[]): unknown {
    const query = fieldValue(fields, "query");
    const op = /# op:(\w+)/.exec(query)?.[1] ?? "base";
    const after = Number(fieldValue(fields, "after") || "0");

    if (op === "base") {
        const pr = fx.threads.map((t, i) => gqlThread(t, i));
        return {
            data: {
                rateLimit: { cost: 1 },
                repository: {
                    pullRequest: {
                        number: fx.number,
                        title: fx.title,
                        url: `https://github.com/${fx.repo}/pull/${fx.number}`,
                        state: fx.state,
                        createdAt: fx.createdAt,
                        mergedAt: fx.mergedAt,
                        additions: fx.additions,
                        deletions: fx.deletions,
                        isDraft: fx.isDraft,
                        author: actor(fx.user),
                        labels: page(
                            fx.labels.map((name) => ({ name })),
                            0,
                        ),
                        reviews: page(fx.reviews.map(gqlReview), 0),
                        comments: page(
                            fx.issueComments.map(gqlIssueComment),
                            0,
                        ),
                        commits: page(fx.commits.map(gqlCommit), 0),
                        reviewThreads: page(pr, 0),
                        closingIssuesReferences: page(
                            fx.linkedIssues.map(gqlLinkedIssue),
                            0,
                        ),
                    },
                },
            },
        };
    }
    if (op === "threadComments") {
        const id = fieldValue(fields, "id");
        const idx = Number(id.split(":")[1]);
        const comments = fx.threads[idx]?.comments.map(gqlThreadComment) ?? [];
        return {
            data: {
                rateLimit: { cost: 1 },
                node: { comments: page(comments, after) },
            },
        };
    }
    // Focused top-level connection pagination.
    const conn: Record<string, { field: string; nodes: unknown[] }> = {
        labels: { field: "labels", nodes: fx.labels.map((name) => ({ name })) },
        reviews: { field: "reviews", nodes: fx.reviews.map(gqlReview) },
        issueComments: {
            field: "comments",
            nodes: fx.issueComments.map(gqlIssueComment),
        },
        commits: { field: "commits", nodes: fx.commits.map(gqlCommit) },
        reviewThreads: {
            field: "reviewThreads",
            nodes: fx.threads.map((t, i) => gqlThread(t, i)),
        },
        closingIssues: {
            field: "closingIssuesReferences",
            nodes: fx.linkedIssues.map(gqlLinkedIssue),
        },
    };
    const spec = conn[op];
    if (!spec) throw new Error(`pr-fixture: unknown graphql op ${op}`);
    return {
        data: {
            rateLimit: { cost: 1 },
            repository: {
                pullRequest: { [spec.field]: page(spec.nodes, after) },
            },
        },
    };
}

function fieldValue(fields: string[], key: string): string {
    for (const f of fields) {
        if (f.startsWith(`${key}=`)) return f.slice(key.length + 1);
    }
    return "";
}

// ---------------------------------------------------------------------------
// REST responders.
// ---------------------------------------------------------------------------

function restMap(fx: PrFixture): Record<string, unknown> {
    const base = `repos/${fx.repo}`;
    const allInline = fx.threads.flatMap((t) => t.comments);
    const map: Record<string, unknown> = {
        [`${base}/pulls/${fx.number}`]: {
            number: fx.number,
            title: fx.title,
            user: fx.user,
            html_url: `https://github.com/${fx.repo}/pull/${fx.number}`,
            state: fx.state === "OPEN" ? "open" : "closed",
            draft: fx.isDraft,
            additions: fx.additions,
            deletions: fx.deletions,
            created_at: fx.createdAt,
            merged_at: fx.mergedAt,
            labels: fx.labels.map((name) => ({ name })),
        },
        [`${base}/pulls/${fx.number}/reviews`]: fx.reviews.map((r) => ({
            id: r.id,
            state: r.state,
            body: r.body,
            submitted_at: r.submittedAt,
            user: r.user,
            author_association: r.authorAssociation,
        })),
        // REST flat inline list — deliberately id-sorted so it differs from the
        // GraphQL thread-flatten order (proves order-independent equivalence).
        [`${base}/pulls/${fx.number}/comments`]: [...allInline]
            .sort((a, b) => a.id - b.id)
            .map((c) => ({
                id: c.id,
                path: c.path,
                line: c.line ?? undefined,
                start_line: c.startLine ?? null,
                original_line: c.originalLine ?? null,
                body: c.body,
                diff_hunk: c.diffHunk,
                in_reply_to_id: c.inReplyToId,
                pull_request_review_id: c.reviewId,
                created_at: c.createdAt,
                user: c.user,
                author_association: c.authorAssociation,
                reactions: c.reactions ?? {},
            })),
        [`${base}/issues/${fx.number}/comments`]: fx.issueComments.map((c) => ({
            id: c.id,
            body: c.body,
            created_at: c.createdAt,
            user: c.user,
            author_association: c.authorAssociation,
            reactions: c.reactions ?? {},
        })),
        [`${base}/pulls/${fx.number}/commits`]: fx.commits.map((c) => ({
            sha: c.sha,
            commit: { committer: { date: c.committedAt } },
        })),
    };
    for (const c of fx.commits) {
        map[`${base}/commits/${c.sha}`] = { files: c.files };
    }
    return map;
}

/** The GraphQL thread-resolution query the REST path issues (first comment only). */
function restThreadGraphql(fx: PrFixture): unknown {
    return {
        data: {
            repository: {
                pullRequest: {
                    reviewThreads: {
                        nodes: fx.threads.map((t) => ({
                            isResolved: t.isResolved,
                            comments: {
                                nodes: t.comments[0]
                                    ? [{ databaseId: t.comments[0].id }]
                                    : [],
                            },
                        })),
                    },
                    closingIssuesReferences: {
                        nodes: fx.linkedIssues.map((i) => ({
                            number: i.number,
                            labels: {
                                nodes: i.labels.map((name) => ({ name })),
                            },
                        })),
                    },
                },
            },
        },
    };
}

/** RecordingClient serving PR #n via the five REST reads + thread GraphQL. */
export function buildRestClient(fx: PrFixture): RecordingClient {
    const map = restMap(fx);
    return makeRecordingClient({
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        rest: <T>(endpoint: string): T => {
            if (!(endpoint in map))
                throw new Error(`pr-fixture(REST): no endpoint ${endpoint}`);
            return map[endpoint] as T;
        },
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        graphql: <T>(): T => restThreadGraphql(fx) as T,
    });
}

/** RecordingClient serving PR #n via the GraphQL query + REST commit detail. */
export function buildGraphqlClient(fx: PrFixture): RecordingClient {
    const map = restMap(fx);
    return makeRecordingClient({
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        rest: <T>(endpoint: string): T => {
            // Only commit detail is REST in the GraphQL path.
            if (!(endpoint in map) || !endpoint.includes("/commits/"))
                throw new Error(
                    `pr-fixture(GraphQL): unexpected REST ${endpoint}`,
                );
            return map[endpoint] as T;
        },
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T mirrors the GhClient contract
        graphql: <T>(fields: string[]): T => graphqlResponse(fx, fields) as T,
    });
}
