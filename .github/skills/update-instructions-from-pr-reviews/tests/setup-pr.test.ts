import { describe, it } from "vitest";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";

import {
    extractCreatedPullRequest,
    parseCliArgs,
    setupDotGitHubFolder,
} from "../scripts/setup-pr.ts";

function git(cwd: string, ...args: string[]): string {
    const result = spawnSync("git", args, {
        cwd,
        encoding: "utf8",
        stdio: ["ignore", "pipe", "pipe"],
    });
    if (result.error) throw result.error;
    if (result.status !== 0) {
        throw new Error(`git ${args.join(" ")} failed: ${result.stderr}`);
    }
    return result.stdout;
}

describe("setup-pr helpers", () => {
    it("parses required args and defaults", () => {
        const args = parseCliArgs([
            "--base-commit",
            "aaa111",
            "--pr-commit",
            "bbb222",
            "--repo",
            "/tmp/test-repo",
            "--branch",
            "step-test",
        ]);

        assert.equal(args.baseCommit, "aaa111");
        assert.equal(args.prCommit, "bbb222");
        assert.equal(args.repo, "/tmp/test-repo");
        assert.equal(args.branch, "step-test");
        assert.equal(args.wait, true);
        assert.equal(args.keepWorktree, false);
        assert.equal(args.waitTimeoutSec, 900);
        assert.equal(args.waitIntervalSec, 20);
    });

    it("parses optional flags", () => {
        const args = parseCliArgs([
            "--base-commit",
            "aaa111",
            "--pr-commit",
            "bbb222",
            "--repo",
            "/tmp/test-repo",
            "--branch",
            "step-test",
            "--github",
            "/tmp/candidate/.github",
            "--keep-worktree",
            "--wait-timeout-sec",
            "30",
            "--wait-interval-sec",
            "5",
        ]);

        assert.equal(args.github, "/tmp/candidate/.github");
        assert.equal(args.keepWorktree, true);
        assert.equal(args.waitTimeoutSec, 30);
        assert.equal(args.waitIntervalSec, 5);
    });

    it("throws when required args are missing", () => {
        assert.throws(
            () => parseCliArgs(["--base-commit", "aaa111"]),
            /--base-commit, --pr-commit, --repo, and --branch are required/,
        );
    });

    it("throws on invalid positive-integer wait options", () => {
        assert.throws(
            () =>
                parseCliArgs([
                    "--base-commit",
                    "aaa111",
                    "--pr-commit",
                    "bbb222",
                    "--repo",
                    "/tmp/test-repo",
                    "--branch",
                    "step-test",
                    "--wait-timeout-sec",
                    "0",
                ]),
            /--wait-timeout-sec must be a positive integer/,
        );
    });

    it("extracts PR details from gh pr create output", () => {
        assert.deepEqual(
            extractCreatedPullRequest(
                "https://github.com/owner/repo/pull/789\n",
            ),
            {
                number: "789",
                url: "https://github.com/owner/repo/pull/789",
            },
        );
    });

    it("throws when gh output does not contain a pull request URL", () => {
        assert.throws(
            () => extractCreatedPullRequest("not a PR"),
            /failed to get a PR from output/,
        );
    });

    it("skips commit when copied .github contents are unchanged", async () => {
        const tempRoot = fs.mkdtempSync(
            path.join(os.tmpdir(), "setup-pr-test-"),
        );
        const worktreePath = path.join(tempRoot, "worktree");
        const sourceDotGithub = path.join(tempRoot, "source-github");

        fs.mkdirSync(path.join(worktreePath, ".github"), { recursive: true });
        fs.mkdirSync(sourceDotGithub, { recursive: true });

        const fileName = "copilot-instructions.md";
        const content = "# same content\n";
        fs.writeFileSync(path.join(worktreePath, ".github", fileName), content);
        fs.writeFileSync(path.join(sourceDotGithub, fileName), content);

        git(worktreePath, "init");
        git(worktreePath, "config", "user.email", "test@example.com");
        git(worktreePath, "config", "user.name", "Test User");
        git(worktreePath, "add", ".");
        git(worktreePath, "commit", "-m", "initial");

        await setupDotGitHubFolder(sourceDotGithub, worktreePath);

        const status = git(worktreePath, "status", "--porcelain").trim();
        assert.equal(status, "");
    });
});
