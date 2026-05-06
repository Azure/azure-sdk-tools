import { describe, test, expect, beforeAll, afterAll } from "vitest";

// Tests for the deriveSpecProjectPath function
// This is an important utility that extracts TypeSpec paths from PR file lists

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

// We need to extract deriveSpecProjectPath — it's not exported, so we test via module internals
// Actually let's check what's exported from github-api
import * as githubApi from "../lib/github-api.js";

// deriveSpecProjectPath is not exported, but it's used by batchFetchSpecProjectPaths.
// Let's test it indirectly by importing the source and testing the logic.
// For now, we test the exported functions and their edge cases.

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
      const result = await githubApi.batchFetchPrStatuses(["https://github.com/org/repo/pull/1"]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });

    test("batchFetchPrDetails returns empty map when no token", async () => {
      const result = await githubApi.batchFetchPrDetails(["https://github.com/org/repo/pull/1"]);
      expect(result).toBeInstanceOf(Map);
      expect(result.size).toBe(0);
    });

    test("batchFetchSpecProjectPaths returns empty map when no token", async () => {
      const result = await githubApi.batchFetchSpecProjectPaths(["https://github.com/org/repo/pull/1"]);
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
      expect(githubApi._extractPrStatus({ merged_at: "2024-01-01", state: "closed", draft: true })).toBe("merged");
    });

    test("returns merged when merged is true", () => {
      expect(githubApi._extractPrStatus({ merged: true, state: "closed" })).toBe("merged");
    });

    test("returns closed when state is closed and PR was draft", () => {
      // This is the key bug fix: a draft PR that was closed should show "closed", not "draft"
      expect(githubApi._extractPrStatus({ state: "closed", draft: true })).toBe("closed");
    });

    test("returns closed when state is closed and not draft", () => {
      expect(githubApi._extractPrStatus({ state: "closed", draft: false })).toBe("closed");
    });

    test("returns draft when state is open and draft is true", () => {
      expect(githubApi._extractPrStatus({ state: "open", draft: true })).toBe("draft");
    });

    test("returns open when state is open and not draft", () => {
      expect(githubApi._extractPrStatus({ state: "open", draft: false })).toBe("open");
    });

    test("returns unknown when no state field", () => {
      expect(githubApi._extractPrStatus({})).toBe("unknown");
    });
  });
});
