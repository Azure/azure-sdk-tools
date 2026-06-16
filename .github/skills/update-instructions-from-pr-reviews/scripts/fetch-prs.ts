#!/usr/bin/env node
/**
 * fetch-prs.ts — List PR numbers + metadata for a repo and fetch
 * reviews/comments into cache files.
 */

import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import type {
  InlineComment,
  IssueComment,
  PullRequestData,
  ReviewSummary,
  User,
} from "./types.ts";
import {
  ghApiJsonAsync,
  ghJsonSync,
  makeLogger,
  runWithConcurrency,
} from "./utils.ts";

const log = makeLogger("fetch-prs");

function usage(): string {
  return [
    "Usage:",
    "  node scripts/fetch-prs.ts [options]",
    "",
    "Options:",
    "  --repo <owner/repo>                Override repo; defaults to cwd's git remote",
    "  --state <open|closed|merged|all>   Default: merged",
    "  --limit <N>                        Default: 50 (gh caps at 1000)",
    '  --since <YYYY-MM-DD>               Adds "merged:>=<date>" to search',
    "  --author <login>                   Filter by PR author",
    "  --label <name>                     Filter by label (repeatable)",
    "  --reviewer <login>                 Filter by reviewer (repeatable)",
    '  --search "<raw>"                   Raw search string, appended verbatim',
    "  --number <N>                       Explicit PR number (repeatable)",
    "  --concurrency <N>                  Max parallel fetches (default: 8)",
    "  --cache-dir <path>                 Override fetch cache dir",
    "  --force                            Re-fetch cache files",
    "  --verbose                          Print diagnostic progress to stderr",
    "  --quiet                            Suppress diagnostics (takes precedence over --verbose)",
    "  --format <json|summary>",
    "                                     Output format selector (default: json)",
    "  --summary-limit <N>                Number of PRs to show in summary (default: 10)",
    "  -h, --help                         Show this help",
  ].join("\n");
}

function parseArgs(argv: string[]): Options {
  const parsed = nodeParseArgs({
    args: argv,
    options: {
      repo: { type: "string" },
      state: { type: "string", default: "merged" },
      limit: { type: "string", default: "50" },
      since: { type: "string" },
      author: { type: "string" },
      label: { type: "string", multiple: true },
      reviewer: { type: "string", multiple: true },
      search: { type: "string" },
      number: { type: "string", multiple: true },
      concurrency: { type: "string", default: "8" },
      "cache-dir": { type: "string" },
      force: { type: "boolean", default: false },
      verbose: { type: "boolean", default: false },
      quiet: { type: "boolean", default: false },
      format: { type: "string" },
      "summary-limit": { type: "string", default: "10" },
      help: { type: "boolean", short: "h", default: false },
    },
    allowPositionals: false,
    strict: true,
  });

  if (parsed.values.help) {
    process.stdout.write(`${usage()}\n`);
    process.exit(0);
  }

  const numbers = parsed.values.number ?? [];
  for (const n of numbers) {
    if (!/^\d+$/.test(n)) throw new Error(`invalid --number "${n}"`);
  }

  const modeRaw = parsed.values.format ?? "json";
  if (modeRaw !== "json" && modeRaw !== "summary") {
    throw new Error(`invalid --format "${modeRaw}"; expected json|summary`);
  }

  const out: Options = {
    repo: parsed.values.repo,
    state: parsed.values.state,
    limit: parsed.values.limit,
    since: parsed.values.since,
    author: parsed.values.author,
    labels: parsed.values.label ?? [],
    reviewers: parsed.values.reviewer ?? [],
    search: parsed.values.search,
    numbers,
    format: modeRaw,
    concurrency: parsed.values.concurrency,
    cacheDir: parsed.values["cache-dir"],
    force: parsed.values.force,
    verbose: parsed.values.verbose,
    quiet: parsed.values.quiet,
    summaryLimit: parsed.values["summary-limit"],
  };

  if (!/^\d+$/.test(out.concurrency) || Number(out.concurrency) < 1) {
    throw new Error(`invalid --concurrency "${out.concurrency}"`);
  }

  if (!/^\d+$/.test(out.summaryLimit) || Number(out.summaryLimit) < 1) {
    throw new Error(`invalid --summary-limit "${out.summaryLimit}"`);
  }

  return out;
}

