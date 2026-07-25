// types.ts — Shared shapes used by the scripts.
//
// This file has NO runtime exports — only types and interfaces. Other scripts
// import from it via `import type { ... } from './types.ts'` so Node's type
// stripping erases the import entirely. Field-naming convention: camelCase
// everywhere (TS types, fixtures, cache files, persisted run JSON).

export type UserType = "User" | "Bot";

export interface User {
    login: string;
    type: UserType;
}

/**
 * GitHub's CommentAuthorAssociation — the comment author's relationship to the
 * repository. Filtering keeps OWNER/MEMBER/COLLABORATOR and drops the rest.
 */
export type AuthorAssociation =
    "OWNER" | "MEMBER" | "COLLABORATOR" | (string & {});

/**
 * Source for comments fetched from GitHub:
 * - "review": PR review-summary bodies from /pulls/{n}/reviews
 * - "inline": line-level review comments from /pulls/{n}/comments
 * - "issue": PR conversation comments from /issues/{n}/comments
 */
export type CommentSource = "review" | "inline" | "issue";

/** Reason a comment was dropped during filtering (kept = retained). */
export type FilterDropReason =
    "keep" | "bot" | "automation" | "short" | "quoted" | "self" | "association";

/**
 * Coarse intent classification for kept comments:
 * - "ask": a reviewer requesting a change — the substantive, actionable signal.
 * - "summary": the top-level review body (approve/request-changes overview).
 * - "reply": acknowledgement / closure ("Fixed", "Addressed").
 * Only "ask" comments count toward "humans asked for X".
 */
export type CommentKind = "ask" | "reply" | "summary";

/** Who authored a comment, after CCR/automation identity resolution. */
export type AuthorKind = "human" | "ccr" | "bot";

/**
 * PR type for metric normalization. Nullable: classification *state* is carried
 * separately in {@link PrClassification.classificationStatus}. `agent` is a
 * source, never a value of PrType.
 */
export type PrType =
    "bug-fix" | "feature" | "refactor" | "docs" | "test" | "chore";

export type PrTypeSource = "label" | "title" | "issue" | "agent" | "unknown";

export type ClassificationStatus = "complete" | "needs-agent" | "failed";

/**
 * Comment severity from the judge. Nullable: `null` means not-yet-judged or
 * judge-failed and is excluded from severity slices. `judgeStatus` carries the
 * detail — there is no `unjudged` sentinel value.
 */
export type Severity = "critical" | "substantive" | "nit";

export type JudgeStatus = "ok" | "failed" | "lowConfidence";

/** Judge verdict on whether the author acted on a CCR comment. */
export type CcrOutcome = "addressed" | "rejected" | "ignored" | "unclear";

export type Category =
    | "error-handling"
    | "concurrency"
    | "input-validation"
    | "security"
    | "resource-management"
    | "api-design"
    | "backward-compatibility"
    | "type-safety"
    | "performance"
    | "testing"
    | "logging-observability"
    | "documentation"
    | "style-naming"
    | "configuration"
    | "other";

// ---------------------------------------------------------------------------
// Raw GitHub cache shapes (written by fetch-prs.ts, rawSchemaVersion-tagged).
// ---------------------------------------------------------------------------

export interface ReviewSummary {
    id: number;
    state: string;
    body: string;
    submittedAt: string | null;
    user: User | null;
    authorAssociation?: AuthorAssociation;
}

export interface Reaction {
    content: string;
    user: User | null;
}

export interface InlineComment {
    id: number;
    path?: string;
    line?: number;
    startLine?: number | null;
    originalLine?: number | null;
    body: string;
    diffHunk?: string;
    inReplyToId?: number;
    pullRequestReviewId?: number;
    createdAt?: string;
    user: User | null;
    authorAssociation?: AuthorAssociation;
    /** Whether the anchored line has drifted from the current diff. */
    lineStale?: boolean;
    /** Whether the inline thread is resolved (GraphQL). */
    threadResolved?: boolean;
    reactions?: Reaction[];
}

export interface IssueComment {
    id: number;
    body: string;
    createdAt: string;
    user: User | null;
    authorAssociation?: AuthorAssociation;
    reactions?: Reaction[];
}

