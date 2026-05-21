import { describe, test, expect, vi, beforeAll, afterAll } from "vitest";

// ── Server integration tests ──────────────────────────────────
// Tests the Express app by importing createApp/validateEnvVars from server.js.
// We set required env vars and mock external calls so server.js gets real coverage.

// vi.hoisted runs before vi.mock and imports — env vars must be set here
// because server.js reads them at module level AND calls start() as a side effect.
vi.hoisted(() => {
  process.env.KEYVAULT_NAME = "test-vault";
  process.env.KEYVAULT_KEY_NAME = "test-key";
  process.env.GITHUB_APP_NUMERIC_ID = "12345";
  process.env.GITHUB_INSTALL_OWNER = "TestOrg";
  process.env.RELEASE_PLAN_DASHBOARD_PM_USERS =
    "pmuser@microsoft.com,pmuser2@microsoft.com";
  // Use port 0 so the side-effect start() server binds to a random port
  process.env.PORT = "0";
});

// Mock only mintGitHubAppToken (network call); keep real parseEasyAuthPrincipal
vi.mock("../lib/auth.js", async () => {
  const actual = await vi.importActual("../lib/auth.js");
  return {
    ...actual,
    mintGitHubAppToken: vi.fn().mockResolvedValue("mock-token"),
  };
});

// Mock the routes/api to avoid real DevOps calls
vi.mock("../routes/api.js", async () => {
  const { default: express } = await import("express");
  const router = express.Router();
  router.get("/api/release-plans", (req, res) =>
    res.json({ plans: [], fetchedAt: null }),
  );
  router.refreshReleasePlansCache = vi.fn().mockResolvedValue(undefined);
  return { default: router };
});

import http from "node:http";
import { createApp, validateEnvVars } from "../server.js";

let app, server;

function makePrincipalHeader(claims) {
  return Buffer.from(JSON.stringify({ claims })).toString("base64");
}

beforeAll(async () => {
  app = createApp();
  await new Promise((resolve) => {
    server = app.listen(0, resolve);
  });
});

afterAll(async () => {
  await new Promise((resolve) => {
    server.close(resolve);
  });
});

function getPort() {
  return server.address().port;
}

function request(method, urlPath, { headers = {}, body } = {}) {
  return new Promise((resolve, reject) => {
    const url = new URL(urlPath, `http://localhost:${getPort()}`);
    const options = {
      hostname: "localhost",
      port: getPort(),
      path: url.pathname + url.search,
      method,
      headers: { ...headers },
    };
    if (body) {
      const data = typeof body === "string" ? body : JSON.stringify(body);
      options.headers["Content-Type"] =
        options.headers["Content-Type"] || "application/json";
      options.headers["Content-Length"] = Buffer.byteLength(data);
    }
    const req = http.request(options, (res) => {
      let data = "";
      res.on("data", (c) => (data += c));
      res.on("end", () =>
        resolve({ status: res.statusCode, headers: res.headers, body: data }),
      );
    });
    req.on("error", reject);
    if (body) req.write(typeof body === "string" ? body : JSON.stringify(body));
    req.end();
  });
}

function authenticatedRequest(method, urlPath, options = {}) {
  const principalHeader = makePrincipalHeader([
    { typ: "preferred_username", val: "testuser@microsoft.com" },
    { typ: "name", val: "Test User" },
    {
      typ: "http://schemas.microsoft.com/identity/claims/objectidentifier",
      val: "obj-123",
    },
  ]);
  const hdrs = {
    "x-ms-client-principal": principalHeader,
    "x-ms-client-principal-name": "testuser@microsoft.com",
    "x-ms-client-principal-id": "obj-123",
    ...(options.headers || {}),
  };
  return request(method, urlPath, { ...options, headers: hdrs });
}

