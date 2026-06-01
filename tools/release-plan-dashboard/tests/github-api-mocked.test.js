import { describe, test, expect, vi, beforeEach, afterAll } from "vitest";

// Set required env vars before import
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";
process.env.GH_TOKEN = "mock-token";

const mockOctokit = {
  pulls: {
    get: vi.fn(),
    listReviews: vi.fn(),
    listFiles: vi.fn(),
  },
  checks: {
    listForRef: vi.fn(),
  },
  issues: {
    listComments: vi.fn(),
    listLabelsOnIssue: vi.fn(),
  },
  paginate: vi.fn(),
};

vi.mock("@octokit/rest", () => ({
  Octokit: vi.fn().mockImplementation(() => mockOctokit),
}));

const {
  getGitHubPrStatus,
  getGitHubPrDetails,
  getGitHubPrFiles,
  getGitHubPrLabels,
  batchFetchPrStatuses,
  batchFetchPrDetails,
  batchFetchSpecProjectPaths,
  batchFetchSpecPrLabels,
} = await import("../lib/github-api.js");

const VALID_PR_URL = "https://github.com/Azure/azure-sdk-for-net/pull/100";

afterAll(() => {
  delete process.env.GH_TOKEN;
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe("getGitHubPrStatus (mocked Octokit)", () => {
  test("returns 'merged' for a merged PR", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: {
        merged_at: "2024-06-01T00:00:00Z",
        merged: true,
        state: "closed",
      },
    });
    const result = await getGitHubPrStatus(VALID_PR_URL);
    expect(result).toBe("merged");
    expect(mockOctokit.pulls.get).toHaveBeenCalledWith({
      owner: "Azure",
      repo: "azure-sdk-for-net",
      pull_number: 100,
    });
  });

  test("returns 'open' for an open PR", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: { state: "open", draft: false },
    });
    const result = await getGitHubPrStatus(VALID_PR_URL);
    expect(result).toBe("open");
  });

  test("returns 'draft' for a draft PR", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: { state: "open", draft: true },
    });
    const result = await getGitHubPrStatus(VALID_PR_URL);
    expect(result).toBe("draft");
  });

  test("returns 'closed' for a closed non-merged PR", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: { state: "closed", draft: false },
    });
    const result = await getGitHubPrStatus(VALID_PR_URL);
    expect(result).toBe("closed");
  });

  test("returns null on API error", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.pulls.get.mockRejectedValue(new Error("API rate limit"));
    const result = await getGitHubPrStatus(VALID_PR_URL);
    expect(result).toBeNull();
    expect(warnSpy).toHaveBeenCalled();
    warnSpy.mockRestore();
  });
});

describe("getGitHubPrDetails (mocked Octokit)", () => {
  test("returns full details for a PR with reviews, checks, and comments", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: {
        mergeable: true,
        mergeable_state: "clean",
        title: "Add feature",
        updated_at: "2024-06-01",
        draft: false,
        state: "open",
        head: { sha: "abc123" },
        requested_reviewers: [{ login: "reviewer1" }],
      },
    });
    mockOctokit.pulls.listReviews.mockResolvedValue({
      data: [
        { state: "APPROVED", user: { login: "alice" } },
        { state: "COMMENTED", user: { login: "bob" } },
      ],
    });
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: {
        check_runs: [
          { status: "completed", conclusion: "success", name: "build" },
          { status: "completed", conclusion: "failure", name: "lint" },
        ],
      },
    });
    mockOctokit.issues.listComments.mockResolvedValue({
      data: [
        {
          user: { login: "check[bot]", type: "Bot" },
          body: "API Change Check: https://apiview.dev/review/r1",
          created_at: "2024-01-01",
        },
        {
          user: { login: "human", type: "User" },
          body: "LGTM",
          created_at: "2024-01-02",
        },
      ],
    });

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).not.toBeNull();
    expect(result.mergeable).toBe(true);
    expect(result.mergeableState).toBe("clean");
    expect(result.title).toBe("Add feature");
    expect(result.isApproved).toBe(true);
    expect(result.approvedBy).toEqual(["alice"]);
    expect(result.failedChecks).toEqual(["lint"]);
    expect(result.apiViewUrl).toBe("https://apiview.dev/review/r1");
    expect(result.latestComment.author).toBe("human");
    expect(result.requestedReviewers).toEqual(["reviewer1"]);
  });

  test("handles PR with no head sha (skips checks)", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: {
        mergeable: false,
        mergeable_state: "unknown",
        title: "WIP",
        updated_at: "2024-06-01",
        head: null,
        requested_reviewers: [],
      },
    });
    mockOctokit.pulls.listReviews.mockResolvedValue({ data: [] });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).not.toBeNull();
    expect(result.failedChecks).toEqual([]);
    expect(mockOctokit.checks.listForRef).not.toHaveBeenCalled();
  });

  test("handles checks API error gracefully", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: {
        mergeable: true,
        mergeable_state: "clean",
        title: "PR",
        updated_at: "2024-06-01",
        head: { sha: "abc123" },
      },
    });
    mockOctokit.pulls.listReviews.mockResolvedValue({ data: [] });
    mockOctokit.checks.listForRef.mockRejectedValue(
      new Error("checks unavailable"),
    );
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).not.toBeNull();
    expect(result.failedChecks).toEqual([]);
  });

  test("handles comments API error gracefully", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.pulls.get.mockResolvedValue({
      data: {
        mergeable: true,
        mergeable_state: "clean",
        title: "PR",
        updated_at: "2024-06-01",
        head: { sha: "def456" },
      },
    });
    mockOctokit.pulls.listReviews.mockResolvedValue({ data: [] });
    mockOctokit.checks.listForRef.mockResolvedValue({
      data: { check_runs: [] },
    });
    mockOctokit.issues.listComments.mockRejectedValue(
      new Error("comment error"),
    );

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).not.toBeNull();
    expect(result.latestComment).toBeNull();
    expect(result.apiViewUrl).toBe("");
    warnSpy.mockRestore();
  });

  test("returns null when prData is null", async () => {
    mockOctokit.pulls.get.mockResolvedValue({ data: null });
    mockOctokit.pulls.listReviews.mockResolvedValue({ data: [] });

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).toBeNull();
  });

  test("returns null on API error", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.pulls.get.mockRejectedValue(new Error("network error"));

    const result = await getGitHubPrDetails(VALID_PR_URL);
    expect(result).toBeNull();
    warnSpy.mockRestore();
  });
});

