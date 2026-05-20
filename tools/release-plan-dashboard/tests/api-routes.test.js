import {
  describe,
  test,
  expect,
  vi,
  beforeAll,
  afterAll,
  beforeEach,
} from "vitest";

// Tests for routes/api.js input validation and caching behavior
// These test the route handlers' input validation without making real API calls.

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";
process.env.DEVOPS_RELEASE_PLAN_PAT = "test-pat";
process.env.GH_TOKEN = "test-gh-token";

const mockRunWiql = vi.fn().mockResolvedValue([]);
const mockFetchWorkItemsBatch = vi.fn().mockResolvedValue([]);
const mockDevopsRequest = vi
  .fn()
  .mockImplementation((url, method, body, options) => {
    if (options && options.returnHeaders) {
      return Promise.resolve({ body: { value: [] }, headers: {} });
    }
    return Promise.resolve({ workItems: [], value: [] });
  });
const mockFetchPackageWorkItems = vi.fn().mockResolvedValue(new Map());
const mockFetchAzureSdkPackageList = vi.fn().mockResolvedValue("");
const mockBatchFetchPrStatuses = vi.fn().mockResolvedValue(new Map());
const mockBatchFetchPrDetails = vi.fn().mockResolvedValue(new Map());
const mockBatchFetchSpecProjectPaths = vi.fn().mockResolvedValue(new Map());
const mockBatchFetchSpecPrLabels = vi.fn().mockResolvedValue(new Map());

// Mock external API calls
vi.mock("../lib/devops-api.js", async () => {
  const original = await vi.importActual("../lib/devops-api.js");
  return {
    ...original,
    devopsRequest: (...args) => mockDevopsRequest(...args),
    runWiql: (...args) => mockRunWiql(...args),
    fetchWorkItemsBatch: (...args) => mockFetchWorkItemsBatch(...args),
    fetchPackageWorkItems: (...args) => mockFetchPackageWorkItems(...args),
    fetchAzureSdkPackageList: (...args) =>
      mockFetchAzureSdkPackageList(...args),
  };
});

vi.mock("../lib/github-api.js", () => ({
  parseGitHubPrUrl: (url) => {
    if (!url) return null;
    const m = url.match(/github\.com\/([^/]+)\/([^/]+)\/pull\/(\d+)/);
    return m ? { owner: m[1], repo: m[2], number: m[3] } : null;
  },
  batchFetchPrStatuses: (...args) => mockBatchFetchPrStatuses(...args),
  batchFetchPrDetails: (...args) => mockBatchFetchPrDetails(...args),
  batchFetchSpecProjectPaths: (...args) =>
    mockBatchFetchSpecProjectPaths(...args),
  batchFetchSpecPrLabels: (...args) => mockBatchFetchSpecPrLabels(...args),
}));

import express from "express";
import http from "node:http";
import { cache } from "../lib/cache.js";

let app, server, apiRoutes;

beforeAll(async () => {
  app = express();
  app.use(express.json());

  // Simulate authenticated user (in production, requireAuth sets req.user)
  app.use((req, res, next) => {
    req.user = {
      login: "testuser@microsoft.com",
      name: "Test User",
      objectId: "obj-123",
    };
    next();
  });

  const mod = await import("../routes/api.js");
  apiRoutes = mod.default;
  app.use(apiRoutes);

  await new Promise((resolve) => {
    server = app.listen(0, resolve);
  });
});

afterAll(async () => {
  await new Promise((resolve) => {
    server.close(resolve);
  });
});

beforeEach(() => {
  mockRunWiql.mockReset().mockResolvedValue([]);
  mockFetchWorkItemsBatch.mockReset().mockResolvedValue([]);
  mockDevopsRequest
    .mockReset()
    .mockImplementation((url, method, body, options) => {
      if (options && options.returnHeaders) {
        return Promise.resolve({ body: { value: [] }, headers: {} });
      }
      return Promise.resolve({ workItems: [], value: [] });
    });
  mockFetchPackageWorkItems.mockReset().mockResolvedValue(new Map());
  mockFetchAzureSdkPackageList.mockReset().mockResolvedValue("");
  mockBatchFetchPrStatuses.mockReset().mockResolvedValue(new Map());
  mockBatchFetchPrDetails.mockReset().mockResolvedValue(new Map());
  mockBatchFetchSpecProjectPaths.mockReset().mockResolvedValue(new Map());
  mockBatchFetchSpecPrLabels.mockReset().mockResolvedValue(new Map());

  // Reset cache state
  cache.releasePlans.data = null;
  cache.releasePlans.fetchedAt = null;
  cache.releasePlans.updatedAt = 0;
  cache.releasePlans.refreshing = false;
  cache.prDetails.clear();
  cache.prStatuses.clear();
});