/** One commit in the PR timeline (used for ccrSawCode + iterations). */
export interface TimelineCommit {
    sha: string;
    committedAt: string | null;
    files: string[];
    /** Optional filename -> unified diff patch for changed files. */
    patches?: Record<string, string>;
}

/** Top-level PR metadata + the normalization/classification fields. */
export interface PullRequestMetadata {
    number: number;
    title: string;
    author: User | null;
    url: string;
    state: string;
    isDraft?: boolean;
    additions?: number;
    deletions?: number;
    createdAt: string | null;
    mergedAt: string | null;
    labels?: string[];
    /** Issues this PR closes, with their labels (for classification). */
    linkedIssues?: { number: number; labels: string[] }[];
    // Derived (classify-pr.ts / attribute-comments.ts):
    prType?: PrType | null;
    prTypeSource?: PrTypeSource;
    classificationStatus?: ClassificationStatus;
    ccrReviewed?: boolean;
    cycleTimeHours?: number | null;
    iterations?: number;
}

/** Cache bundle written by fetch-prs.ts. */
export interface PullRequestData {
    rawSchemaVersion: string;
    pr: PullRequestMetadata;
    reviews: ReviewSummary[];
    inline: InlineComment[];
    issue: IssueComment[];
    commits: TimelineCommit[];
}

// ---------------------------------------------------------------------------
// Filtering shapes.
// ---------------------------------------------------------------------------

export interface FilterOpts {
    includeSelf: boolean;
    minLength: number;
    since?: string;
    until?: string;
    sources?: Set<CommentSource>;
    kinds?: Set<CommentKind>;
    maxBodyLength?: number;
    defaultBots?: boolean;
    /** Logins treated as CCR (from config.json). */
    ccrLogins?: Set<string>;
    /** Logins treated as automation (from config.json). */
    automationLogins?: Set<string>;
}

/** Normalized comment record emitted by filtering. */
export interface KeptComment {
    pr: number | undefined;
    url: string | undefined;
    id: number | undefined;
    commentUrl: string | undefined;
    source: CommentSource;
    kind: CommentKind;
    user: string | undefined;
    authorAssociation?: AuthorAssociation;
    path: string | undefined;
    line: number | undefined;
    startLine?: number | null;
    originalLine?: number | null;
    lineStale?: boolean;
    diffHunk: string | undefined;
    body: string;
    createdAt?: string;
    threadResolved?: boolean;
    reactions?: Reaction[];
}

export interface DroppedCounts {
    bot: number;
    automation: number;
    short: number;
    quoted: number;
    self: number;
    association: number;
    kindFiltered: number;
    sourceFiltered: number;
}

export interface FilterResult {
    kept: KeptComment[];
    dropped: DroppedCounts;
    prSkipped?: boolean;
}

export interface ClassifiableComment {
    body?: string;
    user: User | null;
    authorAssociation?: AuthorAssociation;
}

// ---------------------------------------------------------------------------
// Attribution shapes (run-JSON comments[] row).
// ---------------------------------------------------------------------------

/**
 * A fully-typed, attributed comment row. Judge fields default to null until
 * Step 3 fills them. `ccrSawCode` is a deterministic eligibility gate; the
 * CCR-outcome and same-concern verdicts are filled by the agent judge
 * (references/judge.prompt.md).
 */
export interface AttributedComment {
    pr: number;
    /** GitHub's source id for the review/inline/issue comment. */
    externalId: number;
    /** Deep link to the source comment when GitHub exposes one. */
    url: string | undefined;
    /**
     * Unique per-row identity key: `${pr}:${source}:${externalId}`. Never
     * collides — even for repeated comments on the same line or pathless review
     * summaries. Use this to key judge input and any per-row join.
     */
    rowId: string;
    /**
     * Canonical de-dup/grouping key: `${pr}:${authorLogin}:${path}:${lineStart}-${lineEnd}`.
     * Intentionally collides for repeated findings on the same anchor so metrics
     * can dedupe; it is NOT a unique row key — use {@link rowId} for that.
     */
    findingId: string;
    authorKind: AuthorKind;
    authorLogin: string | undefined;
    kind: CommentKind;
    source: CommentSource;
    /** Null for review summaries or PR conversation comments with no file anchor. */
    path: string | null;
    /** Inclusive anchor range; null when the comment is not line-level. */
    lineStart: number | null;
    lineEnd: number | null;
    /** True when GitHub says the original diff anchor no longer maps cleanly. */
    lineStale: boolean;
    createdAt: string | null;