describe("getGitHubPrFiles (mocked Octokit)", () => {
  test("returns file list from paginate", async () => {
    mockOctokit.paginate.mockResolvedValue([
      "src/file1.ts",
      "src/file2.ts",
      "tspconfig.yaml",
    ]);

    const result = await getGitHubPrFiles(VALID_PR_URL);
    expect(result).toEqual(["src/file1.ts", "src/file2.ts", "tspconfig.yaml"]);
    expect(mockOctokit.paginate).toHaveBeenCalled();
  });

  test("returns empty array on API error", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.paginate.mockRejectedValue(new Error("paginate failed"));

    const result = await getGitHubPrFiles(VALID_PR_URL);
    expect(result).toEqual([]);
    warnSpy.mockRestore();
  });

  test("returns empty array for invalid URL", async () => {
    const result = await getGitHubPrFiles("not-a-url");
    expect(result).toEqual([]);
  });
});

describe("getGitHubPrLabels (mocked Octokit)", () => {
  test("returns mapped labels", async () => {
    mockOctokit.issues.listLabelsOnIssue.mockResolvedValue({
      data: [
        { name: "BreakingChange", color: "d73a4a" },
        { name: "bug", color: "fc0303" },
        { name: "ARM", color: "" },
      ],
    });

    const result = await getGitHubPrLabels(VALID_PR_URL);
    expect(result).toEqual([
      { name: "BreakingChange", color: "d73a4a" },
      { name: "bug", color: "fc0303" },
      { name: "ARM", color: "" },
    ]);
  });

  test("returns empty array on API error", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.issues.listLabelsOnIssue.mockRejectedValue(
      new Error("labels error"),
    );

    const result = await getGitHubPrLabels(VALID_PR_URL);
    expect(result).toEqual([]);
    warnSpy.mockRestore();
  });

  test("returns empty array for invalid URL", async () => {
    const result = await getGitHubPrLabels("bad-url");
    expect(result).toEqual([]);
  });

  test("handles null data gracefully", async () => {
    mockOctokit.issues.listLabelsOnIssue.mockResolvedValue({ data: null });
    const result = await getGitHubPrLabels(VALID_PR_URL);
    expect(result).toEqual([]);
  });

  test("handles label with missing color", async () => {
    mockOctokit.issues.listLabelsOnIssue.mockResolvedValue({
      data: [{ name: "no-color" }],
    });
    const result = await getGitHubPrLabels(VALID_PR_URL);
    expect(result).toEqual([{ name: "no-color", color: "" }]);
  });
});

describe("batchFetchPrStatuses (mocked Octokit)", () => {
  test("fetches statuses for multiple URLs and deduplicates", async () => {
    mockOctokit.pulls.get
      .mockResolvedValueOnce({ data: { state: "open", draft: false } })
      .mockResolvedValueOnce({
        data: { merged_at: "2024-01-01", state: "closed" },
      });

    const urls = [
      "https://github.com/org/repo/pull/1",
      "https://github.com/org/repo/pull/2",
      "https://github.com/org/repo/pull/1", // duplicate
    ];
    const result = await batchFetchPrStatuses(urls);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(2);
    expect(result.get("https://github.com/org/repo/pull/1")).toBe("open");
    expect(result.get("https://github.com/org/repo/pull/2")).toBe("merged");
  });

  test("handles individual errors by setting null", async () => {
    mockOctokit.pulls.get
      .mockResolvedValueOnce({ data: { state: "open", draft: false } })
      .mockRejectedValueOnce(new Error("fail"));

    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    const urls = [
      "https://github.com/org/repo/pull/1",
      "https://github.com/org/repo/pull/2",
    ];
    const result = await batchFetchPrStatuses(urls);
    expect(result.get("https://github.com/org/repo/pull/1")).toBe("open");
    expect(result.get("https://github.com/org/repo/pull/2")).toBeNull();
    warnSpy.mockRestore();
  });

  test("returns empty map for empty urls", async () => {
    const result = await batchFetchPrStatuses([]);
    expect(result.size).toBe(0);
  });

  test("filters out falsy URLs", async () => {
    mockOctokit.pulls.get.mockResolvedValue({
      data: { state: "open", draft: false },
    });
    const result = await batchFetchPrStatuses([
      null,
      "",
      "https://github.com/org/repo/pull/1",
    ]);
    expect(result.size).toBe(1);
  });
});