interface Options {
  repo?: string;
  state: string;
  limit: string;
  since?: string;
  author?: string;
  labels: string[];
  reviewers: string[];
  search?: string;
  numbers: string[];
  format: "json" | "summary";
  concurrency: string;
  cacheDir?: string;
  force: boolean;
  verbose: boolean;
  quiet: boolean;
  summaryLimit: string;
}

interface PullRequest {
  number: number;
  title: string;
  author: { login: string } | null;
  mergedAt: string | null;
  url: string;
  state: string;
}

interface FetchOutput {
  number: number;
  file: string;
  cacheHit: boolean;
}

interface RawUser {
  login: string;
  type: "User" | "Bot";
}

interface RawPr {
  number: number;
  title: string;
  user: RawUser | null;
  html_url: string;
  state: string;
  merged_at: string | null;
}

interface RawReview {
  id: number;
  state: string;
  body: string;
  submitted_at: string;
  user: RawUser | null;
}

interface RawInline {
  id: number;
  path?: string;
  line?: number;
  original_line?: number;
  body: string;
  diff_hunk?: string;
  in_reply_to_id?: number;
  pull_request_review_id?: number;
  created_at?: string;
  user: RawUser | null;
}

interface RawIssue {
  id: number;
  body: string;
  created_at: string;
  user: RawUser | null;
}

interface RepoView {
  nameWithOwner: string;
}

function resolveRepo(repo?: string): string {
  if (repo) {
    if (!/^[^/\s]+\/[^/\s]+$/.test(repo)) {
      throw new Error(`invalid --repo "${repo}"; expected owner/repo`);
    }
    return repo;
  }

  const resolved = ghJsonSync<RepoView>([
    "repo",
    "view",
    "--json",
    "nameWithOwner",
  ]);
  // gh JSON output is typed optimistically; guard against a missing field.
  // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON boundary
  const repoName = resolved.nameWithOwner?.trim() ?? "";

  if (repoName.length === 0) {
    throw new Error("gh repo view returned no repo name");
  }

  return repoName;
}

export function buildSearch(opts: Options): string {
  const parts: string[] = [];
  if (opts.since) parts.push(`merged:>=${opts.since}`);
  if (opts.author) parts.push(`author:${opts.author}`);
  for (const l of opts.labels) parts.push(`label:"${l}"`);
  for (const r of opts.reviewers) parts.push(`reviewer:${r}`);
  if (opts.search) parts.push(opts.search);
  return parts.join(" ");
}

function slimUser(u: RawUser | null): User | null {
  return u ? { login: u.login, type: u.type } : null;
}

function resolveCacheDir(repo: string, cacheDir?: string): string {
  const [owner, repoName] = repo.split("/");
  return cacheDir ?? path.join("pr-cache", `${owner}-${repoName}`);
}

async function fetchPrToCache(
  opts: Options,
  number: number,
): Promise<FetchOutput> {
  // opts.repo is populated by resolveRepo() in main() before any fetch runs.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- set in main()
  const cacheDir = resolveCacheDir(opts.repo!, opts.cacheDir);
  const outFile = path.join(cacheDir, `pr-${number}.json`);

  if (fs.existsSync(outFile) && !opts.force) {
    log.info(`cache hit ${outFile}`);
    return { number, file: outFile, cacheHit: true };
  }

  fs.mkdirSync(cacheDir, { recursive: true });

  const base = `repos/${opts.repo}`;
  log.info(`PR #${number} metadata`);
  const prRaw = await ghApiJsonAsync<RawPr>(`${base}/pulls/${number}`);

  log.info(`PR #${number} reviews`);
  const reviewsRaw = await ghApiJsonAsync<RawReview[]>(
    `${base}/pulls/${number}/reviews`,
  );

  log.info(`PR #${number} inline comments`);
  const inlineRaw = await ghApiJsonAsync<RawInline[]>(
    `${base}/pulls/${number}/comments`,
  );

  log.info(`PR #${number} issue comments`);
  const issueRaw = await ghApiJsonAsync<RawIssue[]>(
    `${base}/issues/${number}/comments`,
  );

  const payload: PullRequestData = {
    pr: {
      number: prRaw.number,
      title: prRaw.title,
      author: slimUser(prRaw.user),
      url: prRaw.html_url,
      state: prRaw.state,
      mergedAt: prRaw.merged_at,
    },
    reviews: reviewsRaw.map(
      (r): ReviewSummary => ({
        // slim this down a bit, there are a lot of fields in the actual JSON
        id: r.id,
        state: r.state,
        body: r.body,
        submitted_at: r.submitted_at,
        user: slimUser(r.user),
      }),
    ),
    inline: inlineRaw.map(
      (c): InlineComment => ({
        // slim this down a bit, there are a lot of fields in the actual JSON
        id: c.id,
        path: c.path,
        line: c.line,
        original_line: c.original_line,
        body: c.body,
        diff_hunk: c.diff_hunk,
        in_reply_to_id: c.in_reply_to_id,
        pull_request_review_id: c.pull_request_review_id,
        created_at: c.created_at,
        user: slimUser(c.user),
      }),
    ),
    issue: issueRaw.map(
      (c): IssueComment => ({
        // slim this down a bit, there are a lot of fields in the actual JSON
        id: c.id,
        body: c.body,
        created_at: c.created_at,
        user: slimUser(c.user),
      }),
    ),
  };

  fs.writeFileSync(outFile, JSON.stringify(payload, null, 2));
  log.info(
    `wrote ${outFile} (reviews=${payload.reviews.length} inline=${payload.inline.length} issue=${payload.issue.length})`,
  );

  return { number, file: outFile, cacheHit: false };
}

