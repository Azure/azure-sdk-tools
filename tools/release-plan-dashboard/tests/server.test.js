import { describe, test, expect, vi, beforeAll, afterAll } from "vitest";

// ── Server integration tests ──────────────────────────────────
// Tests the Express app's middleware and routes without external deps.
// We set required env vars and mock external calls.

// Set all required env vars before importing server modules
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";
process.env.RELEASE_PLAN_DASHBOARD_PM_USERS = "pmuser@microsoft.com,pmuser2@microsoft.com";

// Mock only mintGitHubAppToken (network call); use real parseEasyAuthPrincipal
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
  router.get("/api/release-plans", (req, res) => res.json({ plans: [], fetchedAt: null }));
  router.refreshReleasePlansCache = vi.fn().mockResolvedValue(undefined);
  return { default: router };
});

import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

let app, server;

function makePrincipalHeader(claims) {
  return Buffer.from(JSON.stringify({ claims })).toString("base64");
}

beforeAll(async () => {
  const { default: express } = await import("express");
  const { parseEasyAuthPrincipal } = await import("../lib/auth.js");
  const { createRateLimiter } = await import("../lib/rate-limit.js");
  const { default: apiRoutes } = await import("../routes/api.js");

  app = express();
  app.set("trust proxy", 1);
  app.use(express.json());

  // Health (unauthenticated)
  app.get("/health", (req, res) => res.json({ status: "healthy", uptime: process.uptime() }));

  // Favicon
  app.get("/favicon.ico", (_req, res) => res.status(204).end());

  // Auth middleware
  const PUBLIC_ROUTES = ["/health", "/favicon.ico", "/auth/logout"];
  app.use((req, res, next) => {
    if (PUBLIC_ROUTES.includes(req.path)) return next();
    const principal = parseEasyAuthPrincipal(req);
    if (!principal) {
      return res.status(401).json({ error: "Authentication required. Please sign in via your organization." });
    }
    req.user = principal;
    next();
  });

  app.get("/auth/me", (req, res) => {
    const user = req.user;
    let responseUser = user ? { login: user.login, name: user.name, avatar: "" } : null;
    if (responseUser) {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      responseUser.isPM = pmList.includes((user.login || "").toLowerCase());
    }
    res.json(responseUser);
  });

  app.get("/auth/logout", (_req, res) => res.redirect("/.auth/logout?post_logout_redirect_uri=/"));

  // Rate limiter
  const apiRateLimiter = createRateLimiter({ windowMs: 60000, maxRequests: 5 });
  app.use("/api", apiRateLimiter);
  app.use(apiRoutes);

  // Static files
  app.use(express.static(path.join(__dirname, "../public")));

  await new Promise((resolve) => { server = app.listen(0, resolve); });
});

afterAll(async () => {
  await new Promise((resolve) => { server.close(resolve); });
});

function getPort() {
  return server.address().port;
}

function request(method, path, { headers = {}, body } = {}) {
  return new Promise((resolve, reject) => {
    const url = new URL(path, `http://localhost:${getPort()}`);
    const options = {
      hostname: "localhost", port: getPort(), path: url.pathname + url.search,
      method, headers: { ...headers },
    };
    if (body) {
      const data = typeof body === "string" ? body : JSON.stringify(body);
      options.headers["Content-Type"] = options.headers["Content-Type"] || "application/json";
      options.headers["Content-Length"] = Buffer.byteLength(data);
    }
    const req = http.request(options, (res) => {
      let data = "";
      res.on("data", (c) => (data += c));
      res.on("end", () => resolve({ status: res.statusCode, headers: res.headers, body: data }));
    });
    req.on("error", reject);
    if (body) req.write(typeof body === "string" ? body : JSON.stringify(body));
    req.end();
  });
}

function authenticatedRequest(method, path, options = {}) {
  const principalHeader = makePrincipalHeader([
    { typ: "preferred_username", val: "testuser@microsoft.com" },
    { typ: "name", val: "Test User" },
    { typ: "http://schemas.microsoft.com/identity/claims/objectidentifier", val: "obj-123" },
  ]);
  const hdrs = {
    "x-ms-client-principal": principalHeader,
    "x-ms-client-principal-name": "testuser@microsoft.com",
    "x-ms-client-principal-id": "obj-123",
    ...(options.headers || {}),
  };
  return request(method, path, { ...options, headers: hdrs });
}

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
      expect(res.headers.location).toBe("/.auth/logout?post_logout_redirect_uri=/");
    });
  });

  describe("PM user detection", () => {
    test("identifies PM users from RELEASE_PLAN_DASHBOARD_PM_USERS env var", () => {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      expect(pmList).toContain("pmuser@microsoft.com");
      expect(pmList).toContain("pmuser2@microsoft.com");
      expect(pmList).not.toContain("regularuser@microsoft.com");
    });

    test("PM check is case-insensitive", () => {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      expect(pmList.includes("pmuser@microsoft.com")).toBe(true);
      expect(pmList.includes("PMUSER@MICROSOFT.COM".toLowerCase())).toBe(true);
    });
  });
});
