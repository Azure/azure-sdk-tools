/**
 * pr-graphql.ts — #4 (T2): the five fixed REST reads + thread resolution/linked
 * issues collapsed into **one GraphQL query per PR** on the separate GraphQL
 * pool, admitted through the same {@link GhClient} (and thus the same
 * `ghRequestLimiter`, #5).
 *
 * Equivalence-by-construction: the GraphQL nodes are converted back into the
 * exact `Raw*` shapes `fetch-prs.ts` already normalizes, then handed to the SAME
 * `buildPrPayload`. Only the reaction encoding differs (GraphQL `reactionGroups`
 * vs REST summary object), and it is converted to a REST-style summary so
 * `mapReactions` produces byte-identical output. Commit patch text stays on REST
 * (`fetchCommitDetail`); removing that `N` term is #6/T3, deferred.
 *
 * Every nested connection carries its OWN cursor and is looped to exhaustion
 * (GraphQL connections cap at 100), matching REST with no truncation. Partial
 * responses (`data` + `errors`) are handled explicitly: a null `pullRequest`
 * rejects; field-level errors alongside a present PR are accepted (best-effort)
 * and surfaced in the returned diagnostics. `rateLimit.cost` is read back per
 * query for observability and the Phase 2 cost-model / budget assertions.
 *
 * The query builder accepts a single PR number today but is shaped so a bounded
 * alias set (10–25 PRs, #7/T2-batch) can be added later behind the same seam
 * without reworking the normalization — that micro-batch seam is intentional.
 */
import {
    buildPrPayload,
    fetchCommitDetail,
    type RawInline,
    type RawIssue,
    type RawPrFull,
    type RawReview,
    type RawUser,
} from "./fetch-prs.ts";
import type { GhClient } from "./gh-client.ts";
import type { PullRequestData } from "./types.ts";

// ---------------------------------------------------------------------------
// GraphQL node shapes (only the fields we read).
// ---------------------------------------------------------------------------

interface PageInfo {
    hasNextPage: boolean;
    endCursor: string | null;
}
interface Connection<T> {
    nodes: T[];
    pageInfo: PageInfo;
}
interface GqlActor {
    login: string;
    __typename?: string;
}
interface GqlReactionGroup {
    content: string;
    users: { totalCount: number };
}
interface GqlReview {
    databaseId: number | null;
    state: string;
    body: string | null;
    submittedAt: string | null;
    authorAssociation: string | null;
    author: GqlActor | null;
}
interface GqlThreadComment {
    databaseId: number | null;
    path: string | null;
    line: number | null;
    startLine: number | null;
    originalLine: number | null;
    body: string | null;
    diffHunk: string | null;
    createdAt: string | null;
    authorAssociation: string | null;
    author: GqlActor | null;
    pullRequestReview: { databaseId: number | null } | null;
    replyTo: { databaseId: number | null } | null;
    reactionGroups: GqlReactionGroup[] | null;
}
interface GqlThread {
    id: string;
    isResolved: boolean;
    comments: Connection<GqlThreadComment>;
}
interface GqlIssueComment {
    databaseId: number | null;
    body: string | null;
    createdAt: string | null;
    authorAssociation: string | null;
    author: GqlActor | null;
    reactionGroups: GqlReactionGroup[] | null;
}
interface GqlCommitNode {
    commit: {
        oid: string;
        committedDate: string | null;
        authoredDate: string | null;
    };
}
interface GqlLinkedIssue {
    number: number;
    labels: Connection<{ name: string }>;
}
interface GqlPullRequest {
    number: number;
    title: string;
    url: string;
    state: string;
    createdAt: string | null;
    mergedAt: string | null;
    additions: number;
    deletions: number;
    isDraft: boolean;
    author: GqlActor | null;
    labels: Connection<{ name: string }>;
    reviews: Connection<GqlReview>;
    comments: Connection<GqlIssueComment>;
    commits: Connection<GqlCommitNode>;
    reviewThreads: Connection<GqlThread>;
    closingIssuesReferences: Connection<GqlLinkedIssue>;
}
interface GqlResponse {
    data?: {
        rateLimit?: { cost: number };
        repository?: { pullRequest?: GqlPullRequest | null };
    };
    errors?: { message: string }[];
}