function getPort() {
  return server.address().port;
}

function httpRequest(method, path, body) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: "localhost",
      port: getPort(),
      path,
      method,
      headers: { "Content-Type": "application/json" },
    };
    const req = http.request(options, (res) => {
      let data = "";
      res.on("data", (c) => (data += c));
      res.on("end", () => {
        let parsed;
        try {
          parsed = JSON.parse(data);
        } catch {
          parsed = data;
        }
        resolve({ status: res.statusCode, body: parsed });
      });
    });
    req.on("error", reject);
    if (body) req.write(JSON.stringify(body));
    req.end();
  });
}

// Helper: build a minimal work item matching what mapReleasePlan expects
function buildWorkItem(id, overrides = {}) {
  return {
    id,
    fields: {
      "System.Id": id,
      "System.Title": overrides.title || `Plan ${id}`,
      "System.State": overrides.state || "In Progress",
      "System.CreatedDate": "2024-01-01T00:00:00Z",
      "System.ChangedDate": "2024-06-01T00:00:00Z",
      "System.CreatedBy": { displayName: "Test Author" },
      "Custom.SDKReleasemonth": "2024-07",
      "Custom.SDKtypetobereleased": "GA",
      "Custom.ReleasePlanID": overrides.planId || `RP-${id}`,
      "Custom.ProductName": "TestProduct",
      "Custom.SDKPullRequestForDotnet":
        overrides.dotnetPrUrl ||
        `https://github.com/Azure/azure-sdk-for-net/pull/${id}`,
      "Custom.DotnetPackageName": overrides.dotnetPkg || "Azure.Test",
      ...(overrides.fields || {}),
    },
    relations: overrides.relations || [],
  };
}