    /** Deterministic gate: CCR reviewed the code version this human ask anchors to. */
    ccrSawCode: boolean;
    /** Comment touches an excludedPaths file; excluded from usefulness/miss math. */
    pathExcluded: boolean;

    // Judge-filled (null until Step 3).
    /** Human asks only: whether the ask is a real actionable review concern. */
    isSubstantive: boolean | null;
    /** Human asks only: whether the concern is visible from the supplied diff hunk. */
    diffDetectable: boolean | null;
    /** Closed-vocabulary label assigned by the judge. */
    severity: Severity | null;
    category: Category | null;
    confidence: number | null;
    judgeStatus: JudgeStatus | null;
    judgeError: string | null;
    /** Judge (CCR comments): did the author act on it? null until judged. */
    ccrOutcome: CcrOutcome | null;
    /** Judge (human asks): did a CCR comment raise the same concern? null until judged. */
    ccrAddressedConcern: boolean | null;
    /** Derived for human asks: substantive && diffDetectable && ccrSawCode && !ccrAddressedConcern. */
    isGap: boolean | null;
    /** Usually the judged category for substantive rows; null for non-substantive rows. */
    theme: Category | null;

    /** Raw body, retained for judging + de-dup; trimmed to a budget on emit. */
    body: string;
}

// ---------------------------------------------------------------------------
// Theme / proposal shapes.
// ---------------------------------------------------------------------------

export type PromotedVia = "opinion" | "evidence";

export interface Theme {
    /** Controlled-vocabulary category label. */
    label: Category;
    /** Number of missed human asks in this theme. */
    gapCount: number;
    /** Total substantive asks in this theme, including non-gaps. */
    askCount: number;
    distinctReviewers: number;
    /** True when the theme clears the gate for a proposed instruction rule. */
    promoted: boolean;
    promotedVia: PromotedVia | null;
    priorityScore: number;
    /** PR numbers used as citations in the report. */
    sourcePrs: number[];
}

export type ProposedEditStatus = "proposed" | "applied" | "blocked" | "retired";

export interface ProposedEdit {
    /** Target instruction file for the proposed rule. */
    file: string;
    /** Optional applyTo glob if the rule belongs in a scoped instruction file. */
    applyTo: string | null;
    theme: Category;
    /** Generalized imperative rule for human approval. */
    rule: string;
    /** Existing rule this proposal duplicates, if any. */
    redundantWith: string | null;
    sourcePrs: number[];
    /** Short explanation tying the proposal back to cited evidence. */
    provenance: string;
    status: ProposedEditStatus;
}

// ---------------------------------------------------------------------------
// Judge IO shapes (Step 3).
// ---------------------------------------------------------------------------

export interface JudgeInputItem {
    /**
     * Stable, unique per-item id within a batch — the comment's {@link
     * AttributedComment.rowId} (`${pr}:${source}:${externalId}`), NOT its
     * findingId. Using rowId guarantees ids are unique even when two comments
     * share an anchor (same findingId), so the judge can never conflate two
     * distinct rows.
     */
    id: string;
    body: string;
    diffHunk: string;
    path: string | null;
    lineStart: number | null;
    lineEnd: number | null;
    lineStale: boolean;
    /** "ask" gap-candidate vs. a CCR comment needing severity + outcome. */
    purpose: "gap-candidate" | "ccr-comment";
    /** gap-candidate only: CCR comments on the same PR, for same-concern judging. */
    ccrComments?: {
        path: string | null;
        lineStart: number | null;
        lineEnd: number | null;
        body: string;
    }[];
    /** ccr-comment only: line-level diff of what changed at these lines after the comment. */
    postCommentDiff?: string;
    /** ccr-comment only: author reply bodies. */
    authorReplies?: string[];
}

export interface JudgeResultItem {
    id: string;
    category: Category;
    confidence: number;
    // gap-candidate outputs
    isSubstantive?: boolean;
    diffDetectable?: boolean;
    ccrAddressedConcern?: boolean;
    // ccr-comment outputs
    severity?: Severity;
    outcome?: CcrOutcome;
}