/** Diagnostics returned alongside the assembled record for the Phase 2 gates. */
export interface GraphqlDiagnostics {
    /** Number of `gh api graphql` calls issued (1 + pagination follow-ups). */
    queryCount: number;
    /** Sum of `rateLimit.cost` across those calls. */
    totalCost: number;
    /** Field-level GraphQL error messages accepted alongside a present PR. */
    partialErrors: string[];
}

// GraphQL reactionGroup content enum → REST summary key.
const REACTION_KEY: Record<string, string> = {
    THUMBS_UP: "+1",
    THUMBS_DOWN: "-1",
    LAUGH: "laugh",
    HOORAY: "hooray",
    CONFUSED: "confused",
    HEART: "heart",
    ROCKET: "rocket",
    EYES: "eyes",
};

/** Convert GraphQL reactionGroups into the REST summary object mapReactions expects. */
function reactionGroupsToSummary(
    groups: GqlReactionGroup[] | null,
): Record<string, number> {
    const summary: Record<string, number> = {};
    for (const g of groups ?? []) {
        const key = REACTION_KEY[g.content];
        if (key && g.users.totalCount > 0) summary[key] = g.users.totalCount;
    }
    return summary;
}

/** GraphQL Actor → the REST `{ login, type }` user shape. */
function actorToRawUser(a: GqlActor | null): RawUser | null {
    if (!a) return null;
    return { login: a.login, type: a.__typename === "Bot" ? "Bot" : "User" };
}

/** GraphQL PR.state (OPEN|CLOSED|MERGED) → REST state ("open"|"closed"). */
function stateToRest(state: string): string {
    return state === "OPEN" ? "open" : "closed";
}

// ---------------------------------------------------------------------------
// Query text. Focused pagination queries carry an `# op:<name>` marker so a
// fixture/mock can route without a GraphQL parser; the real gh API ignores it.
// ---------------------------------------------------------------------------

const BASE_QUERY = `# op:base
query($owner:String!,$name:String!,$num:Int!){
  rateLimit{cost}
  repository(owner:$owner,name:$name){
    pullRequest(number:$num){
      number title url state createdAt mergedAt additions deletions isDraft
      author{login __typename}
      labels(first:100){nodes{name} pageInfo{hasNextPage endCursor}}
      reviews(first:100){nodes{databaseId state body submittedAt authorAssociation author{login __typename}} pageInfo{hasNextPage endCursor}}
      comments(first:100){nodes{databaseId body createdAt authorAssociation author{login __typename} reactionGroups{content users{totalCount}}} pageInfo{hasNextPage endCursor}}
      commits(first:100){nodes{commit{oid committedDate authoredDate}} pageInfo{hasNextPage endCursor}}
      reviewThreads(first:100){nodes{id isResolved comments(first:100){nodes{databaseId path line startLine originalLine body diffHunk createdAt authorAssociation author{login __typename} pullRequestReview{databaseId} replyTo{databaseId} reactionGroups{content users{totalCount}}} pageInfo{hasNextPage endCursor}}} pageInfo{hasNextPage endCursor}}
      closingIssuesReferences(first:100){nodes{number labels(first:100){nodes{name} pageInfo{hasNextPage endCursor}}} pageInfo{hasNextPage endCursor}}
    }
  }
}`;

/** A focused query paginating a single top-level PR connection. */
function connectionQuery(op: string, field: string, selection: string): string {
    return `# op:${op}
query($owner:String!,$name:String!,$num:Int!,$after:String!){
  rateLimit{cost}
  repository(owner:$owner,name:$name){
    pullRequest(number:$num){
      ${field}(first:100,after:$after){nodes{${selection}} pageInfo{hasNextPage endCursor}}
    }
  }
}`;
}

/** A focused query paginating one review thread's comments by the thread node id. */
const THREAD_COMMENTS_QUERY = `# op:threadComments
query($id:ID!,$after:String!){
  rateLimit{cost}
  node(id:$id){
    ... on PullRequestReviewThread{
      comments(first:100,after:$after){nodes{databaseId path line startLine originalLine body diffHunk createdAt authorAssociation author{login __typename} pullRequestReview{databaseId} replyTo{databaseId} reactionGroups{content users{totalCount}}} pageInfo{hasNextPage endCursor}}
    }
  }
}`;

const REVIEW_SEL =
    "databaseId state body submittedAt authorAssociation author{login __typename}";
const ISSUE_SEL =
    "databaseId body createdAt authorAssociation author{login __typename} reactionGroups{content users{totalCount}}";
