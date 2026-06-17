#!/usr/bin/env node
/**
 * filter-comments.ts — Drop low-signal noise from one or more cached PR JSON
 * files (the output of fetch-prs.ts --fetch) and emit the comments worth clustering.
 */

import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import type {
  ClassifiableComment,
  FilterOpts,
  FilterResult,
  CommentKind,
  KeptComment,
  PullRequestData,
  CommentSource,
  User,
  CommentType,
} from "./types.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("filter-comments");

function usage(): string {
  return [
    "Usage:",
    "  node scripts/filter-comments.ts <pr-file.json> [more.json ...] [options]",
    '  node scripts/filter-comments.ts --glob "pr-cache/<owner>-<repo>/pr-*.json" [options]',
    "",
    "Comment filters:",
    "  --no-default-bots        Don't auto-deny known automation accounts",
    "  --min-length <N>         Minimum body length to keep (default: 20)",
    "  --max-body-length <N>    Truncate kept comment bodies to N chars",
    "",
    "PR-level filters:",
    "  --since <ISO date>       Skip PRs merged before this date (YYYY-MM-DD)",
    "  --until <ISO date>       Skip PRs merged on/after this date",
    "",
    "Source / kind filters:",
    "  --source <list>          Comma-separated: inline,review,issue (default: all)",
    "  --kind <list>            Comma-separated: ask,reply,summary (default: all)",
    "  -h, --help              Show this help",
  ].join("\n");
}

export function parseArgs(argv: string[]): CliOptions {
  const parsed = nodeParseArgs({
    args: argv,
    options: {
      "no-default-bots": { type: "boolean", default: false },
      "min-length": { type: "string", default: "20" },
      "max-body-length": { type: "string" },
      since: { type: "string" },
      until: { type: "string" },
      source: { type: "string" },
      kind: { type: "string" },
      help: { type: "boolean", short: "h", default: false },
      glob: { type: "string" },
    },
    allowPositionals: true,
    strict: true,
  });

  if (parsed.values.help) {
    process.stdout.write(`${usage()}\n`);
    process.exit(0);
  }

  const out: CliOptions = {
    files: [...parsed.positionals],
    includeSelf: true,
    minLength: Number.parseInt(parsed.values["min-length"], 10),
    glob: parsed.values.glob,
    defaultBots: !parsed.values["no-default-bots"],
    since: parsed.values.since,
    until: parsed.values.until,
  };

  if (!Number.isFinite(out.minLength) || out.minLength < 1) {
    throw new Error(`invalid --min-length "${parsed.values["min-length"]}"`);
  }

  const maxBodyRaw = parsed.values["max-body-length"];
  if (maxBodyRaw != null) {
    const n = Number.parseInt(maxBodyRaw, 10);
    if (!Number.isFinite(n) || n < 1) {
      throw new Error(`invalid --max-body-length "${maxBodyRaw}"`);
    }
    out.maxBodyLength = n;
  }

  for (const dateFlag of ["since", "until"] as const) {
    const raw = parsed.values[dateFlag];
    if (raw != null && Number.isNaN(Date.parse(raw))) {
      throw new Error(`invalid --${dateFlag} "${raw}" (expected ISO date)`);
    }
  }

  const sourceRaw = parsed.values.source;
  if (sourceRaw != null) {
    const set = parseSourceList(sourceRaw);
    out.sources = set;
  }

  const kindRaw = parsed.values.kind;
  if (kindRaw != null) {
    const set = parseKindList(kindRaw);
    out.kinds = set;
  }

  if (out.glob) {
    const matches = fs.globSync(out.glob, { withFileTypes: false });
    out.files.push(...matches);
  }
  if (out.files.length === 0) {
    throw new Error("no input files (pass one or more paths, or --glob)");
  }
  return out;
}

interface CliOptions extends FilterOpts {
  files: string[];
  glob?: string;
}

interface FullStats {
  kept: number;
  dropped: {
    bot: number;
    short: number;
    quoted: number;
    self: number;
    association: number;
    kindFiltered: number;
    sourceFiltered: number;
    total: number;
  };
  prSkipped: number;
}

function parseSourceList(raw: string): Set<CommentSource> {
  const valid: CommentSource[] = ["inline", "review", "issue"];
  const set = new Set<CommentSource>();
  for (const tok of raw.split(",").map((s) => s.trim()).filter(Boolean)) {
    if (!valid.includes(tok as CommentSource)) {
      throw new Error(`invalid --source "${tok}"; expected ${valid.join("|")}`);
    }
    set.add(tok as CommentSource);
  }
  if (set.size === 0) throw new Error(`--source requires at least one value`);
  return set;
}