describe("API routes", () => {
  describe("GET /api/release-plans", () => {
    test("returns plans structure (empty on first call)", async () => {
      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      expect(res.body).toHaveProperty("plans");
      expect(res.body).toHaveProperty("fetchedAt");
    });

    test("returns notFound when filtering by non-existent plan ID", async () => {
      const res = await httpRequest(
        "GET",
        "/api/release-plans?releasePlan=99999",
      );
      expect(res.status).toBe(200);
      expect(res.body.plans).toEqual([]);
      expect(res.body.notFound).toBe("99999");
    });

    test("returns 400 for invalid release plan ID format", async () => {
      const res = await httpRequest(
        "GET",
        "/api/release-plans?releasePlan=abc;DROP",
      );
      expect(res.status).toBe(400);
      expect(res.body.error).toContain("Invalid");
    });

    test("returns plans when filtering by valid plan ID with results", async () => {
      const wi = buildWorkItem(100, { planId: "VALID-123" });
      mockRunWiql.mockResolvedValueOnce([100]);
      mockFetchWorkItemsBatch.mockResolvedValueOnce([wi]);
      const res = await httpRequest(
        "GET",
        "/api/release-plans?releasePlan=VALID-123",
      );
      expect(res.status).toBe(200);
      expect(res.body.plans.length).toBe(1);
      expect(res.body.plans[0].releasePlanId).toBe("VALID-123");
    });

    test("returns cached data when available", async () => {
      cache.releasePlans.data = {
        plans: [{ id: 1, title: "Cached" }],
        fetchedAt: "2024-01-01T00:00:00Z",
      };
      cache.releasePlans.updatedAt = Date.now();
      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      expect(res.body.plans[0].title).toBe("Cached");
    });

    test("returns stale data when cache is refreshing", async () => {
      cache.releasePlans.data = {
        plans: [{ id: 1, title: "Stale" }],
        fetchedAt: "2024-01-01T00:00:00Z",
      };
      cache.releasePlans.refreshing = true;
      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      expect(res.body.plans[0].title).toBe("Stale");
    });

    test("returns loading state when no data and refreshing", async () => {
      cache.releasePlans.data = null;
      cache.releasePlans.refreshing = true;
      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      expect(res.body.loading).toBe(true);
    });

    test("handles errors gracefully", async () => {
      mockRunWiql.mockRejectedValueOnce(new Error("DevOps down"));
      // Force fresh fetch by not caching
      cache.releasePlans.data = null;
      cache.releasePlans.updatedAt = 0;
      const res = await httpRequest("GET", "/api/release-plans");
      // Should return 500 or empty data depending on error handling
      expect([200, 500]).toContain(res.status);
    });
  });

  describe("POST /api/refresh", () => {
    test("refreshes cache successfully", async () => {
      const res = await httpRequest("POST", "/api/refresh");
      expect(res.status).toBe(200);
      expect(res.body.ok).toBe(true);
      expect(res.body).toHaveProperty("fetchedAt");
    });

    test("clears PR caches on refresh", async () => {
      cache.prDetails.set("url1", { data: "d", updatedAt: 1 });
      cache.prStatuses.set("url1", { data: "s", updatedAt: 1 });
      await httpRequest("POST", "/api/refresh");
      expect(cache.prDetails.size).toBe(0);
      expect(cache.prStatuses.size).toBe(0);
    });
  });

  describe("POST /api/refresh-plan/:id", () => {
    test("returns 400 for non-numeric ID", async () => {
      const res = await httpRequest("POST", "/api/refresh-plan/invalid", {});
      expect(res.status).toBe(400);
    });

    test("returns 404 when work item not found", async () => {
      mockDevopsRequest.mockResolvedValueOnce({ value: [] });
      const res = await httpRequest("POST", "/api/refresh-plan/99999");
      expect(res.status).toBe(404);
    });

    test("refreshes a single plan by work item ID", async () => {
      const wi = buildWorkItem(200);
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      // For child items fetch
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      const res = await httpRequest("POST", "/api/refresh-plan/200");
      expect(res.status).toBe(200);
      expect(res.body).toHaveProperty("plan");
      expect(res.body.plan.id).toBe(200);
    });

    test("updates existing plan in global cache", async () => {
      cache.releasePlans.data = {
        plans: [{ id: 200, title: "Old" }],
        fetchedAt: "2024-01-01T00:00:00Z",
      };
      const wi = buildWorkItem(200, { title: "Updated" });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      await httpRequest("POST", "/api/refresh-plan/200");
      expect(cache.releasePlans.data.plans[0].title).toBe("Updated");
    });

    test("adds new plan to global cache if not present", async () => {
      cache.releasePlans.data = {
        plans: [{ id: 100, title: "Existing" }],
        fetchedAt: "2024-01-01T00:00:00Z",
      };
      const wi = buildWorkItem(300, { title: "New Plan" });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      await httpRequest("POST", "/api/refresh-plan/300");
      expect(cache.releasePlans.data.plans.length).toBe(2);
    });

    test("invalidates PR caches for refreshed plan's SDK PRs", async () => {
      const prUrl = "https://github.com/Azure/azure-sdk-for-net/pull/200";
      cache.prDetails.set(prUrl, { data: "old", updatedAt: 1 });
      cache.prStatuses.set(prUrl, { data: "old", updatedAt: 1 });
      const wi = buildWorkItem(200, { dotnetPrUrl: prUrl });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      await httpRequest("POST", "/api/refresh-plan/200");
      expect(cache.prDetails.has(prUrl)).toBe(false);
      expect(cache.prStatuses.has(prUrl)).toBe(false);
    });

    test("handles child API spec work items", async () => {
      const wi = buildWorkItem(400, {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/401",
          },
        ],
      });
      const specChild = {
        id: 401,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.ActiveSpecPullRequestUrl":
            "https://github.com/Azure/azure-rest-api-specs/pull/50",
          "Custom.RESTAPIReviews": "",
          "Custom.APISpecversion": "2024-01-01",
          "Custom.APISpecDefinitionType": "TypeSpec",
        },
      };
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([specChild]);
      const res = await httpRequest("POST", "/api/refresh-plan/400");
      expect(res.status).toBe(200);
      expect(res.body.plan.apiSpec).not.toBeNull();
      expect(res.body.plan.apiSpec.specPrUrl).toContain("pull/50");
    });

    test("handles errors gracefully", async () => {
      mockDevopsRequest.mockRejectedValueOnce(new Error("DevOps error"));
      const res = await httpRequest("POST", "/api/refresh-plan/500");
      expect(res.status).toBe(500);
    });
  });

  describe("POST /api/pr-statuses", () => {
    test("returns 400 if urls is not an array", async () => {
      const res = await httpRequest("POST", "/api/pr-statuses", {
        urls: "not-array",
      });
      expect(res.status).toBe(400);
      expect(res.body.error).toContain("array");
    });

    test("returns empty statuses for empty urls", async () => {
      const res = await httpRequest("POST", "/api/pr-statuses", { urls: [] });
      expect(res.status).toBe(200);
      expect(res.body.statuses).toEqual({});
    });

    test("filters out invalid URLs", async () => {
      const res = await httpRequest("POST", "/api/pr-statuses", {
        urls: [
          "not-a-url",
          "https://example.com",
          "https://github.com/org/repo/pull/1",
        ],
      });
      expect(res.status).toBe(200);
    });

    test("returns cached statuses when available", async () => {
      const url = "https://github.com/Azure/sdk/pull/1";
      cache.prStatuses.set(url, { data: "merged", updatedAt: Date.now() });
      const res = await httpRequest("POST", "/api/pr-statuses", {
        urls: [url],
      });
      expect(res.status).toBe(200);
      expect(res.body.statuses[url]).toBe("merged");
    });

    test("fetches and caches new statuses", async () => {
      const url = "https://github.com/Azure/sdk/pull/2";
      mockBatchFetchPrStatuses.mockResolvedValueOnce(new Map([[url, "open"]]));
      const res = await httpRequest("POST", "/api/pr-statuses", {
        urls: [url],
      });
      expect(res.status).toBe(200);
      expect(res.body.statuses[url]).toBe("open");
      expect(cache.prStatuses.has(url)).toBe(true);
    });

    test("handles errors gracefully", async () => {
      mockBatchFetchPrStatuses.mockRejectedValueOnce(new Error("API error"));
      const url = "https://github.com/Azure/sdk/pull/3";
      const res = await httpRequest("POST", "/api/pr-statuses", {
        urls: [url],
      });
      expect(res.status).toBe(500);
    });
  });

  describe("POST /api/pr-details", () => {
    test("returns 400 if urls is not an array", async () => {
      const res = await httpRequest("POST", "/api/pr-details", { urls: 123 });
      expect(res.status).toBe(400);
      expect(res.body.error).toContain("array");
    });

    test("returns empty details for empty urls", async () => {
      const res = await httpRequest("POST", "/api/pr-details", { urls: [] });
      expect(res.status).toBe(200);
      expect(res.body.details).toEqual({});
    });

    test("returns cached details when available", async () => {
      const url = "https://github.com/Azure/sdk/pull/10";
      const detailEntry = {
        gitHubStatus: "open",
        prDetails: { title: "Test PR" },
      };
      cache.prDetails.set(url, { data: detailEntry, updatedAt: Date.now() });
      const res = await httpRequest("POST", "/api/pr-details", {
        urls: [url],
      });
      expect(res.status).toBe(200);
      expect(res.body.details[url].prDetails.title).toBe("Test PR");
    });

    test("fetches and caches new details", async () => {
      const url = "https://github.com/Azure/sdk/pull/11";
      const mockDetails = {
        mergeable: true,
        mergeableState: "clean",
        isApproved: true,
        approvedBy: ["reviewer"],
        failedChecks: [],
        apiViewUrl: "",
        title: "New PR",
        requestedReviewers: [],
        latestComment: null,
        updatedAt: "2024-01-01",
      };
      mockBatchFetchPrStatuses.mockResolvedValueOnce(new Map([[url, "open"]]));
      mockBatchFetchPrDetails.mockResolvedValueOnce(
        new Map([[url, mockDetails]]),
      );
      const res = await httpRequest("POST", "/api/pr-details", {
        urls: [url],
      });
      expect(res.status).toBe(200);
      expect(res.body.details[url].gitHubStatus).toBe("open");
      expect(res.body.details[url].prDetails.title).toBe("New PR");
      expect(cache.prDetails.has(url)).toBe(true);
    });

    test("handles null details from fetch", async () => {
      const url = "https://github.com/Azure/sdk/pull/12";
      mockBatchFetchPrStatuses.mockResolvedValueOnce(new Map([[url, "open"]]));
      mockBatchFetchPrDetails.mockResolvedValueOnce(new Map([[url, null]]));
      const res = await httpRequest("POST", "/api/pr-details", {
        urls: [url],
      });
      expect(res.status).toBe(200);
      expect(res.body.details[url].prDetails).toBeNull();
    });

    test("handles errors gracefully", async () => {
      mockBatchFetchPrStatuses.mockRejectedValueOnce(new Error("API error"));
      const url = "https://github.com/Azure/sdk/pull/13";
      const res = await httpRequest("POST", "/api/pr-details", {
        urls: [url],
      });
      expect(res.status).toBe(500);
    });
  });

  describe("GET /api/previous-sdk-prs/:id", () => {
    test("returns 400 for non-numeric ID", async () => {
      const res = await httpRequest("GET", "/api/previous-sdk-prs/abc");
      expect(res.status).toBe(400);
      expect(res.body.error).toContain("Invalid");
    });

    test("returns previous PRs structure for valid ID", async () => {
      const res = await httpRequest("GET", "/api/previous-sdk-prs/12345");
      expect(res.status).toBe(200);
      expect(res.body).toHaveProperty("previousPrs");
    });

    test("extracts previous PR URLs from work item updates", async () => {
      mockDevopsRequest.mockResolvedValueOnce({
        body: {
          value: [
            {
              fields: {
                "Custom.SDKPullRequestForDotnet": {
                  oldValue:
                    "https://github.com/Azure/azure-sdk-for-net/pull/100",
                  newValue:
                    "https://github.com/Azure/azure-sdk-for-net/pull/200",
                },
              },
            },
          ],
        },
        headers: {},
      });
      const res = await httpRequest("GET", "/api/previous-sdk-prs/12345");
      expect(res.status).toBe(200);
      expect(res.body.previousPrs[".NET"]).toContain(
        "https://github.com/Azure/azure-sdk-for-net/pull/100",
      );
    });

    test("removes current PR from previous list", async () => {
      const currentPrUrl =
        "https://github.com/Azure/azure-sdk-for-net/pull/200";
      cache.releasePlans.data = {
        plans: [
          {
            id: 12345,
            languages: { ".NET": { sdkPrUrl: currentPrUrl } },
          },
        ],
      };
      mockDevopsRequest.mockResolvedValueOnce({
        body: {
          value: [
            {
              fields: {
                "Custom.SDKPullRequestForDotnet": {
                  oldValue: currentPrUrl,
                  newValue: "",
                },
              },
            },
          ],
        },
        headers: {},
      });
      const res = await httpRequest("GET", "/api/previous-sdk-prs/12345");
      expect(res.status).toBe(200);
      expect(res.body.previousPrs[".NET"]).not.toContain(currentPrUrl);
    });

    test("handles continuation token pagination", async () => {
      mockDevopsRequest
        .mockResolvedValueOnce({
          body: { value: [] },
          headers: { "x-ms-continuationtoken": "token123" },
        })
        .mockResolvedValueOnce({
          body: { value: [] },
          headers: {},
        });
      const res = await httpRequest("GET", "/api/previous-sdk-prs/12345");
      expect(res.status).toBe(200);
      expect(mockDevopsRequest).toHaveBeenCalledTimes(2);
    });

    test("handles errors gracefully", async () => {
      mockDevopsRequest.mockRejectedValueOnce(new Error("DevOps down"));
      const res = await httpRequest("GET", "/api/previous-sdk-prs/12345");
      expect(res.status).toBe(500);
    });
  });

  describe("enrichment coverage", () => {
    test("enrichSpecPrData sets apiReadiness from status map", async () => {
      const wi = buildWorkItem(500, {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/501",
          },
        ],
      });
      const specChild = {
        id: 501,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.ActiveSpecPullRequestUrl":
            "https://github.com/Azure/azure-rest-api-specs/pull/99",
          "Custom.RESTAPIReviews": "",
          "Custom.APISpecversion": "2024-01-01",
          "Custom.APISpecDefinitionType": "TypeSpec",
        },
      };
      const specUrl = "https://github.com/Azure/azure-rest-api-specs/pull/99";
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([specChild]);
      mockBatchFetchPrStatuses
        .mockResolvedValueOnce(new Map([[specUrl, "merged"]]))
        .mockResolvedValueOnce(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(
        new Map([[specUrl, "specification/test/Test.Management"]]),
      );
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(
        new Map([[specUrl, [{ name: "BreakingChange", color: "ff0000" }]]]),
      );
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");

      const res = await httpRequest("POST", "/api/refresh-plan/500");
      expect(res.status).toBe(200);
      expect(res.body.plan.apiReadiness).toBe("completed");
      expect(res.body.plan.specProjectPath).toBe(
        "specification/test/Test.Management",
      );
      expect(res.body.plan.specPrLabels).toHaveLength(1);
    });

    test("enrichSpecPrData sets pending for open spec PRs", async () => {
      const wi = buildWorkItem(510, {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/511",
          },
        ],
      });
      const specChild = {
        id: 511,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.ActiveSpecPullRequestUrl":
            "https://github.com/Azure/azure-rest-api-specs/pull/101",
          "Custom.RESTAPIReviews": "",
        },
      };
      const specUrl = "https://github.com/Azure/azure-rest-api-specs/pull/101";
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([specChild]);
      mockBatchFetchPrStatuses
        .mockResolvedValueOnce(new Map([[specUrl, "open"]]))
        .mockResolvedValueOnce(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");

      const res = await httpRequest("POST", "/api/refresh-plan/510");
      expect(res.status).toBe(200);
      expect(res.body.plan.apiReadiness).toBe("pending");
    });

    test("enrichPackageData matches package data and sets version", async () => {
      const wi = buildWorkItem(520, {
        dotnetPkg: "Azure.Storage.Blobs",
        fields: {
          "Custom.ReleaseStatusForDotnet": "In Progress",
        },
      });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      const pkgMap = new Map([
        [
          "Azure.Storage.Blobs|.NET",
          {
            version: "13.0.0",
            apiReviewStatus: "Approved",
            namespaceApproval: "Approved",
          },
        ],
      ]);
      mockFetchPackageWorkItems.mockResolvedValueOnce(pkgMap);
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("Azure.Storage.Blobs");
      mockBatchFetchPrStatuses.mockResolvedValue(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());

      const res = await httpRequest("POST", "/api/refresh-plan/520");
      expect(res.status).toBe(200);
      const dotnet = res.body.plan.languages[".NET"];
      expect(dotnet.pkgVersion).toBe("13.0.0");
      expect(dotnet.namespaceApproval).toBe("Approved");
      expect(dotnet.apiReviewStatus).toBe("Approved");
      expect(dotnet.isNewPackage).toBe(false);
    });

    test("enrichSdkPrStatuses sets statuses on SDK PRs", async () => {
      const sdkPrUrl = "https://github.com/Azure/azure-sdk-for-net/pull/600";
      const wi = buildWorkItem(530, { dotnetPrUrl: sdkPrUrl });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());
      // First call for spec PRs (enrichSpecPrData), second for SDK PRs (enrichSdkPrStatuses)
      mockBatchFetchPrStatuses
        .mockResolvedValueOnce(new Map())
        .mockResolvedValueOnce(new Map([[sdkPrUrl, "open"]]));

      const res = await httpRequest("POST", "/api/refresh-plan/530");
      expect(res.status).toBe(200);
      expect(res.body.plan.languages[".NET"].sdkPrGitHubStatus).toBe("open");
      // Verify batchFetchPrStatuses was called with SDK PR URLs
      expect(mockBatchFetchPrStatuses).toHaveBeenCalledTimes(2);
      const sdkCall = mockBatchFetchPrStatuses.mock.calls[1];
      expect(sdkCall[0]).toContain(sdkPrUrl);
    });

    test("enrichSpecPrData without GitHub token sets unknown apiReadiness", async () => {
      const ghToken = process.env.GH_TOKEN;
      delete process.env.GH_TOKEN;
      delete process.env.GITHUB_PAT_RELEASE_PLAN;

      const wi = buildWorkItem(550);
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");

      const res = await httpRequest("POST", "/api/refresh-plan/550");
      expect(res.status).toBe(200);
      expect(res.body.plan.apiReadiness).toBe("unknown");
      // Spec/SDK PR enrichment skipped — batchFetchPrStatuses should NOT be called
      expect(mockBatchFetchPrStatuses).not.toHaveBeenCalled();

      process.env.GH_TOKEN = ghToken;
    });

    test("enrichPlans catches package enrichment errors", async () => {
      const wi = buildWorkItem(540, { dotnetPkg: "Azure.Fail" });
      mockDevopsRequest.mockResolvedValueOnce({ value: [wi] });
      mockFetchWorkItemsBatch.mockResolvedValueOnce([]);
      mockFetchPackageWorkItems.mockRejectedValueOnce(
        new Error("Package API error"),
      );
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");
      mockBatchFetchPrStatuses.mockResolvedValue(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());

      const res = await httpRequest("POST", "/api/refresh-plan/540");
      // Should still succeed — package enrichment error is caught
      expect(res.status).toBe(200);
    });

    test("GET /api/release-plans catches errors in plan lookup path", async () => {
      mockRunWiql.mockRejectedValueOnce(new Error("WIQL fail"));
      const res = await httpRequest(
        "GET",
        "/api/release-plans?releasePlan=TEST-ERR",
      );
      expect(res.status).toBe(500);
      expect(res.body.error).toContain("Internal");
    });

    test("full refresh with work items exercises fetchAllReleasePlans", async () => {
      // Clear cache to force full refresh
      cache.releasePlans.data = null;
      cache.releasePlans.updatedAt = 0;
      cache.releasePlans.refreshing = false;

      const wi = buildWorkItem(900, {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/901",
          },
        ],
      });
      const specChild = {
        id: 901,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.ActiveSpecPullRequestUrl":
            "https://github.com/Azure/azure-rest-api-specs/pull/88",
          "Custom.APISpecDefinitionType": "TypeSpec",
        },
      };
      // runWiql returns IDs → triggers fetchWorkItemsBatch → buildApiSpecMap
      mockRunWiql.mockResolvedValueOnce([900]);
      mockFetchWorkItemsBatch
        .mockResolvedValueOnce([wi]) // main work items
        .mockResolvedValueOnce([specChild]); // child items (buildApiSpecMap)
      mockBatchFetchPrStatuses.mockResolvedValue(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");

      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      expect(res.body.plans.length).toBe(1);
      expect(res.body.plans[0].id).toBe(900);
      expect(res.body.plans[0].apiSpec).not.toBeNull();
      expect(res.body.plans[0].apiSpec.specPrUrl).toContain("pull/88");
    });

    test("fetchAllReleasePlans filters out OpenAPI definition types", async () => {
      cache.releasePlans.data = null;
      cache.releasePlans.updatedAt = 0;
      cache.releasePlans.refreshing = false;

      const wiTypeSpec = buildWorkItem(910);
      const wiOpenApi = buildWorkItem(911, {
        relations: [
          {
            rel: "System.LinkTypes.Hierarchy-Forward",
            url: "https://dev.azure.com/_apis/wit/workItems/912",
          },
        ],
      });
      const openApiSpec = {
        id: 912,
        fields: {
          "System.WorkItemType": "API Spec",
          "Custom.APISpecDefinitionType": "OpenAPI",
        },
      };
      mockRunWiql.mockResolvedValueOnce([910, 911]);
      mockFetchWorkItemsBatch
        .mockResolvedValueOnce([wiTypeSpec, wiOpenApi])
        .mockResolvedValueOnce([openApiSpec]);
      mockBatchFetchPrStatuses.mockResolvedValue(new Map());
      mockBatchFetchSpecProjectPaths.mockResolvedValueOnce(new Map());
      mockBatchFetchSpecPrLabels.mockResolvedValueOnce(new Map());
      mockFetchPackageWorkItems.mockResolvedValueOnce(new Map());
      mockFetchAzureSdkPackageList.mockResolvedValueOnce("");

      const res = await httpRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
      // OpenAPI plan should be filtered out
      expect(res.body.plans.length).toBe(1);
      expect(res.body.plans[0].id).toBe(910);
    });
  });
});