describe("batchFetchPrDetails (mocked Octokit)", () => {
  test("fetches details for multiple URLs", async () => {
    const prData = {
      mergeable: true,
      mergeable_state: "clean",
      title: "PR",
      updated_at: "2024-01-01",
      head: null,
      requested_reviewers: [],
    };
    mockOctokit.pulls.get.mockResolvedValue({ data: prData });
    mockOctokit.pulls.listReviews.mockResolvedValue({ data: [] });
    mockOctokit.issues.listComments.mockResolvedValue({ data: [] });

    const urls = [
      "https://github.com/org/repo/pull/1",
      "https://github.com/org/repo/pull/2",
    ];
    const result = await batchFetchPrDetails(urls);
    expect(result).toBeInstanceOf(Map);
    expect(result.size).toBe(2);
    expect(result.get(urls[0])).not.toBeNull();
    expect(result.get(urls[1])).not.toBeNull();
  });

  test("handles individual errors by setting null", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.pulls.get.mockRejectedValue(new Error("fail"));

    const result = await batchFetchPrDetails([
      "https://github.com/org/repo/pull/1",
    ]);
    expect(result.get("https://github.com/org/repo/pull/1")).toBeNull();
    warnSpy.mockRestore();
  });
});

describe("batchFetchSpecProjectPaths (mocked Octokit)", () => {
  test("fetches files and derives TypeSpec project paths", async () => {
    mockOctokit.paginate.mockResolvedValue([
      "specification/compute/tspconfig.yaml",
      "specification/compute/main.tsp",
    ]);

    const urls = ["https://github.com/Azure/azure-rest-api-specs/pull/10"];
    const result = await batchFetchSpecProjectPaths(urls);
    expect(result).toBeInstanceOf(Map);
    expect(result.get(urls[0])).toBe("specification/compute");
  });

  test("handles errors by setting empty string", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.paginate.mockRejectedValue(new Error("fail"));

    const result = await batchFetchSpecProjectPaths([
      "https://github.com/org/repo/pull/1",
    ]);
    // getGitHubPrFiles catches error and returns [], deriveSpecProjectPath([]) returns ""
    expect(result.get("https://github.com/org/repo/pull/1")).toBe("");
    warnSpy.mockRestore();
  });
});

describe("batchFetchSpecPrLabels (mocked Octokit)", () => {
  test("fetches labels and filters by SPEC_LABEL_PATTERNS", async () => {
    mockOctokit.issues.listLabelsOnIssue.mockResolvedValue({
      data: [
        { name: "BreakingChange", color: "d73a4a" },
        { name: "bug", color: "fc0303" },
        { name: "ARM review", color: "0075ca" },
        { name: "API", color: "e4e669" },
        { name: "enhancement", color: "a2eeef" },
      ],
    });

    const urls = ["https://github.com/Azure/azure-rest-api-specs/pull/5"];
    const result = await batchFetchSpecPrLabels(urls);
    expect(result).toBeInstanceOf(Map);
    const labels = result.get(urls[0]);
    expect(labels).toHaveLength(3);
    expect(labels.map((l) => l.name)).toEqual(
      expect.arrayContaining(["BreakingChange", "ARM review", "API"]),
    );
  });

  test("handles errors by setting empty array", async () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockOctokit.issues.listLabelsOnIssue.mockRejectedValue(new Error("fail"));

    const result = await batchFetchSpecPrLabels([
      "https://github.com/org/repo/pull/1",
    ]);
    expect(result.get("https://github.com/org/repo/pull/1")).toEqual([]);
    warnSpy.mockRestore();
  });

  test("returns empty map for empty urls", async () => {
    const result = await batchFetchSpecPrLabels([]);
    expect(result.size).toBe(0);
  });

  test("filters out non-matching labels", async () => {
    mockOctokit.issues.listLabelsOnIssue.mockResolvedValue({
      data: [
        { name: "bug", color: "fc0303" },
        { name: "enhancement", color: "a2eeef" },
      ],
    });

    const urls = ["https://github.com/org/repo/pull/1"];
    const result = await batchFetchSpecPrLabels(urls);
    expect(result.get(urls[0])).toEqual([]);
  });
});