function parseKindList(raw: string): Set<CommentKind> {
  const valid: CommentKind[] = ["ask", "reply", "summary"];
  const set = new Set<CommentKind>();
  for (const tok of raw.split(",").map((s) => s.trim()).filter(Boolean)) {
    if (!valid.includes(tok as CommentKind)) {
      throw new Error(`invalid --kind "${tok}"; expected ${valid.join("|")}`);
    }
    set.add(tok as CommentKind);
  }
  if (set.size === 0) throw new Error(`--kind requires at least one value`);
  return set;
}

/**
 * Author associations we keep: people formally joined to the repository.
 * GitHub reports each comment's relationship via CommentAuthorAssociation; we
 * retain only OWNER/MEMBER/COLLABORATOR and drop external drive-by feedback
 * (CONTRIBUTOR / NONE / FIRST_TIME_CONTRIBUTOR / ...) before clustering.
 *
 * This is intentionally not configurable: the skill mines a repo's own review
 * conventions, so only people that we trust (ie, don't leave malicious comments, etc..)
 * should be included.
 */
export const ALLOWED_ASSOCIATIONS: ReadonlySet<string> = new Set([
  "OWNER",
  "MEMBER",
  "COLLABORATOR",

  // NOTE: I've explicitly excluded 'contributor' from here because _anyone_ can become a 
  // contributor by getting a PR merged, and that still seems too broad to me.
]);

// Low-signal patterns. Match the *entire* trimmed body so longer comments that
// merely *start* with a polite acknowledgement ("Thanks for the patch! Now
// could you also...") still survive. Order doesn't matter — these are
// alternatives, not a priority list.
export const LOW_SIGNAL: RegExp[] = [
  /^lgtm[!.\s]*$/i,
  /^\+1[!.\s]*$/,
  /^thanks?[!.\s]*$/i,
  /^ty[!.\s]*$/i,
  /^done[!.\s]*$/i,
  /^fixed[!.\s]*$/i,
  /^ack(nowledged)?[!.\s]*$/i,
  /^👍|^👎|^🎉|^❤️|^🚀$/u,
];

/**
 * Known release-automation / app accounts that aren't tagged as `type: "Bot"`
 * by the GitHub API and don't carry the `[bot]` suffix, but produce noise
 * that has no business in a clustering pass.
 *
 * Add to this list cautiously — it applies to every repo.
 */
export const DEFAULT_BOT_LOGINS: ReadonlySet<string> = new Set([
  // Microsoft release automation that posts install-instruction blocks.
  "azure-sdk",
  "azure-sdk-write",
  // GitHub App identities for the Copilot pipeline (sometimes surfaced
  // without the [bot] suffix or with an `app/` prefix).
  "copilot-swe-agent",
  "copilot-pull-request-reviewer",
  "app/copilot-swe-agent",
  "app/copilot-pull-request-reviewer",
]);

/**
 * Markers embedded by automation in comment bodies. If any of these appear,
 * the body is treated as boilerplate and dropped regardless of author.
 */
export const AUTOMATION_MARKERS: RegExp[] = [
  // Copilot CLI posts a hidden HTML comment to identify its own messages.
  /<!--\s*#comment-cli-pr\s*-->/i,
  // GitHub Actions install-instruction summary header.
  /<!--\s*install-instructions\s*-->/i,
];

export function isAutomationBoilerplate(body: string): boolean {
  return AUTOMATION_MARKERS.some((re) => re.test(body));
}

/**
 * Author-reply patterns. A body that begins with one of these phrases is
 * almost always a reaction to feedback ("Fixed in <sha>", "Addressed, thanks")
 * rather than the reviewer-ask we want for clustering. Used by `inferKind`.
 */