function listFromSearch(opts: Options): PullRequest[] {
  const args = [
    "pr",
    "list",
    "--repo",
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- set in main()
    opts.repo!,
    "--state",
    opts.state,
    "--limit",
    opts.limit,
    "--json",
    "number,title,author,mergedAt,url,state",
  ];
  const search = buildSearch(opts);
  if (search) args.push("--search", search);

  log.info(`gh ${args.join(" ")}`);
  const prs = ghJsonSync<PullRequest[]>(args);
  log.info(`${prs.length} PR(s) found`);
  return prs;
}

function printSummary(
  prs: PullRequest[],
  summaryLimit: number,
  fetched: FetchOutput[],
): void {
  process.stdout.write(`Total PRs fetched: ${prs.length}\n\n`);

  const shown = prs.slice(0, summaryLimit);
  for (const pr of shown) {
    const author = pr.author?.login ?? "unknown";
    process.stdout.write(`  #${pr.number}: ${pr.title} (@${author})\n`);
  }

  const remaining = prs.length - shown.length;
  if (remaining > 0) {
    process.stdout.write(`  ... and ${remaining} more\n`);
  }

  const cacheHits = fetched.filter((item) => item.cacheHit).length;
  const written = fetched.length - cacheHits;
  process.stdout.write(
    `\nCache files: ${fetched.length} total (${written} written, ${cacheHits} cache hit)\n`,
  );
}

async function listFromNumbers(opts: Options): Promise<PullRequest[]> {
  const unique = Array.from(new Set(opts.numbers.map((n) => Number(n))));
  log.info(`loading ${unique.length} explicit PR(s)`);
  return runWithConcurrency(unique, Number(opts.concurrency), async (n) => {
    const raw = await ghApiJsonAsync<RawPr>(`repos/${opts.repo}/pulls/${n}`);
    return {
      number: raw.number,
      title: raw.title,
      author: raw.user ? { login: raw.user.login } : null,
      mergedAt: raw.merged_at,
      url: raw.html_url,
      state: raw.state,
    };
  });
}

async function main(): Promise<void> {
  const opts = parseArgs(process.argv.slice(2));
  log.enabled = opts.verbose && !opts.quiet;
  opts.repo = resolveRepo(opts.repo);
  log.info(`repo ${opts.repo}`);
  const prs =
    opts.numbers.length > 0
      ? await listFromNumbers(opts)
      : listFromSearch(opts);

  log.info(
    `fetching ${prs.length} PR(s) with concurrency=${opts.concurrency}`,
  );

  const fetched = await runWithConcurrency(
    prs,
    Number(opts.concurrency),
    async (pr): Promise<FetchOutput> => fetchPrToCache(opts, pr.number),
  );

  if (opts.format === "summary") {
    printSummary(prs, Number(opts.summaryLimit), fetched);
    return;
  }

  process.stdout.write(
    JSON.stringify(
      {
        prs,
        fetched,
      },
      null,
      2,
    ) + "\n",
  );
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((err: unknown) => {
    const message = err instanceof Error ? err.message : String(err);
    log.error(message);
    process.exit(1);
  });
}
