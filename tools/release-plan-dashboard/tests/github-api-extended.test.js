import { describe, test, expect, beforeAll, afterAll } from "vitest";

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

import * as githubApi from "../lib/github-api.js";

describe("github-api additional tests", () => {
  describe("no-token guard tests", () => {
    const originalPat = process.env.GITHUB_PAT_RELEASE_PLAN;
    const originalGhToken = process.env.GH_TOKEN;

    beforeAll(() => {
      delete process.env.GITHUB_PAT_RELEASE_PLAN;
      delete process.env.GH_TOKEN;
    });

    afterAll(() => {
      if (originalPat) process.env.GITHUB_PAT_RELEASE_PLAN = originalPat;
      if (originalGhToken) process.env.GH_TOKEN = originalGhToken;
    });

    test("batchFetchPrStatuses returns empty map when no token", async () => {
      const result = await githubApi.batchFetchPrStatuses([
        "https://github.com/org/repo/pull/1",
      ]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });

    test("batchFetchPrDetails returns empty map when no token", async () => {
      const result = await githubApi.batchFetchPrDetails([
        "https://github.com/org/repo/pull/1",
      ]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });

    test("batchFetchSpecProjectPaths returns empty map when no token", async () => {
      const result = await githubApi.batchFetchSpecProjectPaths([
        "https://github.com/org/repo/pull/1",
      ]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });
  });

  describe("batchFetchPrStatuses input validation", () => {
    test("handles empty array", async () => {
      const result = await githubApi.batchFetchPrStatuses([]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });
  });

  describe("getGitHubPrStatus with invalid URL", () => {
    test("returns null for invalid PR URL", async () => {
      const result = await githubApi.getGitHubPrStatus("not-a-url");
      expect(result).toBeNull();
    });

    test("returns null for null input", async () => {
      const result = await githubApi.getGitHubPrStatus(null);
      expect(result).toBeNull();
    });
  });

  describe("getGitHubPrDetails with invalid URL", () => {
    test("returns null for invalid PR URL", async () => {
      const result = await githubApi.getGitHubPrDetails("not-a-url");
      expect(result).toBeNull();
    });
  });

  describe("_extractPrStatus", () => {
    test("returns null for null data", () => {
      expect(githubApi._extractPrStatus(null)).toBeNull();
    });

    test("returns merged when merged_at is set", () => {
      expect(
        githubApi._extractPrStatus({
          merged_at: "2024-01-01",
          state: "closed",
          draft: true,
        }),
      ).toBe("merged");
    });

    test("returns merged when merged is true", () => {
      expect(
        githubApi._extractPrStatus({ merged: true, state: "closed" }),
      ).toBe("merged");
    });

    test("returns closed when state is closed and PR was draft", () => {
      // This is the key bug fix: a draft PR that was closed should show "closed", not "draft"
      expect(githubApi._extractPrStatus({ state: "closed", draft: true })).toBe(
        "closed",
      );
    });

    test("returns closed when state is closed and not draft", () => {
      expect(
        githubApi._extractPrStatus({ state: "closed", draft: false }),
      ).toBe("closed");
    });

    test("returns draft when state is open and draft is true", () => {
      expect(githubApi._extractPrStatus({ state: "open", draft: true })).toBe(
        "draft",
      );
    });

    test("returns open when state is open and not draft", () => {
      expect(githubApi._extractPrStatus({ state: "open", draft: false })).toBe(
        "open",
      );
    });

    test("returns unknown when no state field", () => {
      expect(githubApi._extractPrStatus({})).toBe("unknown");
    });
  });

  describe("extractFailedChecks", () => {
    test("filters out passing conclusions and returns failed check names", () => {
      const checkRuns = [
        { status: "completed", conclusion: "success", name: "build" },
        { status: "completed", conclusion: "failure", name: "lint" },
        { status: "completed", conclusion: "cancelled", name: "deploy" },
        { status: "completed", conclusion: "skipped", name: "optional" },
        { status: "completed", conclusion: "neutral", name: "info" },
        { status: "completed", conclusion: "timed_out", name: "slow-test" },
      ];
      const result = githubApi.extractFailedChecks(checkRuns);
      expect(result).toEqual(["lint", "slow-test"]);
    });

    test("excludes incomplete check runs", () => {
      const checkRuns = [
        { status: "in_progress", conclusion: null, name: "running" },
        { status: "queued", conclusion: null, name: "waiting" },
        { status: "completed", conclusion: "failure", name: "done-fail" },
      ];
      expect(githubApi.extractFailedChecks(checkRuns)).toEqual(["done-fail"]);
    });

    test("excludes completed runs with no conclusion", () => {
      const checkRuns = [
        { status: "completed", conclusion: null, name: "no-conclusion" },
      ];
      expect(githubApi.extractFailedChecks(checkRuns)).toEqual([]);
    });

    test("returns empty array for empty input", () => {
      expect(githubApi.extractFailedChecks([])).toEqual([]);
    });

    test("returns empty array when all checks pass", () => {
      const checkRuns = [
        { status: "completed", conclusion: "success", name: "a" },
        { status: "completed", conclusion: "neutral", name: "b" },
      ];
      expect(githubApi.extractFailedChecks(checkRuns)).toEqual([]);
    });
  });

  describe("extractApprovers", () => {
    test("extracts unique approver logins from APPROVED reviews", () => {
      const reviews = [
        { state: "APPROVED", user: { login: "alice" } },
        { state: "APPROVED", user: { login: "bob" } },
        { state: "APPROVED", user: { login: "alice" } },
      ];
      const result = githubApi.extractApprovers(reviews);
      expect(result).toEqual(["alice", "bob"]);
    });

    test("ignores non-APPROVED reviews", () => {
      const reviews = [
        { state: "CHANGES_REQUESTED", user: { login: "alice" } },
        { state: "COMMENTED", user: { login: "bob" } },
        { state: "APPROVED", user: { login: "carol" } },
        { state: "DISMISSED", user: { login: "dave" } },
      ];
      expect(githubApi.extractApprovers(reviews)).toEqual(["carol"]);
    });

    test("skips reviews without user", () => {
      const reviews = [
        { state: "APPROVED", user: null },
        { state: "APPROVED" },
        { state: "APPROVED", user: { login: "alice" } },
      ];
      expect(githubApi.extractApprovers(reviews)).toEqual(["alice"]);
    });

    test("returns empty array for empty input", () => {
      expect(githubApi.extractApprovers([])).toEqual([]);
    });

    test("returns empty array for non-array input", () => {
      expect(githubApi.extractApprovers(null)).toEqual([]);
      expect(githubApi.extractApprovers(undefined)).toEqual([]);
      expect(githubApi.extractApprovers("not-array")).toEqual([]);
    });
  });

  describe("extractCommentData", () => {
    test("finds latest non-bot comment", () => {
      const comments = [
        {
          user: { login: "alice", type: "User" },
          body: "first comment",
          created_at: "2024-01-01",
        },
        {
          user: { login: "bob", type: "User" },
          body: "second comment",
          created_at: "2024-01-02",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment).toEqual({
        author: "bob",
        body: "second comment",
        createdAt: "2024-01-02",
      });
    });

    test("skips bot comments by [bot] in login", () => {
      const comments = [
        {
          user: { login: "human", type: "User" },
          body: "human says hi",
          created_at: "2024-01-01",
        },
        {
          user: { login: "azure-sdk[bot]", type: "Bot" },
          body: "bot says hi",
          created_at: "2024-01-02",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment.author).toBe("human");
    });

    test("skips bot comments by type Bot", () => {
      const comments = [
        {
          user: { login: "real-user", type: "User" },
          body: "hello",
          created_at: "2024-01-01",
        },
        {
          user: { login: "myapp", type: "Bot" },
          body: "automated",
          created_at: "2024-01-02",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment.author).toBe("real-user");
    });

    test("skips bot comments by 'bot' substring in login", () => {
      const comments = [
        {
          user: { login: "dependabot", type: "User" },
          body: "bump deps",
          created_at: "2024-01-01",
        },
        {
          user: { login: "actual-human", type: "User" },
          body: "looks good",
          created_at: "2024-01-02",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      // "dependabot" contains "bot", so it's treated as bot; latest non-bot is "actual-human"
      expect(result.latestComment.author).toBe("actual-human");
    });

    test("finds APIView URL in comments with API Change Check text", () => {
      const comments = [
        {
          user: { login: "check-bot[bot]", type: "Bot" },
          body: "API Change Check found changes. See https://apiview.dev/review/abc123 for details.",
          created_at: "2024-01-01",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.apiViewUrl).toBe("https://apiview.dev/review/abc123");
    });

    test("finds APIView URL with spa subdomain", () => {
      const comments = [
        {
          user: { login: "check[bot]", type: "Bot" },
          body: "APIView link: https://spa.apiview.dev/Assemblies/Review/xyz",
          created_at: "2024-01-01",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.apiViewUrl).toBe(
        "https://spa.apiview.dev/Assemblies/Review/xyz",
      );
    });

    test("finds APIView URL with lowercase apiview keyword", () => {
      const comments = [
        {
          user: { login: "bot[bot]" },
          body: "Check apiview here: https://apiview.dev/r/42",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.apiViewUrl).toBe("https://apiview.dev/r/42");
    });

    test("returns null latestComment and empty apiViewUrl for empty input", () => {
      expect(githubApi.extractCommentData([])).toEqual({
        latestComment: null,
        apiViewUrl: "",
      });
    });

    test("returns defaults for null/undefined input", () => {
      expect(githubApi.extractCommentData(null)).toEqual({
        latestComment: null,
        apiViewUrl: "",
      });
      expect(githubApi.extractCommentData(undefined)).toEqual({
        latestComment: null,
        apiViewUrl: "",
      });
    });

    test("returns null latestComment when all comments are from bots", () => {
      const comments = [
        {
          user: { login: "bot1[bot]", type: "Bot" },
          body: "automated",
          created_at: "2024-01-01",
        },
        {
          user: { login: "bot2[bot]", type: "Bot" },
          body: "also automated",
          created_at: "2024-01-02",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment).toBeNull();
    });

    test("truncates comment body to 300 characters", () => {
      const longBody = "x".repeat(500);
      const comments = [
        {
          user: { login: "alice", type: "User" },
          body: longBody,
          created_at: "2024-01-01",
        },
      ];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment.body).toHaveLength(300);
    });

    test("handles comment with missing user login", () => {
      const comments = [
        { user: {}, body: "comment with no login", created_at: "2024-01-01" },
      ];
      const result = githubApi.extractCommentData(comments);
      // empty login does not contain "bot" or "[bot]", so it's not a bot
      expect(result.latestComment.author).toBe("");
    });

    test("handles comment with missing created_at", () => {
      const comments = [{ user: { login: "alice" }, body: "no date" }];
      const result = githubApi.extractCommentData(comments);
      expect(result.latestComment.createdAt).toBe("");
    });
  });

  describe("deriveSpecProjectPath", () => {
    test("returns directory of tspconfig.yaml when found", () => {
      const files = [
        "specification/foo/bar/tspconfig.yaml",
        "specification/foo/bar/main.tsp",
        "specification/foo/bar/models/model.tsp",
      ];
      expect(githubApi.deriveSpecProjectPath(files)).toBe(
        "specification/foo/bar",
      );
    });

    test("returns directory of main.tsp when no tspconfig.yaml", () => {
      const files = [
        "specification/foo/bar/main.tsp",
        "specification/foo/bar/models/model.tsp",
      ];
      expect(githubApi.deriveSpecProjectPath(files)).toBe(
        "specification/foo/bar",
      );
    });

    test("returns directory of client.tsp when no tspconfig or main.tsp", () => {
      const files = [
        "specification/foo/bar/client.tsp",
        "specification/foo/bar/other.tsp",
      ];
      expect(githubApi.deriveSpecProjectPath(files)).toBe(
        "specification/foo/bar",
      );
    });

    test("prefers tspconfig.yaml over main.tsp", () => {
      const files = ["a/b/main.tsp", "x/y/tspconfig.yaml"];
      expect(githubApi.deriveSpecProjectPath(files)).toBe("x/y");
    });

    test("falls back to common directory prefix when no marker files", () => {
      const files = [
        "specification/foo/bar/file1.json",
        "specification/foo/bar/file2.json",
        "specification/foo/baz/file3.json",
      ];
      expect(githubApi.deriveSpecProjectPath(files)).toBe("specification/foo");
    });

    test("returns empty string for empty input", () => {
      expect(githubApi.deriveSpecProjectPath([])).toBe("");
      expect(githubApi.deriveSpecProjectPath(null)).toBe("");
      expect(githubApi.deriveSpecProjectPath(undefined)).toBe("");
    });

    test("returns empty string for single file with no directory", () => {
      expect(githubApi.deriveSpecProjectPath(["readme.md"])).toBe("");
    });

    test("handles marker file at root level (no directory)", () => {
      expect(githubApi.deriveSpecProjectPath(["tspconfig.yaml"])).toBe("");
    });

    test("common prefix reduction handles divergent paths", () => {
      const files = [
        "a/b/c/file1.json",
        "a/b/d/file2.json",
        "x/y/z/file3.json",
      ];
      // common prefix between a/b and x/y → empty
      expect(githubApi.deriveSpecProjectPath(files)).toBe("");
    });

    test("common prefix with all files in same directory", () => {
      const files = [
        "spec/service/v1/file1.json",
        "spec/service/v1/file2.json",
      ];
      expect(githubApi.deriveSpecProjectPath(files)).toBe("spec/service/v1");
    });
  });

  describe("getOctokit", () => {
    const originalPat = process.env.GITHUB_PAT_RELEASE_PLAN;
    const originalGhToken = process.env.GH_TOKEN;

    afterAll(() => {
      if (originalPat) process.env.GITHUB_PAT_RELEASE_PLAN = originalPat;
      else delete process.env.GITHUB_PAT_RELEASE_PLAN;
      if (originalGhToken) process.env.GH_TOKEN = originalGhToken;
      else delete process.env.GH_TOKEN;
    });

    test("returns Octokit instance with explicit token", () => {
      const octokit = githubApi.getOctokit("my-explicit-token");
      expect(octokit).not.toBeNull();
      expect(octokit).toBeDefined();
    });

    test("returns Octokit instance with GITHUB_PAT_RELEASE_PLAN env var", () => {
      delete process.env.GH_TOKEN;
      process.env.GITHUB_PAT_RELEASE_PLAN = "pat-token";
      const octokit = githubApi.getOctokit();
      expect(octokit).not.toBeNull();
      delete process.env.GITHUB_PAT_RELEASE_PLAN;
    });

    test("returns Octokit instance with GH_TOKEN env var", () => {
      delete process.env.GITHUB_PAT_RELEASE_PLAN;
      process.env.GH_TOKEN = "gh-token";
      const octokit = githubApi.getOctokit();
      expect(octokit).not.toBeNull();
      process.env.GH_TOKEN = originalGhToken;
    });

    test("returns null when no token available", () => {
      const savedPat = process.env.GITHUB_PAT_RELEASE_PLAN;
      const savedGh = process.env.GH_TOKEN;
      delete process.env.GITHUB_PAT_RELEASE_PLAN;
      delete process.env.GH_TOKEN;
      const octokit = githubApi.getOctokit();
      expect(octokit).toBeNull();
      if (savedPat) process.env.GITHUB_PAT_RELEASE_PLAN = savedPat;
      if (savedGh) process.env.GH_TOKEN = savedGh;
    });
  });
});