export const AUTHOR_REPLY_PATTERNS: RegExp[] = [
  /^(fixed|done|addressed|resolved|removed|updated|refactored|reverted|accepted|agreed)\b/i,
  /^(intentional|not taken)\b/i,
  /^this (?:is|was) intentional\b/i,
  /^you'?re right\b/i,
  /^(yes|no),?\s+(added|removed|updated|fixed)\b/i,
  /^(good|nice|great)\s+catch\b/i,
  /^(thanks|thank you)[,!.\s]/i,
  /^(will|i'?ll|going to)\s+(fix|address|update|do)\b/i,
  /^sg[!.\s]/i, // "sg" / "sg!" — sounds-good shorthand
];

/**
 * Infer the coarse intent of a kept comment. See `Kind` for definitions.
 *
 * We classify purely from the comment text, never by comparing the comment's
 * author to the PR author. That deliberate choice matters in two cases:
 *
 *   1. Copilot-authored PRs. When the PR author is `Copilot` (the coding
 *      agent), it's the human driving the PR who writes the replies to
 *      reviewers. Matching author-to-PR-author would mislabel those replies
 *      as reviewer "asks" and pollute clustering; text-matching tags them
 *      "reply" correctly.
 *
 *   2. Reviewer-to-reviewer chatter. Agreement like "Good catch, let's
 *      remove it" from one reviewer endorsing another is conversational
 *      noise, not a reviewer ask. Text-matching tags it "reply" correctly.
 *
 * Validation (Azure/azure-dev cache, 199 PRs over 30 days): of 183 inline
 * comments tagged "reply", 178 (97%) came from the PR author and 5 are the
 * cases above. Exactly one substantive comment was misclassified ("Thanks
 * for the suggestion. This seems a bit overkill — will defer…") — an
 * acceptable trade.
 */
export function inferKind(source: CommentSource, body: string): CommentKind {
  if (source === "review") return "summary";
  const trimmed = body.trim();
  if (AUTHOR_REPLY_PATTERNS.some((re) => re.test(trimmed))) return "reply";
  return "ask";
}

export function isBot(user: User | null): boolean {
  if (!user) return false;
  if (user.type === "Bot") return true;
  return /\[bot\]$/i.test(user.login || "");
}

export function isQuotedOnly(body: string): boolean {
  const lines = body
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean);
  if (lines.length === 0) return true;
  return lines.every((l) => l.startsWith(">"));
}

export function classifyComment({
  comment,
  prAuthor,
  minLength,
  includeSelf,
  defaultBots,
}: {
  comment: ClassifiableComment;
  prAuthor: string | undefined;
  minLength: number;
  includeSelf: boolean;
  defaultBots: boolean;
}): CommentType {
  const body = (comment.body ?? "").trim();
  const login = comment.user?.login;
  if (
    isBot(comment.user) ||
    (defaultBots && !!login && DEFAULT_BOT_LOGINS.has(login))
  ) {
    return "bot";
  }

  if (defaultBots && isAutomationBoilerplate(body)) return "bot";
  
  // Author-association gate: keep only people formally joined to the repo
  // (OWNER/MEMBER/COLLABORATOR). Bots are already handled above; comments that
  // lack an association field (older caches) are not subject to this filter.
  if (
    comment.authorAssociation !== undefined &&
    !ALLOWED_ASSOCIATIONS.has(comment.authorAssociation.toUpperCase())
  ) {
    return "association";
  }
  
  if (
    !includeSelf &&
    prAuthor &&
    comment.user?.login === prAuthor
  ) {
    return "self";
  }
  
  if (body.length < minLength) return "short";
  if (LOW_SIGNAL.some((re) => re.test(body))) return "short";
  if (isQuotedOnly(body)) return "quoted";

  return "keep";
}

/**
 * True if the PR itself should be skipped wholesale based on PR-level
 * filters (since / until). Returns true => skip.
 */
export function shouldSkipPr(data: PullRequestData, opts: FilterOpts): boolean {
  // data.pr comes from external JSON; guard even though the type says non-null.
  // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON boundary
  const mergedAt = data.pr?.mergedAt;
  if (opts.since != null) {
    if (!mergedAt) return true;
    if (Date.parse(mergedAt) < Date.parse(opts.since)) return true;
  }
  if (opts.until != null) {
    if (!mergedAt) return true;
    if (Date.parse(mergedAt) >= Date.parse(opts.until)) return true;
  }
  return false;
}

function truncateBody(body: string, maxLen: number | undefined): string {
  if (maxLen == null || body.length <= maxLen) return body;
  return body.slice(0, maxLen).trimEnd() + "…";
}

