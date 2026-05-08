import { describe, test, expect, vi, beforeAll, afterAll } from "vitest";

// Tests for routes/api.js input validation and caching behavior
// These test the route handlers' input validation without making real API calls.

process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";
process.env.DEVOPS_RELEASE_PLAN_PAT = "test-pat";

// Mock external API calls
vi.mock("../lib/devops-api.js", async () => {
  const original = await vi.importActual("../lib/devops-api.js");
  return {
    ...original,
    devopsRequest: vi.fn().mockImplementation((url, method, body, options) => {
      if (options && options.returnHeaders) {
        return Promise.resolve({ body: { value: [] }, headers: {} });
      }
      return Promise.resolve({ workItems: [], value: [] });
    }),
    runWiql: vi.fn().mockResolvedValue([]),
    fetchWorkItemsBatch: vi.fn().mockResolvedValue([]),
    fetchPackageWorkItems: vi.fn().mockResolvedValue(new Map()),
    fetchAzureSdkPackageList: vi.fn().mockResolvedValue(""),
  };
});

vi.mock("../lib/github-api.js", () => ({
  parseGitHubPrUrl: (url) => {
    if (!url) return null;
    const m = url.match(/github\.com\/([^/]+)\/([^/]+)\/pull\/(\d+)/);
    return m ? { owner: m[1], repo: m[2], number: m[3] } : null;
  },
  batchFetchPrStatuses: vi.fn().mockResolvedValue(new Map()),
  batchFetchPrDetails: vi.fn().mockResolvedValue(new Map()),
  batchFetchSpecProjectPaths: vi.fn().mockResolvedValue(new Map()),
}));

import express from "express";
import http from "node:http";

let app, server;

beforeAll(async () => {
  app = express();
  app.use(express.json());

  // Simulate authenticated user (in production, requireAuth sets req.user)
  app.use((req, res, next) => {
    req.user = { login: "testuser@microsoft.com", name: "Test User", objectId: "obj-123" };
    next();
  });

  const { default: apiRoutes } = await import("../routes/api.js");
  app.use(apiRoutes);

  await new Promise((resolve) => { server = app.listen(0, resolve); });
});

afterAll(async () => {
  await new Promise((resolve) => { server.close(resolve); });
});

function getPort() { return server.address().port; }

function httpRequest(method, path, body) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: "localhost", port: getPort(), path, method,
      headers: { "Content-Type": "application/json" },
    };
    const req = http.request(options, (res) => {
      let data = "";
      res.on("data", (c) => (data += c));
      res.on("end", () => {
        let parsed;
        try { parsed = JSON.parse(data); } catch { parsed = data; }
        resolve({ status: res.statusCode, body: parsed });
      });
    });
    req.on("error", reject);
    if (body) req.write(JSON.stringify(body));
    req.end();
  });
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
      const res = await httpRequest("GET", "/api/release-plans?releasePlan=99999");
      expect(res.status).toBe(200);
      expect(res.body.plans).toEqual([]);
      expect(res.body.notFound).toBe("99999");
    });
  });

  describe("POST /api/pr-statuses", () => {
    test("returns 400 if urls is not an array", async () => {
      const res = await httpRequest("POST", "/api/pr-statuses", { urls: "not-array" });
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
        urls: ["not-a-url", "https://example.com", "https://github.com/org/repo/pull/1"],
      });
      expect(res.status).toBe(200);
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

    test("accepts valid PR URLs", async () => {
      const res = await httpRequest("POST", "/api/pr-details", {
        urls: ["https://github.com/Azure/azure-sdk-for-net/pull/123"],
      });
      expect(res.status).toBe(200);
      expect(res.body).toHaveProperty("details");
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
  });

  describe("POST /api/refresh-plan/:id", () => {
    test("returns 400 for non-numeric ID", async () => {
      const res = await httpRequest("POST", "/api/refresh-plan/invalid", {});
      expect(res.status).toBe(400);
    });
  });
});
