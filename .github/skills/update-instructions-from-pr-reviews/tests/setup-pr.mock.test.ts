import { afterEach, describe, it, vi } from "vitest";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { rmSync } from "node:fs";

import {
  cleanupEphemeralWorkspace,
  cleanupOldState,
  waitForCopilotReview,
  waitForInlineComments,
} from "../scripts/setup-pr.ts";

vi.mock("node:child_process", async (importOriginal) => {
  const actual = await importOriginal<typeof import("node:child_process")>();
  return { ...actual, spawnSync: vi.fn() };
});

vi.mock("node:fs", async (importOriginal) => {
  const actual = await importOriginal<typeof import("node:fs")>();
  return { ...actual, rmSync: vi.fn() };
});

const mockSpawnSync = vi.mocked(spawnSync);
const mockRmSync = vi.mocked(rmSync);

function spawnResult(stdout: string, status: number) {
  return { status, stdout, stderr: "", signal: null, pid: 0, output: [] };
}

afterEach(() => {
  mockSpawnSync.mockReset();
  mockRmSync.mockReset();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("cleanup helpers", () => {
  it("cleanupOldState keeps going when steps fail", () => {
    const calls: string[] = [];
    vi.spyOn(process.stderr, "write").mockImplementation(() => true);

    mockSpawnSync.mockImplementation(((command: string, args: string[]) => {
      calls.push(`${command} ${args.join(" ")}`);
      return spawnResult("", 1);
    }) as unknown as typeof spawnSync);

    mockRmSync.mockImplementation(((targetPath: string) => {
      calls.push(`rm ${targetPath}`);
      throw new Error("rm failed");
    }) as unknown as typeof rmSync);

    cleanupOldState("/tmp/repo", "base", "base-pr", "/tmp/repo/worktrees/base");

    assert.deepEqual(calls, [
      "git worktree remove --force /tmp/repo/worktrees/base",
      "rm /tmp/repo/worktrees/base",
      "git worktree prune",
      "gh pr close base-pr",
      "git branch -D base",
      "git branch -D base-pr",
      "git push origin --delete base",
      "git push origin --delete base-pr",
    ]);
  });

  it("cleanupEphemeralWorkspace keeps going when steps fail", () => {
    const calls: string[] = [];

    mockSpawnSync.mockImplementation(((command: string, args: string[]) => {
      calls.push(`${command} ${args.join(" ")}`);
      return spawnResult("", 1);
    }) as unknown as typeof spawnSync);

    mockRmSync.mockImplementation(((targetPath: string) => {
      calls.push(`rm ${targetPath}`);
      throw new Error("rm failed");
    }) as unknown as typeof rmSync);

    cleanupEphemeralWorkspace(
      "/tmp/repo",
      "base",
      "base-pr",
      "/tmp/repo/worktrees/base",
    );

    assert.deepEqual(calls, [
      "git worktree remove --force /tmp/repo/worktrees/base",
      "rm /tmp/repo/worktrees/base",
      "git worktree prune",
      "git branch -D base",
      "git branch -D base-pr",
    ]);
  });
});

describe("copilot wait helpers", () => {
  it("stabilizes inline comments after repeated equal counts", async () => {
    vi.useFakeTimers();
    const counts = ["1", "2", "2", "2", "2"];
    let apiCalls = 0;
    const writes: string[] = [];

    vi.spyOn(process.stdout, "write").mockImplementation(
      (chunk: string | Uint8Array) => {
        writes.push(String(chunk));
        return true;
      },
    );

    mockSpawnSync.mockImplementation(((_command: string, args: string[]) => {
      if (args[0] === "repo") return spawnResult("owner/repo\n", 0);
      apiCalls++;
      return spawnResult(`${counts.shift() ?? "2"}\n`, 0);
    }) as unknown as typeof spawnSync);

    const pending = waitForInlineComments("42", "/tmp/repo", 1, 0);
    await vi.advanceTimersByTimeAsync(60_000);
    await pending;

    assert.equal(apiCalls, 5);
    assert.ok(
      writes.some((line) =>
        line.includes("Inline comment count stabilized at 2."),
      ),
    );
  });

  it("times out waiting for review summary", async () => {
    vi.useFakeTimers();

    vi.spyOn(process.stdout, "write").mockImplementation(() => true);
    mockSpawnSync.mockImplementation(
      () => spawnResult("false\n", 0),
    );

    const pending = waitForCopilotReview("42", "/tmp/repo", 3, 1);
    const rejection = assert.rejects(
      () => pending,
      /timed out waiting for copilot review on PR #42/,
    );
    await vi.advanceTimersByTimeAsync(5_000);
    await rejection;
  });
});
