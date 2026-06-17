#!/usr/bin/env node
/**
 * replay-pull-request.ts — Replay a PR's review rounds one commit at a time and optionally
 * test each round against Copilot with a candidate .github/ folder.
 *
 * A "review round" is a commit SHA that had human (non-bot) review comments
 * associated with it. For each round, replay-pull-request can invoke setup-pr.ts to create
 * a test PR at that commit state, inject the candidate .github/ instructions,
 * and wait for Copilot to review.
 */

import { spawn } from "node:child_process";
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import * as path from "node:path";

import {
    formatElapsed,
    ghApiJsonSync,
    isBot,
    makeLogger,
    parsePositiveInt,
    runWithConcurrency,
} from "./utils.ts";

const log = makeLogger("replay-pull-request");

function usage(): string {
    return [
        "Usage:",
        "  node scripts/replay-pull-request.ts --repo owner/repo --number <N> [options]",
        "",
        "Options:",
        "  --repo <owner/repo>       Repository (required)",
        "  --number <N>              PR number (repeatable)",
        "  --input-json-stdin        Read candidate JSON from stdin",
        "  --github <path>           .github/ folder to inject (passed to setup-pr)",
        "  --test-repo <path>        Local repo path for worktrees (required)",
        "  --branch <prefix>         Branch name prefix (default: step-test)",
        "  --concurrency <N>         Max PRs to process in parallel (default: 1)",
        "  --dry-run                 Print what would be done without creating PRs",
        "  --round <N>               Only process the Nth round (1-indexed)",
        `  --wait-timeout-sec <N>    Max wait for Copilot per round (default: ${DEFAULT_WAIT_TIMEOUT_SEC})`,
        `  --wait-interval-sec <N>   Poll interval (default: ${DEFAULT_WAIT_INTERVAL_SEC})`,
        "  -h, --help                Show this help",
    ].join("\n");
}

export function parseCli(argv: string[]): Options {
    const parsed = parseArgs({
        args: argv,
        options: {
            repo: { type: "string" },
            number: { type: "string", multiple: true },
            "input-json-stdin": { type: "boolean", default: false },
            github: { type: "string" },
            "test-repo": { type: "string" },
            branch: { type: "string", default: "step-test" },
            concurrency: { type: "string", default: "1" },
            "dry-run": { type: "boolean", default: false },
            round: { type: "string" },
            "wait-timeout-sec": {
                type: "string",
                default: String(DEFAULT_WAIT_TIMEOUT_SEC),
            },
            "wait-interval-sec": {
                type: "string",
                default: String(DEFAULT_WAIT_INTERVAL_SEC),
            },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });

    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        process.exit(0);
    }

    const repoRaw = parsed.values.repo;
    if (!repoRaw) {
        throw new Error("--repo is required");
    }
    if (!/^[^/\s]+\/[^/\s]+$/.test(repoRaw)) {
        throw new Error(`invalid --repo "${repoRaw}"; expected owner/repo`);
    }

    const numbersRaw = parsed.values.number ?? [];
    for (const raw of numbersRaw) {
        if (!/^\d+$/.test(raw)) {
            throw new Error(
                `invalid --number ${JSON.stringify(raw)}; expected positive integer`,
            );
        }
    }

    return {
        repo: repoRaw,
        numbers: numbersRaw.map((n) => parsePositiveInt(n, "--number")),
        inputJsonStdin: parsed.values["input-json-stdin"],
        github: parsed.values.github,
        testRepo: parsed.values["test-repo"],
        branch: parsed.values.branch,
        concurrency: parsePositiveInt(
            parsed.values.concurrency,
            "--concurrency",
        ),
        dryRun: parsed.values["dry-run"],
        round: parsed.values.round
            ? parsePositiveInt(parsed.values.round, "--round")
            : undefined,
        waitTimeoutSec: parsePositiveInt(
            parsed.values["wait-timeout-sec"],
            "--wait-timeout-sec",
        ),
        waitIntervalSec: parsePositiveInt(
            parsed.values["wait-interval-sec"],
            "--wait-interval-sec",
        ),
    };
}

const DEFAULT_WAIT_TIMEOUT_SEC = 900;
const DEFAULT_WAIT_INTERVAL_SEC = 20;

export interface Options {
    repo: string;
    numbers: number[];
    inputJsonStdin: boolean;
    github?: string;
    testRepo?: string;
    branch: string;
    concurrency: number;
    dryRun: boolean;
    round?: number;
    waitTimeoutSec: number;
    waitIntervalSec: number;
}