describe("validateEnvVars", () => {
  test("returns missing vars when env vars are unset", () => {
    const saved = {};
    const keys = [
      "KEYVAULT_NAME",
      "KEYVAULT_KEY_NAME",
      "GITHUB_APP_NUMERIC_ID",
      "GITHUB_INSTALL_OWNER",
    ];
    for (const key of keys) {
      saved[key] = process.env[key];
      delete process.env[key];
    }
    try {
      const missing = validateEnvVars();
      expect(missing).toEqual(keys);
    } finally {
      for (const key of keys) {
        process.env[key] = saved[key];
      }
    }
  });

  test("returns empty array when all env vars are set", () => {
    const missing = validateEnvVars();
    expect(missing).toEqual([]);
  });

  test("returns only the missing vars", () => {
    const saved = process.env.KEYVAULT_NAME;
    delete process.env.KEYVAULT_NAME;
    try {
      const missing = validateEnvVars();
      expect(missing).toEqual(["KEYVAULT_NAME"]);
    } finally {
      process.env.KEYVAULT_NAME = saved;
    }
  });
});

describe("createApp", () => {
  test("returns an Express app with listen method", () => {
    const testApp = createApp();
    expect(typeof testApp.listen).toBe("function");
    expect(typeof testApp.use).toBe("function");
  });
});

describe("server integration", () => {
  describe("health endpoint", () => {
    test("GET /health returns 200 with status", async () => {
      const res = await request("GET", "/health");
      expect(res.status).toBe(200);
      const body = JSON.parse(res.body);
      expect(body.status).toBe("healthy");
      expect(typeof body.uptime).toBe("number");
    });
  });

  describe("favicon", () => {
    test("GET /favicon.ico returns 204", async () => {
      const res = await request("GET", "/favicon.ico");
      expect(res.status).toBe(204);
    });
  });

  describe("authentication middleware", () => {
    test("unauthenticated request to / returns 401", async () => {
      const res = await request("GET", "/");
      expect(res.status).toBe(401);
    });

    test("unauthenticated request to /api/* returns 401", async () => {
      const res = await request("GET", "/api/release-plans");
      expect(res.status).toBe(401);
    });

    test("authenticated request to /api/* succeeds", async () => {
      const res = await authenticatedRequest("GET", "/api/release-plans");
      expect(res.status).toBe(200);
    });
  });

  describe("auth/me endpoint", () => {
    test("returns user info when authenticated", async () => {
      const res = await authenticatedRequest("GET", "/auth/me");
      expect(res.status).toBe(200);
      const body = JSON.parse(res.body);
      expect(body.login).toBe("testuser@microsoft.com");
      expect(body.name).toBe("Test User");
      expect(body.isPM).toBe(false);
    });

    test("returns isPM true for PM users", async () => {
      const principalHeader = makePrincipalHeader([
        { typ: "preferred_username", val: "pmuser@microsoft.com" },
        { typ: "name", val: "PM User" },
      ]);
      const res = await request("GET", "/auth/me", {
        headers: {
          "x-ms-client-principal": principalHeader,
          "x-ms-client-principal-name": "pmuser@microsoft.com",
        },
      });
      expect(res.status).toBe(200);
      const body = JSON.parse(res.body);
      expect(body.isPM).toBe(true);
    });

    test("returns 401 when not authenticated", async () => {
      const res = await request("GET", "/auth/me");
      expect(res.status).toBe(401);
    });
  });

  describe("auth/logout endpoint", () => {
    test("redirects to Azure Easy Auth logout", async () => {
      const res = await request("GET", "/auth/logout");
      expect(res.status).toBe(302);
      expect(res.headers.location).toBe(
        "/.auth/logout?post_logout_redirect_uri=/",
      );
    });
  });

  describe("rate limiting", () => {
    test("returns 429 after exceeding rate limit on /api paths", async () => {
      // The real createRateLimiter uses 30 req/min — send 31 requests
      const results = [];
      for (let i = 0; i < 31; i++) {
        results.push(await authenticatedRequest("GET", "/api/release-plans"));
      }
      const last = results[results.length - 1];
      expect(last.status).toBe(429);
      const body = JSON.parse(last.body);
      expect(body.error).toMatch(/too many requests/i);
      expect(last.headers["retry-after"]).toBeDefined();
    });
  });
});