export function filterPullRequestData(
  data: PullRequestData,
  opts: FilterOpts,
): FilterResult {
  if (shouldSkipPr(data, opts)) {
    return {
      kept: [],
      dropped: {
        bot: 0,
        short: 0,
        quoted: 0,
        self: 0,
        association: 0,
        kindFiltered: 0,
        sourceFiltered: 0,
      },
      prSkipped: true,
    };
  }

  /* eslint-disable @typescript-eslint/no-unnecessary-condition -- external JSON boundary: data.pr may be absent */
  const prAuthor = data.pr?.author?.login;
  const prNumber = data.pr?.number;
  const prUrl = data.pr?.url;
  /* eslint-enable @typescript-eslint/no-unnecessary-condition */

  type SourcedComment =
    | (PullRequestData["reviews"][number] & { _source: CommentSource })
    | (PullRequestData["inline"][number] & { _source: CommentSource })
    | (PullRequestData["issue"][number] & { _source: CommentSource });

  const sourceFilter = opts.sources;
  const dropped = {
    bot: 0,
    short: 0,
    quoted: 0,
    self: 0,
    association: 0,
    kindFiltered: 0,
    sourceFiltered: 0,
  };

  const allComments: SourcedComment[] = [
    ...data.reviews
      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- body may be absent in external JSON
      .filter((r) => r.body?.trim())
      .map((r): SourcedComment => ({ ...r, _source: "review" })),
    ...data.inline.map((c): SourcedComment => ({ ...c, _source: "inline" })),
    ...data.issue.map((c): SourcedComment => ({ ...c, _source: "issue" })),
  ];

  const comments: SourcedComment[] = [];
  for (const c of allComments) {
    if (sourceFilter && !sourceFilter.has(c._source)) {
      dropped.sourceFiltered++;
      continue;
    }
    comments.push(c);
  }

  const kept: KeptComment[] = [];
  for (const c of comments) {
    const verdict = classifyComment({
      comment: c,
      prAuthor,
      minLength: opts.minLength,
      includeSelf: opts.includeSelf,
      defaultBots: opts.defaultBots !== false,
    });
    if (verdict !== "keep") {
      dropped[verdict]++;
      continue;
    }

    const id = "id" in c ? c.id : undefined;
    const trimmed = c.body.trim();
    const kind = inferKind(c._source, trimmed);
    if (opts.kinds && !opts.kinds.has(kind)) {
      dropped.kindFiltered++;
      continue;
    }

    kept.push({
      pr: prNumber,
      url: prUrl,
      id,
      comment_url: synthesizeCommentUrl(prUrl, c._source, id, prNumber),
      source: c._source,
      kind,
      user: c.user?.login,
      path: "path" in c ? c.path : undefined,
      line:
        "line" in c
          ? (c.line ?? ("original_line" in c ? c.original_line : undefined))
          : undefined,
      diff_hunk: "diff_hunk" in c ? c.diff_hunk : undefined,
      body: truncateBody(trimmed, opts.maxBodyLength),
    });
  }
  return { kept, dropped };
}

/**
 * Synthesize a deep-link URL to a specific comment. GitHub's per-comment
 * fragments are:
 *   - inline review comment: <pr_url>#discussion_r<id>
 *   - review summary:        <pr_url>#pullrequestreview-<id>
 *   - issue comment:         <pr_url with /pull/ -> /issues/>#issuecomment-<id>
 *
 * `gh` does not return `html_url` on these payloads, so we reconstruct from
 * the PR URL + comment id. The PR URL itself is canonical (e.g.,
 * https://github.com/Azure/azure-dev/pull/8459), so a simple string ops pass
 * is enough.
 */
export function synthesizeCommentUrl(
  prUrl: string | undefined,
  source: CommentSource,
  id: number | undefined,
  prNumber: number | undefined,
): string | undefined {
  if (!prUrl || id == null || !Number.isFinite(id)) return undefined;
  switch (source) {
    case "inline":
      return `${prUrl}#discussion_r${id}`;
    case "review":
      return `${prUrl}#pullrequestreview-${id}`;
    case "issue": {
      // Issue comments live on the /issues/ URL, not /pull/.
      // We rewrite only the final /pull/<n> segment to /issues/<n>, leaving
      // any custom owner/repo paths intact.
      const issuesUrl =
        prNumber != null
          ? prUrl.replace(/\/pull\/(\d+)(?=$|[/?#])/, `/issues/$1`)
          : prUrl;
      return `${issuesUrl}#issuecomment-${id}`;
    }
    default:
      return undefined;
  }
}

function main(): void {
  const opts = parseArgs(process.argv.slice(2));
  const stats: FullStats = {
    kept: 0,
    dropped: {
      bot: 0,
      short: 0,
      quoted: 0,
      self: 0,
      association: 0,
      kindFiltered: 0,
      sourceFiltered: 0,
      total: 0,
    },
    prSkipped: 0,
  };
  const kept: KeptComment[] = [];

  for (const file of opts.files) {
    let data: PullRequestData;
    try {
      data = JSON.parse(fs.readFileSync(file, "utf8")) as PullRequestData;
    } catch (e) {
      throw new Error(`cannot read ${file}: ${(e as Error).message}`);
    }
    const result = filterPullRequestData(data, opts);
    if (result.prSkipped) stats.prSkipped++;
    kept.push(...result.kept);
    stats.kept += result.kept.length;
    for (const k of [
      "bot",
      "short",
      "quoted",
      "self",
      "association",
      "kindFiltered",
      "sourceFiltered",
    ] as const) {
      stats.dropped[k] += result.dropped[k];
      stats.dropped.total += result.dropped[k];
    }
  }

  process.stdout.write(JSON.stringify({ kept, stats }, null, 2) + "\n");
}

// Only run when invoked directly, not when imported by tests.
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  try {
    main();
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    log.error(message);
    process.exit(1);
  }
}