interface CandidateLike {
    pr?: number;
    number?: number;
}

interface CandidateEnvelope {
    candidates?: CandidateLike[];
}

interface RawUser {
    login: string;
    type: "User" | "Bot";
}

interface RawCommit {
    sha: string;
    commit: {
        message: string;
    };
}

interface RawReview {
    id: number;
    state: string;
    body: string;
    commit_id: string;
    submitted_at: string;
    user: RawUser | null;
}

interface RawInline {
    id: number;
    path?: string;
    line?: number;
    body: string;
    original_commit_id?: string;
    commit_id?: string;
    created_at?: string;
    user: RawUser | null;
}

interface PrMeta {
    base: { sha: string };
    head: { sha: string };
    merged_at: string | null;
    merge_commit_sha: string | null;
    title: string;
}

export interface ReviewRound {
    index: number;
    commitSha: string;
    commitMessage: string;
    humanReviewCount: number;
    humanInlineCount: number;
    reviews: {
        user: string;
        state: string;
        bodyLength: number;
    }[];
    inlineFiles: string[];
}

function parseCandidateJson(raw: string): number[] {
    let parsed: unknown;
    try {
        parsed = JSON.parse(raw);
    } catch (err) {
        throw new Error(`invalid candidate JSON: ${(err as Error).message}`);
    }

    const values: number[] = [];
    if (Array.isArray(parsed)) {
        for (const item of parsed as CandidateLike[]) {
            // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON entries may be null
            const pr = item?.pr ?? item?.number;
            if (typeof pr === "number") values.push(pr);
        }
        return values;
    }

    const envelope = parsed as CandidateEnvelope;
    if (Array.isArray(envelope.candidates)) {
        for (const item of envelope.candidates) {
            // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON entries may be null
            const pr = item?.pr ?? item?.number;
            if (typeof pr === "number") values.push(pr);
        }
    }
    return values;
}

function resolveTargetPrNumbers(opts: Options): number[] {
    const out = new Set<number>(opts.numbers);

    if (opts.inputJsonStdin) {
        try {
            const raw = fs.readFileSync("/dev/stdin", "utf8");
            for (const n of parseCandidateJson(raw)) out.add(n);
        } catch (err) {
            throw new Error(
                `failed to read candidate JSON from stdin: ${(err as Error).message}`,
            );
        }
    }

    const targets = Array.from(out).sort((a, b) => a - b);
    if (targets.length === 0) {
        throw new Error(
            "no PR numbers provided; pass --number or --input-json-stdin",
        );
    }
    return targets;
}

export function buildRounds(
    commits: RawCommit[],
    reviews: RawReview[],
    inline: RawInline[],
): ReviewRound[] {
    const humanReviews = reviews.filter((r) => !isBot(r.user));
    const humanInline = inline.filter((c) => !isBot(c.user));

    // Build a map: commit SHA → reviews and inline comments
    const reviewsByCommit = new Map<string, RawReview[]>();
    for (const r of humanReviews) {
        const bucket = reviewsByCommit.get(r.commit_id) ?? [];
        bucket.push(r);
        reviewsByCommit.set(r.commit_id, bucket);
    }

    const inlineByCommit = new Map<string, RawInline[]>();
    for (const c of humanInline) {
        // Use original_commit_id first (the commit the comment was authored on),
        // fall back to commit_id (the latest commit at the time the comment was posted).
        const sha = c.original_commit_id ?? c.commit_id ?? "";
        if (sha.length === 0) continue;
        const bucket = inlineByCommit.get(sha) ?? [];
        bucket.push(c);
        inlineByCommit.set(sha, bucket);
    }

    const rounds: ReviewRound[] = [];
    let roundIndex = 0;

    for (const commit of commits) {
        const commitReviews = reviewsByCommit.get(commit.sha) ?? [];
        const commitInline = inlineByCommit.get(commit.sha) ?? [];

        // Only include commits that had human feedback
        if (commitReviews.length === 0 && commitInline.length === 0) continue;

        roundIndex++;
        const inlineFiles = [
            ...new Set(
                commitInline.map((c) => c.path).filter(Boolean) as string[],
            ),
        ];

        rounds.push({
            index: roundIndex,
            commitSha: commit.sha,
            commitMessage: commit.commit.message.split("\n")[0] ?? "",
            humanReviewCount: commitReviews.length,
            humanInlineCount: commitInline.length,
            reviews: commitReviews.map((r) => ({
                user: r.user?.login ?? "unknown",
                state: r.state,
                // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- body may be absent in external JSON
                bodyLength: r.body?.length ?? 0,
            })),
            inlineFiles,
        });
    }

    return rounds;
}

