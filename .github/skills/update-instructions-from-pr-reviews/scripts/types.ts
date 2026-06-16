// types.ts — Shared shapes used by the scripts.
//
// This file has NO runtime exports — only types and interfaces. Other scripts
// should import from it via `import type { ... } from './types.ts'` so Node's
// type stripping erases the import entirely.

export type UserType = "User" | "Bot";

export interface User {
  login: string;
  type: UserType;
}

/**
 * Source for comments fetched from GitHub:
 * - "review": PR review-summary bodies from /pulls/{n}/reviews
 * - "inline": line-level review comments from /pulls/{n}/comments
 * - "issue": PR conversation comments from /issues/{n}/comments
 */
export type CommentSource = "review" | "inline" | "issue";

/**
 * Classification outcome for a comment during filtering:
 * - "keep": comment is retained for downstream processing
 * - "bot": authored by automation, known bot identity, or automation marker
 * - "short": too short or matches low-signal shorthand (for example, LGTM/+1)
 * - "quoted": contains only quoted content and no new feedback
 * - "self": authored by the PR author when self-comments are excluded
 */
export type Verdict = "keep" | "bot" | "short" | "quoted" | "self";

/**
 * Coarse intent classification for kept comments:
 * - "summary": top-level review summary body (source === "review")
 * - "reply": acknowledgement or closure phrasing (for example, "Fixed", "Addressed")
 * - "ask": substantive reviewer feedback request (everything else)
 *
 * Useful for clustering: when mining "what do reviewers ask for", you usually
 * keep "ask" and treat "summary" + "reply" as conversational noise.
 */
export type Kind = "ask" | "reply" | "summary";

/** Top-level pull request metadata shared across all fetched comment sources. */
export interface PullRequestMetadata {
  number: number;
  title: string;
  author: User | null;
  url: string;
  state: string;
  mergedAt: string | null;
}

/** Review-summary body submitted via the PR review flow (approve/comment/request changes). */
export interface ReviewSummary {
  id: number;
  state: string;
  body: string;
  submitted_at: string;
  user: User | null;
  /** Internal origin tag added after fetch so combined comment sources stay identifiable. */
  _source?: CommentSource;
}

/** Line-level review comment anchored to a file/diff hunk in the pull request. */
export interface InlineComment {
  id: number;
  path?: string;
  line?: number;
  original_line?: number;
  body: string;
  diff_hunk?: string;
  in_reply_to_id?: number;
  pull_request_review_id?: number;
  created_at?: string;
  user: User | null;
  /** Internal origin tag added after fetch so combined comment sources stay identifiable. */
  _source?: CommentSource;
}

/** Issue-style conversation comment from the main PR discussion timeline. */
export interface IssueComment {
  id: number;
  body: string;
  created_at: string;
  user: User | null;
  /** Internal origin tag added after fetch so combined comment sources stay identifiable. */
  _source?: CommentSource;
}

/** Cache written by fetch-prs.ts: a bundle of GitHub API payloads, narrowed to the fields we keep. */
export interface PullRequestData {
  pr: PullRequestMetadata;
  reviews: ReviewSummary[];
  inline: InlineComment[];
  issue: IssueComment[];
}

/** Runtime options controlling which comments are kept and how bodies are shaped. */
export interface FilterOpts {
  includeSelf: boolean;
  minLength: number;
  /** ISO date (YYYY-MM-DD or full). Skips PRs merged before this. */
  since?: string;
  /** ISO date (YYYY-MM-DD or full). Skips PRs merged on/after this. */
  until?: string;
  /** Sources to keep (default: all three). */
  sources?: Set<CommentSource>;
  /** Coarse-intent kinds to keep (default: all). */
  kinds?: Set<Kind>;
  /** Truncate body text in kept comments to at most N chars. */
  maxBodyLength?: number;
  /**
   * If true (default), known release-automation / bot accounts (see
   * DEFAULT_BOT_LOGINS) and known automation boilerplate markers
   * (e.g., the Copilot CLI PR marker) are dropped.
   */
  defaultBots?: boolean;
}

/** Normalized comment record emitted by filtering for downstream clustering/rendering. */
export interface KeptComment {
  pr: number | undefined;
  /** PR-level URL (e.g., https://github.com/owner/repo/pull/N). */
  url: string | undefined;
  /** Source comment id. Stable across renders; useful for synthesizing deep links. */
  id: number | undefined;
  /**
   * Deep-link URL to the specific comment (e.g.,
   * https://github.com/owner/repo/pull/N#discussion_r<id> for inline,
   * #pullrequestreview-<id> for review, #issuecomment-<id> for issue).
   * Synthesized from the PR URL and the comment id + source.
   */
  comment_url: string | undefined;
  source: CommentSource;
  /** Coarse intent — see Kind. Useful for clustering. */
  kind: Kind;
  user: string | undefined;
  path: string | undefined;
  line: number | undefined;
  diff_hunk: string | undefined;
  body: string;
}

/** Counters for comments dropped during filtering, keyed by drop reason. */
export interface DroppedCounts {
  bot: number;
  short: number;
  quoted: number;
  self: number;
  /** Dropped by post-classification kind filter (e.g. --kind ask). */
  kindFiltered: number;
  /** Dropped by source filter (e.g. --source inline,issue). */
  sourceFiltered: number;
}

/** Counts for whole-PR skips before per-comment filtering begins. */
export interface SkippedCounts {
  /** PRs skipped wholesale by --since / --until. */
  prFiltered: number;
}

/** Output payload for a single PR after filtering is applied. */
export interface FilterResult {
  kept: KeptComment[];
  dropped: DroppedCounts;
  /** If non-zero, the PR was skipped wholesale by --since/--until. */
  prSkipped?: boolean;
}

/** Anything classify() needs from a comment. */
export interface Classifiable {
  body?: string;
  user: User | null;
}
