#!/usr/bin/env node

import { rmSync } from "node:fs";
import { cp as copyPath, mkdir, rm } from "node:fs/promises";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import {
  formatElapsed,
  makeLogger,
  parsePositiveInt,
  runGhSync,
  runGitSync,
  sleep,
} from "./utils.ts";

const log = makeLogger("setup-pr");

// this is the copilot account we add in later so copilot will review the PR
const COPILOT_REVIEWER = "copilot-pull-request-reviewer[bot]";

// and this is the copilot login that's used when it posts its review.
const COPILOT_REVIEWER_API = "copilot-pull-request-reviewer";
// Inline comments are posted by a separate "Copilot" user that arrives after
// the review summary. We poll for these in a stabilization loop.
const COPILOT_INLINE_LOGIN = "Copilot";
const DEFAULT_WAIT_TIMEOUT_SEC = 900;
const DEFAULT_WAIT_INTERVAL_SEC = 20;
const DEFAULT_INLINE_STABILIZE_SEC = 60;
const DEFAULT_INLINE_STABILIZE_POLLS = 3;

function usage(): string {
  return [
    "Usage:",
    "  node ./setup-pr.ts --base-commit <sha> --pr-commit <sha> --repo <path> --branch <name> [--github <path>]",
    "",
    "Options:",
    "  --base-commit <sha>  Git commit for base branch",
    "  --pr-commit <sha>    Git commit for PR branch",
    "  --repo <path>        Path to repository containing both commits",
    "  --branch <name>      Base branch name",
    "  --github <path>      Optional folder copied into worktree/.github and committed",
    "  --wait               Wait for copilot-pull-request-reviewer[bot] review/comment",
    "  --keep-worktree      Keep local worktree/branches after creating test PR",
    `  --wait-timeout-sec <n>   Maximum seconds to wait (default: ${DEFAULT_WAIT_TIMEOUT_SEC})`,
    `  --wait-interval-sec <n>  Poll interval in seconds (default: ${DEFAULT_WAIT_INTERVAL_SEC})`,
    "  -h, --help           Show this help",
  ].join("\n");
}