function printRound(round: ReviewRound): void {
    const total = round.humanReviewCount + round.humanInlineCount;
    process.stdout.write(
        `Round ${round.index}: ${round.commitSha.slice(0, 12)} — ` +
            `${total} human comment${total === 1 ? "" : "s"} ` +
            `(${round.humanReviewCount} review${round.humanReviewCount === 1 ? "" : "s"}, ` +
            `${round.humanInlineCount} inline)\n`,
    );
    process.stdout.write(`  commit: ${round.commitMessage}\n`);
    for (const r of round.reviews) {
        process.stdout.write(
            `  review by ${r.user}: ${r.state}${r.bodyLength > 0 ? ` (${r.bodyLength} chars)` : ""}\n`,
        );
    }
    if (round.inlineFiles.length > 0) {
        process.stdout.write(
            `  inline on: ${round.inlineFiles.slice(0, 5).join(", ")}` +
                (round.inlineFiles.length > 5
                    ? ` (+${round.inlineFiles.length - 5} more)`
                    : "") +
                "\n",
        );
    }
}

export function buildSetupPrArgs(
    opts: Options,
    round: ReviewRound,
    baseSha: string,
): { scriptPath: string; args: string[]; branchName: string } {
    const branchName = `${opts.branch}-r${round.index}`;
    const scriptPath = path.join(
        path.dirname(fileURLToPath(import.meta.url)),
        "setup-pr.ts",
    );

    const args = [
        scriptPath,
        "--base-commit",
        baseSha,
        "--pr-commit",
        round.commitSha,
        "--repo",
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- validated in main()
        opts.testRepo!,
        "--branch",
        branchName,
        "--wait",
        "--wait-timeout-sec",
        String(opts.waitTimeoutSec),
        "--wait-interval-sec",
        String(opts.waitIntervalSec),
    ];

    if (opts.github) {
        args.push("--github", opts.github);
    }

    return { scriptPath, args, branchName };
}

export function extractPrUrlFromSetupOutput(output: string): string {
    const prMatch = /^PR created: (https:\/\/github\.com\/[^\s]+)$/m.exec(
        output,
    );
    return prMatch?.[1] ?? "";
}

export async function runSetupPr(
    opts: Options,
    round: ReviewRound,
    baseSha: string,
): Promise<string> {
    const { args, branchName } = buildSetupPrArgs(opts, round, baseSha);

    process.stdout.write(
        `  creating test PR: branch=${branchName} ` +
            `base=${baseSha.slice(0, 12)} pr=${round.commitSha.slice(0, 12)}\n`,
    );

    const child = spawn("node", args, {
        cwd: opts.testRepo,
        stdio: ["inherit", "pipe", "pipe"],
    });

    let output = "";
    let spawnErrorMessage = "";

    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- stdout is null unless piped
    child.stdout?.on("data", (chunk: Buffer | string) => {
        const text = String(chunk);
        output += text;
        process.stdout.write(text);
    });

    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- stderr is null unless piped
    child.stderr?.on("data", (chunk: Buffer | string) => {
        const text = String(chunk);
        output += text;
        process.stderr.write(text);
    });

    child.on("error", (err: unknown) => {
        spawnErrorMessage = err instanceof Error ? err.message : String(err);
    });

    const exitCode = await new Promise<number | null>((resolve) => {
        child.on("close", (code: number | null) => {
            resolve(code);
        });
    });

    if (spawnErrorMessage.length > 0) {
        process.stderr.write(`  setup-pr failed: ${spawnErrorMessage}\n`);
        return "";
    }
    if (exitCode !== 0) {
        process.stderr.write(`  setup-pr exited ${String(exitCode)}\n`);
        return "";
    }

    const prUrl = extractPrUrlFromSetupOutput(output);
    if (prUrl) {
        process.stdout.write(`  test PR: ${prUrl}\n`);
    }

    // Check for Copilot review confirmation
    if (/Copilot review found/i.test(output)) {
        process.stdout.write("  ✓ Copilot review received\n");
    }

    return prUrl;
}

