import { describe, it, vi } from "vitest";
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { spawn } from "node:child_process";

import {
    buildRounds,
    buildSetupPrArgs,
    extractPrUrlFromSetupOutput,
    runSetupPr,
} from "../scripts/replay-pull-request.ts";

vi.mock("node:child_process", async (importOriginal) => {
    const actual = await importOriginal<typeof import("node:child_process")>();
    return { ...actual, spawn: vi.fn() };
});

const mockSpawn = vi.mocked(spawn);

describe("replay-pull-request helpers", () => {
    it("groups only human feedback into review rounds", () => {
        const rounds = buildRounds(
            [
                { sha: "aaa111", commit: { message: "first commit\n\nbody" } },
                { sha: "bbb222", commit: { message: "second commit" } },
            ],
            [
                {
                    id: 1,
                    state: "COMMENTED",
                    body: "Looks good",
                    commit_id: "aaa111",
                    submitted_at: "2026-05-29T20:00:00Z",
                    user: { login: "alice", type: "User" },
                },
                {
                    id: 2,
                    state: "COMMENTED",
                    body: "bot noise",
                    commit_id: "bbb222",
                    submitted_at: "2026-05-29T20:01:00Z",
                    user: { login: "dependabot[bot]", type: "User" },
                },
            ],
            [
                {
                    id: 3,
                    path: "src/app.ts",
                    line: 10,
                    body: "Need a nil check",
                    original_commit_id: "bbb222",
                    created_at: "2026-05-29T20:02:00Z",
                    user: { login: "carol", type: "User" },
                },
            ],
        );

        assert.equal(rounds.length, 2);
        assert.equal(rounds[0]?.commitSha, "aaa111");
        assert.equal(rounds[0]?.humanReviewCount, 1);
        assert.equal(rounds[0]?.humanInlineCount, 0);
        assert.equal(rounds[1]?.commitSha, "bbb222");
        assert.equal(rounds[1]?.humanReviewCount, 0);
        assert.equal(rounds[1]?.humanInlineCount, 1);
        assert.deepEqual(rounds[1]?.inlineFiles, ["src/app.ts"]);
    });

    it("builds setup-pr arguments including wait settings and github override", () => {
        const built = buildSetupPrArgs(
            {
                repo: "owner/repo",
                numbers: [42],
                inputJsonStdin: false,
                github: "/tmp/candidate/.github",
                testRepo: "/tmp/test-repo",
                branch: "step-test",
                concurrency: 1,
                dryRun: false,
                waitTimeoutSec: 900,
                waitIntervalSec: 20,
            },
            {
                index: 2,
                commitSha: "abcdef1234567890",
                commitMessage: "message",
                humanReviewCount: 1,
                humanInlineCount: 1,
                reviews: [],
                inlineFiles: [],
            },
            "base1234567890",
        );

        assert.match(built.scriptPath, /setup-pr\.ts$/);
        assert.ok(built.args.includes("--wait"));
        assert.ok(built.args.includes("--github"));
        assert.ok(built.args.includes("/tmp/candidate/.github"));
        assert.equal(built.branchName, "step-test-r2");
    });

    it("extracts the PR URL from setup-pr output", () => {
        assert.equal(
            extractPrUrlFromSetupOutput(
                "other\nPR created: https://github.com/owner/repo/pull/123\nmore\n",
            ),
            "https://github.com/owner/repo/pull/123",
        );
        assert.equal(extractPrUrlFromSetupOutput("no pr here"), "");
    });

    it("runs setup-pr through a mocked child process and returns the created URL", async () => {
        const stdoutWrites: string[] = [];
        const stderrWrites: string[] = [];
        const outSpy = vi
            .spyOn(process.stdout, "write")
            .mockImplementation((chunk: string | Uint8Array) => {
                stdoutWrites.push(String(chunk));
                return true;
            });
        const errSpy = vi
            .spyOn(process.stderr, "write")
            .mockImplementation((chunk: string | Uint8Array) => {
                stderrWrites.push(String(chunk));
                return true;
            });

        const calls: {
            command: string;
            args: string[];
            cwd: string | undefined;
        }[] = [];

        mockSpawn.mockImplementation(((
            command: string,
            args: string[],
            options: { cwd?: string },
        ) => {
            calls.push({ command, args, cwd: options.cwd });
            const child = new EventEmitter() as EventEmitter & {
                stdout: EventEmitter;
                stderr: EventEmitter;
            };
            child.stdout = new EventEmitter();
            child.stderr = new EventEmitter();
            queueMicrotask(() => {
                child.stdout.emit(
                    "data",
                    "PR created: https://github.com/owner/repo/pull/456\n",
                );
                child.stdout.emit("data", "Copilot review found.\n");
                child.emit("close", 0);
            });
            return child;
        }) as unknown as typeof spawn);

        try {
            const prUrl = await runSetupPr(
                {
                    repo: "owner/repo",
                    numbers: [42],
                    inputJsonStdin: false,
                    github: "/tmp/candidate/.github",
                    testRepo: "/tmp/test-repo",
                    branch: "step-test",
                    concurrency: 1,
                    dryRun: false,
                    waitTimeoutSec: 900,
                    waitIntervalSec: 20,
                },
                {
                    index: 1,
                    commitSha: "abcdef1234567890",
                    commitMessage: "message",
                    humanReviewCount: 1,
                    humanInlineCount: 0,
                    reviews: [],
                    inlineFiles: [],
                },
                "base1234567890",
            );

            assert.equal(prUrl, "https://github.com/owner/repo/pull/456");
            assert.equal(calls.length, 1);
            assert.equal(calls[0]?.command, "node");
            assert.equal(calls[0]?.cwd, "/tmp/test-repo");
            assert.ok(
                stdoutWrites.some((line) =>
                    line.includes(
                        "test PR: https://github.com/owner/repo/pull/456",
                    ),
                ),
            );
            assert.ok(
                stdoutWrites.some((line) =>
                    line.includes("Copilot review received"),
                ),
            );
            assert.equal(stderrWrites.length, 0);
        } finally {
            outSpy.mockRestore();
            errSpy.mockRestore();
            mockSpawn.mockReset();
        }
    });
});