export function parseCliArgs(argv: string[]): Args {
  const parsed = parseArgs({
    args: argv,
    options: {
      "base-commit": { type: "string" },
      "pr-commit": { type: "string" },
      repo: { type: "string" },
      branch: { type: "string" },
      github: { type: "string" },
      wait: { type: "boolean", default: true },
      "keep-worktree": { type: "boolean", default: false },
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
    console.log(usage());
    process.exit(0);
  }

  const baseCommit = parsed.values["base-commit"];
  const prCommit = parsed.values["pr-commit"];
  const repo = parsed.values.repo;
  const branch = parsed.values.branch;

  if (!baseCommit || !prCommit || !repo || !branch) {
    throw new Error(
      "--base-commit, --pr-commit, --repo, and --branch are required",
    );
  }

  return {
    baseCommit,
    prCommit,
    repo,
    branch,
    github: parsed.values.github,
    wait: parsed.values.wait,
    keepWorktree: parsed.values["keep-worktree"],
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

export interface Args {
  baseCommit: string;
  prCommit: string;
  repo: string;
  branch: string;
  github?: string;
  wait: boolean;
  waitTimeoutSec: number;
  waitIntervalSec: number;
  keepWorktree: boolean;
}

export interface CreatedPullRequest {
  number: string;
  url: string;
}

export function cleanupOldState(
  repo: string,
  baseBranchName: string,
  prBranchName: string,
  worktreePath: string,
): void {
  log.info(
    `cleaning up old state for ${JSON.stringify(baseBranchName)} / ${JSON.stringify(prBranchName)}`,
  );

  const cleanupSteps = [
    () => runGitSync(repo, "worktree", "remove", "--force", worktreePath),
    () => { rmSync(worktreePath, { recursive: true, force: true }); },
    () => runGitSync(repo, "worktree", "prune"),
    () => runGhSync(repo, "pr", "close", prBranchName),
    () => runGitSync(repo, "branch", "-D", baseBranchName),
    () => runGitSync(repo, "branch", "-D", prBranchName),
    () => runGitSync(repo, "push", "origin", "--delete", baseBranchName),
    () => runGitSync(repo, "push", "origin", "--delete", prBranchName),
  ];

  for (const cleanupStep of cleanupSteps) {
    try {
      cleanupStep();
    } catch {
      // Cleanup is intentionally best-effort.
    }
  }
}

export function cleanupEphemeralWorkspace(
  repo: string,
  baseBranchName: string,
  prBranchName: string,
  worktreePath: string,
): void {
  const cleanupSteps = [
    () => runGitSync(repo, "worktree", "remove", "--force", worktreePath),
    () => { rmSync(worktreePath, { recursive: true, force: true }); },
    () => runGitSync(repo, "worktree", "prune"),
    () => runGitSync(repo, "branch", "-D", baseBranchName),
    () => runGitSync(repo, "branch", "-D", prBranchName),
  ];

  for (const cleanupStep of cleanupSteps) {
    try {
      cleanupStep();
    } catch {
      // Cleanup is intentionally best-effort.
    }
  }
}

/**
 * Use the specified .github folder, rather than relying on what's in the base branch.
 * This also makes it simple to test old code with newer instructions.
 */
export async function setupDotGitHubFolder(
  sourceDotGithubFolder: string,
  worktreePath: string,
): Promise<void> {
  const destDotGithubFolder = path.join(worktreePath, ".github");
  await rm(destDotGithubFolder, { recursive: true, force: true });
  await mkdir(path.dirname(destDotGithubFolder), { recursive: true });
  await copyPath(sourceDotGithubFolder, destDotGithubFolder, {
    recursive: true,
    force: true,
  });

  runGitSync(destDotGithubFolder, "add", ".");
  runGitSync(
    destDotGithubFolder,
    "commit",
    "--allow-empty",
    "-m",
    `Committing .github folder from ${sourceDotGithubFolder}`,
    ".",
  );
}

export async function waitForCopilotReview(
  prNumber: string,
  cwd: string,
  timeoutSec: number,
  intervalSec: number,
): Promise<void> {
  const start = Date.now();
  const timeoutMs = timeoutSec * 1000;
  const intervalMs = intervalSec * 1000;

  process.stdout.write(
    `Waiting for ${COPILOT_REVIEWER} (timeout ${timeoutSec}s, interval ${intervalSec}s)...\n`,
  );

  // Phase 1: Wait for the review summary from copilot-pull-request-reviewer.
  const jsonFields = "reviews,comments";
  const jqFilter = `any((.reviews[]?.author.login, .comments[]?.author.login); . == ${JSON.stringify(COPILOT_REVIEWER_API)})`;

  while (Date.now() - start < timeoutMs) {
    const elapsedMs = Date.now() - start;
    const hasCopilotReview =
      runGhSync(
        cwd,
        "pr",
        "view",
        prNumber,
        "--json",
        jsonFields,
        "--jq",
        jqFilter,
      ).trim() === "true";

    if (hasCopilotReview) {
      process.stdout.write(
        `Copilot review found after ${formatElapsed(elapsedMs)}.\n`,
      );
      break;
    }

    process.stdout.write(
      `  waiting for review summary: elapsed ${formatElapsed(elapsedMs)} / ${timeoutSec}s\n`,
    );

    await sleep(intervalMs);
  }

  if (Date.now() - start >= timeoutMs) {
    throw new Error(`timed out waiting for copilot review on PR #${prNumber}`);
  }

  // Phase 2: Wait for inline comments from "Copilot" to stabilize.
  // Inline comments are posted by a separate user and may arrive after the
  // review summary. We poll until the count stops changing for a few cycles.
  await waitForInlineComments(prNumber, cwd, intervalSec, start);

  process.stdout.write(
    `Copilot wait complete in ${formatElapsed(Date.now() - start)}.\n`,
  );
}

/**
 * Poll `pulls/{pr}/comments` for inline comments from the "Copilot" user.
 * Return once the count has been stable for {@link DEFAULT_INLINE_STABILIZE_POLLS}
 * consecutive polls, or after {@link DEFAULT_INLINE_STABILIZE_SEC} seconds.
 */
export async function waitForInlineComments(
  prNumber: string,
  cwd: string,
  intervalSec: number,
  overallStartMs: number,
): Promise<void> {
  const intervalMs = intervalSec * 1000;
  const stabilizeTimeoutMs = DEFAULT_INLINE_STABILIZE_SEC * 1000;
  const start = Date.now();

  // Resolve owner/repo from the worktree's remote.
  const remoteUrl = runGhSync(
    cwd,
    "repo",
    "view",
    "--json",
    "nameWithOwner",
    "-q",
    ".nameWithOwner",
  ).trim();

  let prevCount = -1;
  let stablePolls = 0;

  process.stdout.write(
    `Waiting for inline comments from ${COPILOT_INLINE_LOGIN} ` +
      `(stabilize ${DEFAULT_INLINE_STABILIZE_POLLS} polls, ` +
      `timeout ${DEFAULT_INLINE_STABILIZE_SEC}s)...\n`,
  );

  while (Date.now() - start < stabilizeTimeoutMs) {
    await sleep(intervalMs);

    const countStr = runGhSync(
      cwd,
      "api",
      `repos/${remoteUrl}/pulls/${prNumber}/comments`,
      "--jq",
      `[.[] | select(.user.login == ${JSON.stringify(COPILOT_INLINE_LOGIN)})] | length`,
    ).trim();
    const count = Number.parseInt(countStr, 10) || 0;
    const phaseElapsed = formatElapsed(Date.now() - start);
    const overallElapsed = formatElapsed(Date.now() - overallStartMs);

    if (count === prevCount) {
      stablePolls++;
      process.stdout.write(
        `  inline wait: ${count} comments, stable ${stablePolls}/${DEFAULT_INLINE_STABILIZE_POLLS} ` +
          `(phase ${phaseElapsed}, overall ${overallElapsed})\n`,
      );
      if (stablePolls >= DEFAULT_INLINE_STABILIZE_POLLS) {
        process.stdout.write(
          `Inline comment count stabilized at ${count}.\n`,
        );
        return;
      }
    } else {
      process.stdout.write(
        `  inline comments: ${count} (was ${prevCount < 0 ? "?" : prevCount}, ` +
          `phase ${phaseElapsed}, overall ${overallElapsed})\n`,
      );
      stablePolls = 0;
    }

    prevCount = count;
  }

  const finalCount = prevCount < 0 ? 0 : prevCount;
  process.stdout.write(
    `Inline stabilization timeout reached (${finalCount} comments).\n`,
  );
}

export function extractCreatedPullRequest(
  prOutput: string,
): CreatedPullRequest {
  const prMatch = /^https:\/\/github\.com\/[^/]+\/[^/]+\/pull\/(\d+)$/m.exec(prOutput);
  if (!prMatch) {
    throw new Error(
      `failed to get a PR from output ${JSON.stringify(prOutput)}`,
    );
  }

  const pullRequestUrl = prMatch[0];
  const pullRequestNumber = prMatch[1];
  if (!pullRequestUrl || !pullRequestNumber) {
    throw new Error(
      `failed to extract PR details from output ${JSON.stringify(prOutput)}`,
    );
  }

  return {
    number: pullRequestNumber,
    url: pullRequestUrl,
  };
}

function createPullRequest(
  worktreePath: string,
  baseBranchName: string,
  prBranchName: string,
): CreatedPullRequest {
  const prOutput = runGhSync(
    worktreePath,
    "pr",
    "create",
    // this just keeps the repo a bit more manageable, and copilot can review draft PRs.
    "--draft",
    "-f", // auto-fill in most of the PR fields (we're not using them, and they don't impact the review)
    // the 'target' branch (we created this as well so we can have some localized testing of instructions)
    "-B",
    baseBranchName,
    // the actual branch for our PR changes
    "-H",
    prBranchName,
  );
  const { number: pullRequestNumber, url: pullRequestUrl } =
    extractCreatedPullRequest(prOutput);

  runGhSync(
    worktreePath,
    "pr",
    "edit",
    pullRequestNumber,
    "--add-reviewer",
    COPILOT_REVIEWER,
  );

  process.stdout.write(`PR created: ${pullRequestUrl}\n`);
  process.stdout.write(`PR number: ${pullRequestNumber}\n`);

  return {
    number: pullRequestNumber,
    url: pullRequestUrl,
  };
}

async function main(): Promise<void> {
  const args = parseCliArgs(process.argv.slice(2));

  const prBranchName = `${args.branch}-pr`;
  const worktreePath = path.join(args.repo, "worktrees", args.branch);

  cleanupOldState(args.repo, args.branch, prBranchName, worktreePath);

  try {
    runGitSync(
      args.repo,
      "worktree",
      "add",
      "-b",
      args.branch,
      worktreePath,
      args.baseCommit,
    );

    if (args.github) {
      await setupDotGitHubFolder(args.github, worktreePath);
    }

    runGitSync(args.repo, "push", "origin", args.branch);

    runGitSync(worktreePath, "checkout", "-b", prBranchName, args.prCommit);
    runGitSync(args.repo, "push", "origin", prBranchName);

    const pullRequest = createPullRequest(
      worktreePath,
      args.branch,
      prBranchName,
    );

    if (args.wait) {
      process.stdout.write(`Tracking Copilot review on: ${pullRequest.url}\n`);
      await waitForCopilotReview(
        pullRequest.number,
        worktreePath,
        args.waitTimeoutSec,
        args.waitIntervalSec,
      );
    }
  } finally {
    if (!args.keepWorktree) {
      cleanupEphemeralWorkspace(
        args.repo,
        args.branch,
        prBranchName,
        worktreePath,
      );
    }
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((err: unknown) => {
    const message = err instanceof Error ? err.message : String(err);
    log.error(message);
    process.stderr.write(`\n${usage()}\n`);
    process.exit(1);
  });
}