async function processSinglePr(
    baseRepoOpts: Options,
    prNumber: number,
    branchPrefix: string,
): Promise<{
    pr: number;
    rounds: {
        round: number;
        commitSha: string;
        humanComments: number;
        testPrUrl: string;
    }[];
}> {
    const opts: Options = {
        ...baseRepoOpts,
        numbers: [prNumber],
        branch: branchPrefix,
    };

    const base = `repos/${opts.repo}`;
    log.info(`loading ${opts.repo}#${prNumber}`);

    // Fetch PR metadata to get base SHA
    const pr = ghApiJsonSync<PrMeta>(`${base}/pulls/${prNumber}`);
    const baseSha = pr.base.sha;

    log.info(`base=${baseSha.slice(0, 12)} head=${pr.head.sha.slice(0, 12)}`);

    // Fetch commits
    const commits = ghApiJsonSync<RawCommit[]>(
        `${base}/pulls/${prNumber}/commits?per_page=100`,
    );
    log.info(`${commits.length} commit(s)`);

    // Fetch reviews and inline comments
    const reviews = ghApiJsonSync<RawReview[]>(
        `${base}/pulls/${prNumber}/reviews`,
    );
    const inline = ghApiJsonSync<RawInline[]>(
        `${base}/pulls/${prNumber}/comments`,
    );

    const rounds = buildRounds(commits, reviews, inline);
    log.info(`${rounds.length} round(s) with human feedback`);

    if (rounds.length === 0) {
        process.stdout.write(
            `No review rounds with human feedback found for ${opts.repo}#${prNumber}.\n`,
        );
        return { pr: prNumber, rounds: [] };
    }

    // Filter to specific round if requested
    const targetRounds =
        opts.round != null
            ? rounds.filter((r) => r.index === opts.round)
            : rounds;

    if (opts.round != null && targetRounds.length === 0) {
        throw new Error(
            `round ${opts.round} not found (${rounds.length} rounds available)`,
        );
    }

    const results: {
        round: number;
        commitSha: string;
        humanComments: number;
        testPrUrl: string;
    }[] = [];

    for (const round of targetRounds) {
        printRound(round);

        if (opts.dryRun) {
            process.stdout.write("  (dry-run — skipping PR creation)\n\n");
            continue;
        }

        const prUrl = await runSetupPr(opts, round, baseSha);
        results.push({
            round: round.index,
            commitSha: round.commitSha,
            humanComments: round.humanReviewCount + round.humanInlineCount,
            testPrUrl: prUrl,
        });
        process.stdout.write("\n");
    }

    return { pr: prNumber, rounds: results };
}

async function main(): Promise<void> {
    const opts = parseCli(process.argv.slice(2));

    if (!opts.testRepo) {
        throw new Error("--test-repo is required");
    }

    const targetPrs = resolveTargetPrNumbers(opts);
    log.info(
        `running ${targetPrs.length} PR(s) with concurrency=${opts.concurrency}`,
    );

    const waveStartMs = Date.now();

    const perPr = await runWithConcurrency(
        targetPrs,
        opts.concurrency,
        async (prNumber) => {
            const branchPrefix =
                targetPrs.length > 1
                    ? `${opts.branch}-${prNumber}`
                    : opts.branch;
            return processSinglePr(opts, prNumber, branchPrefix);
        },
        (event) => {
            const waiting = event.total - event.completed - event.active;
            const elapsed = formatElapsed(Date.now() - waveStartMs);
            if (event.type === "start") {
                log.info(
                    `progress start #${event.item} | completed=${event.completed}/${event.total} active=${event.active} waiting=${waiting} elapsed=${elapsed}`,
                );
            } else {
                log.info(
                    `progress done  #${event.item} | completed=${event.completed}/${event.total} active=${event.active} waiting=${waiting} elapsed=${elapsed}`,
                );
            }
        },
    );

    log.info(`wave complete in ${formatElapsed(Date.now() - waveStartMs)}`);

    if (!opts.dryRun) {
        const nonEmpty = perPr.filter((item) => item.rounds.length > 0);
        if (targetPrs.length === 1) {
            process.stdout.write(
                "\n" +
                    JSON.stringify(
                        { rounds: nonEmpty[0]?.rounds ?? [] },
                        null,
                        2,
                    ) +
                    "\n",
            );
        } else {
            process.stdout.write(
                "\n" + JSON.stringify({ prs: perPr }, null, 2) + "\n",
            );
        }
    }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((err: unknown) => {
        const message = err instanceof Error ? err.message : String(err);
        log.error(message);
        process.exit(1);
    });
}
