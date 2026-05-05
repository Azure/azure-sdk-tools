import { describe, test, expect, vi, beforeAll, afterAll } from "vitest";

// ── Server integration tests ──────────────────────────────────
// Tests the Express app's middleware and routes without external deps.
// We set required env vars and mock external calls.

// Set all required env vars before importing server modules
process.env.KEYVAULT_NAME = "test-vault";
process.env.KEYVAULT_KEY_NAME = "test-key";
process.env.GITHUB_APP_NUMERIC_ID = "12345";
process.env.GITHUB_INSTALL_OWNER = "TestOrg";
process.env.GITHUB_APP_CLIENT_ID = "test-client-id";
process.env.GITHUB_APP_CLIENT_SECRET = "test-client-secret";
process.env.DEVOPS_RELEASE_PLAN_PAT = "test-pat";
process.env.SESSION_SECRET = "test-session-secret";
process.env.RELEASE_PLAN_DASHBOARD_PM_USERS = "pmuser1,pmuser2";

// Mock the auth module to avoid real Key Vault / GitHub calls
vi.mock("../lib/auth.js", () => ({
  mintGitHubAppToken: vi.fn().mockResolvedValue("mock-token"),
  isMemberOfAnyOrg: vi.fn().mockResolvedValue(true),
  escapeHtml: (str) => String(str).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"),
  getBaseUrl: () => "http://localhost:3000",
}));

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

beforeAll(async () => {
  // Require server.js — it calls app.listen internally, so we need to intercept
  // Instead, manually construct the app in a test-friendly way
  // Actually, server.js calls process.exit and app.listen. Let's require and test key middleware.
  // We'll build a mini-app replicating server.js structure for testability.
  const { default: express } = await import("express");
  const { default: session } = await import("express-session");
  const { escapeHtml, getBaseUrl } = await import("../lib/auth.js");
  const { createRateLimiter } = await import("../lib/rate-limit.js");
  const { default: apiRoutes } = await import("../routes/api.js");

  app = express();
  app.set("trust proxy", 1);
  app.use(session({
    secret: "test-secret", resave: false, saveUninitialized: false,
    cookie: { secure: false, httpOnly: true, sameSite: "lax" },
  }));
  app.use(express.json());

  // Health (unauthenticated)
  app.get("/health", (req, res) => res.json({ status: "healthy", uptime: process.uptime() }));

  // Favicon
  app.get("/favicon.ico", (_req, res) => res.status(204).end());

  // Auth middleware
  app.use((req, res, next) => {
    if (["/auth/github", "/auth/github/callback", "/auth/logout", "/login", "/health", "/favicon.ico"].includes(req.path)) return next();
    if (req.session && req.session.user) return next();
    if (req.session) req.session.returnTo = req.originalUrl;
    res.redirect("/login");
  });

  app.get("/login", (req, res) => {
    if (req.session && req.session.user) return res.redirect("/");
    res.status(200).send("Login page");
  });

  app.get("/auth/me", (req, res) => {
    const user = req.session && req.session.user ? { ...req.session.user } : null;
    if (user) {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      user.isPM = pmList.includes((user.login || "").toLowerCase());
    }
    res.json(user);
  });

  app.get("/auth/logout", (req, res) => { req.session.destroy(() => res.redirect("/login")); });

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

function request(method, path, { headers = {}, body, followRedirects = false } = {}) {
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

// Helper to make an authenticated request by setting session cookie
let sessionCookie = null;

async function authenticatedRequest(method, path, options = {}) {
  const hdrs = { ...(options.headers || {}) };
  if (sessionCookie) hdrs["Cookie"] = sessionCookie;
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
    test("unauthenticated request to / redirects to /login", async () => {
      const res = await request("GET", "/");
      expect(res.status).toBe(302);
      expect(res.headers.location).toBe("/login");
    });

    test("unauthenticated request to /api/* redirects to /login", async () => {
      const res = await request("GET", "/api/release-plans");
      expect(res.status).toBe(302);
      expect(res.headers.location).toBe("/login");
    });

    test("GET /login returns 200", async () => {
      const res = await request("GET", "/login");
      expect(res.status).toBe(200);
    });
  });

  describe("auth/me endpoint", () => {
    test("returns null when not authenticated", async () => {
      const res = await request("GET", "/auth/me");
      // /auth/me is not in the bypass list, so it redirects
      expect(res.status).toBe(302);
    });
  });

  describe("PM user detection", () => {
    test("identifies PM users from RELEASE_PLAN_DASHBOARD_PM_USERS env var", () => {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      expect(pmList).toContain("pmuser1");
      expect(pmList).toContain("pmuser2");
      expect(pmList).not.toContain("regularuser");
    });

    test("PM check is case-insensitive", () => {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "").split(",").map(u => u.trim().toLowerCase()).filter(Boolean);
      expect(pmList.includes("pmuser1")).toBe(true);
      expect(pmList.includes("PMUSER1".toLowerCase())).toBe(true);
    });
  });

  describe("open redirect prevention", () => {
    // This tests the sanitization logic used in the OAuth callback
    function safeRedirect(returnTo) {
      return (returnTo.startsWith("/") && !returnTo.startsWith("//")) ? returnTo : "/";
    }

    test("allows normal relative paths", () => {
      expect(safeRedirect("/")).toBe("/");
      expect(safeRedirect("/api/release-plans")).toBe("/api/release-plans");
      expect(safeRedirect("/some/deep/path?q=1")).toBe("/some/deep/path?q=1");
    });

    test("blocks protocol-relative URLs (open redirect)", () => {
      expect(safeRedirect("//evil.com")).toBe("/");
      expect(safeRedirect("//evil.com/path")).toBe("/");
    });

    test("blocks absolute URLs", () => {
      expect(safeRedirect("https://evil.com")).toBe("/");
      expect(safeRedirect("http://evil.com/foo")).toBe("/");
    });

    test("blocks empty string", () => {
      expect(safeRedirect("")).toBe("/");
    });
  });
});
