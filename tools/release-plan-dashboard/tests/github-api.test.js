import { describe, test, expect } from "vitest";

// Set required env vars before import
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";

import { parseGitHubPrUrl, throttledMap } from "../lib/github-api.js";

describe("github-api module", () => {
  describe("parseGitHubPrUrl", () => {
    test("parses valid GitHub PR URL", () => {
      const result = parseGitHubPrUrl("https://github.com/Azure/azure-sdk-for-net/pull/12345");
      expect(result).toEqual({ owner: "Azure", repo: "azure-sdk-for-net", number: "12345" });
    });

    test("parses URL with trailing content", () => {
      const result = parseGitHubPrUrl("https://github.com/Azure/azure-sdk-for-python/pull/99/files");
      expect(result).toEqual({ owner: "Azure", repo: "azure-sdk-for-python", number: "99" });
    });

    test("parses URL with www prefix (regex matches github.com substring)", () => {
      const result = parseGitHubPrUrl("https://www.github.com/org/repo/pull/1");
      // The regex /github\.com\/.../ matches because www.github.com contains github.com
      expect(result).toEqual({ owner: "org", repo: "repo", number: "1" });
    });

    test("returns null for non-PR GitHub URLs", () => {
      expect(parseGitHubPrUrl("https://github.com/Azure/azure-sdk-for-net")).toBeNull();
      expect(parseGitHubPrUrl("https://github.com/Azure/azure-sdk-for-net/issues/123")).toBeNull();
    });

    test("returns null for empty or null input", () => {
      expect(parseGitHubPrUrl("")).toBeNull();
      expect(parseGitHubPrUrl(null)).toBeNull();
      expect(parseGitHubPrUrl(undefined)).toBeNull();
    });

    test("returns null for non-GitHub URLs", () => {
      expect(parseGitHubPrUrl("https://dev.azure.com/org/project/pull/123")).toBeNull();
      expect(parseGitHubPrUrl("https://gitlab.com/org/repo/pull/123")).toBeNull();
    });

    test("handles complex repo names", () => {
      const result = parseGitHubPrUrl("https://github.com/Azure/azure-rest-api-specs/pull/7890");
      expect(result).toEqual({ owner: "Azure", repo: "azure-rest-api-specs", number: "7890" });
    });

    test("handles single digit PR number", () => {
      const result = parseGitHubPrUrl("https://github.com/org/repo/pull/1");
      expect(result).toEqual({ owner: "org", repo: "repo", number: "1" });
    });
  });

  describe("throttledMap", () => {
    test("processes all items with concurrency control", async () => {
      const items = [1, 2, 3, 4, 5];
      const results = await throttledMap(items, async (x) => x * 2, { concurrency: 2, delayMs: 0 });
      expect(results).toEqual([2, 4, 6, 8, 10]);
    });

    test("handles empty array", async () => {
      const results = await throttledMap([], async (x) => x, { concurrency: 5, delayMs: 0 });
      expect(results).toEqual([]);
    });

    test("respects concurrency limit", async () => {
      let maxConcurrent = 0;
      let current = 0;

      const items = [1, 2, 3, 4, 5, 6];
      await throttledMap(items, async (x) => {
        current++;
        maxConcurrent = Math.max(maxConcurrent, current);
        await new Promise(r => setTimeout(r, 10));
        current--;
        return x;
      }, { concurrency: 3, delayMs: 0 });

      expect(maxConcurrent).toBeLessThanOrEqual(3);
    });

    test("preserves order of results", async () => {
      const items = [50, 10, 30, 20, 40];
      const results = await throttledMap(items, async (x) => {
        await new Promise(r => setTimeout(r, Math.random() * 10));
        return x;
      }, { concurrency: 3, delayMs: 0 });
      expect(results).toEqual([50, 10, 30, 20, 40]);
    });

    test("handles errors in individual items", async () => {
      const items = [1, 2, 3];
      await expect(
        throttledMap(items, async (x) => {
          if (x === 2) throw new Error("fail");
          return x;
        }, { concurrency: 5, delayMs: 0 })
      ).rejects.toThrow("fail");
    });
  });
});