const COMMIT_SEL = "commit{oid committedDate authoredDate}";
const LABEL_SEL = "name";

// ---------------------------------------------------------------------------
// Collection.
// ---------------------------------------------------------------------------

interface Collector {
    queryCount: number;
    totalCost: number;
    partialErrors: string[];
}

function accrue(c: Collector, res: GqlResponse): void {
    c.queryCount++;
    c.totalCost += res.data?.rateLimit?.cost ?? 0;
    for (const e of res.errors ?? []) c.partialErrors.push(e.message);
}

/** Fully paginate one top-level PR connection, returning all nodes in order. */
async function paginateConnection<T>(
    client: GhClient,
    c: Collector,
    vars: { owner: string; name: string; num: number },
    op: string,
    field: string,
    selection: string,
    first: Connection<T>,
): Promise<T[]> {
    const nodes = [...first.nodes];
    let page = first.pageInfo;
    while (page.hasNextPage && page.endCursor) {
        const res = await client.graphql<GqlResponse>([
            "-f",
            `query=${connectionQuery(op, field, selection)}`,
            "-F",
            `owner=${vars.owner}`,
            "-F",
            `name=${vars.name}`,
            "-F",
            `num=${vars.num}`,
            "-f",
            `after=${page.endCursor}`,
        ]);
        accrue(c, res);
        const conn = (
            res.data?.repository?.pullRequest as
                Record<string, Connection<T>> | undefined
        )?.[field];
        if (!conn) break;
        nodes.push(...conn.nodes);
        page = conn.pageInfo;
    }
    return nodes;
}

/** Fully paginate a single thread's comments by node id. */
async function paginateThreadComments(
    client: GhClient,
    c: Collector,
    threadId: string,
    first: Connection<GqlThreadComment>,
): Promise<GqlThreadComment[]> {
    const nodes = [...first.nodes];
    let page = first.pageInfo;
    while (page.hasNextPage && page.endCursor) {
        const res = await client.graphql<
            GqlResponse & {
                data?: {
                    node?: { comments?: Connection<GqlThreadComment> };
                };
            }
        >([
            "-f",
            `query=${THREAD_COMMENTS_QUERY}`,
            "-F",
            `id=${threadId}`,
            "-f",
            `after=${page.endCursor}`,
        ]);
        accrue(c, res);
        const conn = res.data?.node?.comments;
        if (!conn) break;
        nodes.push(...conn.nodes);
        page = conn.pageInfo;
    }
    return nodes;
}

/**
 * Assemble one PR's normalized {@link PullRequestData} via a single per-PR
 * GraphQL query (plus per-connection pagination follow-ups), reusing the REST
 * normalizer. Commit patch detail is still fetched on REST. Returns the record
 * and GraphQL diagnostics (query count, summed cost, partial errors).
 */
export async function collectGraphqlPr(
    client: GhClient,
    repo: string,
    number: number,
): Promise<{ data: PullRequestData; diagnostics: GraphqlDiagnostics }> {
    const [owner, name] = repo.split("/");
    if (!owner || !name) throw new Error(`repo must be owner/repo: ${repo}`);
    const vars = { owner, name, num: number };
    const c: Collector = { queryCount: 0, totalCost: 0, partialErrors: [] };

    const base = await client.graphql<GqlResponse>([
        "-f",
        `query=${BASE_QUERY}`,
        "-F",
        `owner=${owner}`,
        "-F",
        `name=${name}`,
        "-F",
        `num=${number}`,
    ]);
    accrue(c, base);

    const pr = base.data?.repository?.pullRequest;
    // Partial-response policy: a null pullRequest is unrecoverable → reject.
    if (!pr) {
        const detail = c.partialErrors.length
            ? `: ${c.partialErrors.join("; ")}`
            : "";
        throw new Error(
            `GraphQL returned no pullRequest for #${number}${detail}`,
        );
    }

    // Paginate every top-level connection to exhaustion.
    const [labels, reviews, issueComments, commitNodes, linkedIssues] =
        await Promise.all([
            paginateConnection<{ name: string }>(
                client,
                c,
                vars,
                "labels",
                "labels",
                LABEL_SEL,
                pr.labels,
            ),
            paginateConnection<GqlReview>(
                client,
                c,
                vars,
                "reviews",
                "reviews",
                REVIEW_SEL,
                pr.reviews,
            ),
            paginateConnection<GqlIssueComment>(
                client,
                c,
                vars,
                "issueComments",
                "comments",
                ISSUE_SEL,
                pr.comments,
            ),
            paginateConnection<GqlCommitNode>(
                client,
                c,
                vars,
                "commits",
                "commits",
                COMMIT_SEL,
                pr.commits,
            ),
            paginateConnection<GqlLinkedIssue>(
                client,
                c,
                vars,
                "closingIssues",
                "closingIssuesReferences",
                `number labels(first:100){nodes{name} pageInfo{hasNextPage endCursor}}`,
                pr.closingIssuesReferences,
            ),
        ]);

    const threads = await paginateConnection<GqlThread>(
        client,
        c,
        vars,
        "reviewThreads",
        "reviewThreads",
        `id isResolved comments(first:100){nodes{databaseId path line startLine originalLine body diffHunk createdAt authorAssociation author{login __typename} pullRequestReview{databaseId} replyTo{databaseId} reactionGroups{content users{totalCount}}} pageInfo{hasNextPage endCursor}}`,
        pr.reviewThreads,
    );

    // Flatten review threads into the REST-equivalent inline list. threadResolved
    // mirrors the REST hybrid: only a thread's FIRST comment carries the resolved
    // flag (the REST path joins isResolved by the first comment's databaseId).
    const inlineRaw: RawInline[] = [];
    const resolvedByCommentId = new Map<number, boolean>();
    for (const t of threads) {
        const comments =
            t.comments.pageInfo.hasNextPage && t.id
                ? await paginateThreadComments(client, c, t.id, t.comments)
                : t.comments.nodes;
        comments.forEach((cm, idx) => {
            const id = cm.databaseId ?? 0;
            if (idx === 0) resolvedByCommentId.set(id, t.isResolved);
            inlineRaw.push({
                id,
                path: cm.path ?? undefined,
                line: cm.line ?? undefined,
                start_line: cm.startLine,
                original_line: cm.originalLine,
                body: cm.body ?? "",
                diff_hunk: cm.diffHunk ?? undefined,
                in_reply_to_id: cm.replyTo?.databaseId ?? undefined,
                pull_request_review_id:
                    cm.pullRequestReview?.databaseId ?? undefined,
                created_at: cm.createdAt ?? undefined,
                user: actorToRawUser(cm.author),
                author_association: cm.authorAssociation ?? undefined,
                reactions: reactionGroupsToSummary(cm.reactionGroups),
            });
        });
    }

    const prRaw: RawPrFull = {
        number: pr.number,
        title: pr.title,
        user: actorToRawUser(pr.author),
        html_url: pr.url,
        state: stateToRest(pr.state),
        draft: pr.isDraft,
        additions: pr.additions,
        deletions: pr.deletions,
        created_at: pr.createdAt,
        merged_at: pr.mergedAt,
        labels: labels.map((l) => ({ name: l.name })),
    };

    const reviewsRaw: RawReview[] = reviews.map((r) => ({
        id: r.databaseId ?? 0,
        state: r.state,
        body: r.body ?? "",
        submitted_at: r.submittedAt,
        user: actorToRawUser(r.author),
        author_association: r.authorAssociation ?? undefined,
    }));

    const issueRaw: RawIssue[] = issueComments.map((c2) => ({
        id: c2.databaseId ?? 0,
        body: c2.body ?? "",
        created_at: c2.createdAt ?? "",
        user: actorToRawUser(c2.author),
        author_association: c2.authorAssociation ?? undefined,
        reactions: reactionGroupsToSummary(c2.reactionGroups),
    }));

    const linked = linkedIssues.map((i) => ({
        number: i.number,
        labels: i.labels.nodes.map((l) => l.name),
    }));

    const commits = await fetchCommitDetail(
        client,
        repo,
        commitNodes.map((cm) => ({
            sha: cm.commit.oid,
            committedAt: cm.commit.committedDate ?? cm.commit.authoredDate,
        })),
    );

    const data = buildPrPayload({
        prRaw,
        reviewsRaw,
        inlineRaw,
        issueRaw,
        commits,
        resolvedByCommentId,
        linkedIssues: linked,
    });

    return { data, diagnostics: { ...c } };
}

/** Thin wrapper returning just the normalized record (production entry point). */
export async function assembleViaGraphql(
    client: GhClient,
    repo: string,
    number: number,
): Promise<PullRequestData> {
    const { data } = await collectGraphqlPr(client, repo, number);
    return data;
}
