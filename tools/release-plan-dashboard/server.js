// Release Plan Dashboard — Express server with Azure Easy Auth and cached API data.

import express from "express";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";

import { mintGitHubAppToken, parseEasyAuthPrincipal } from "./lib/auth.js";
import { createRateLimiter } from "./lib/rate-limit.js";
import { CACHE_TTL_MS } from "./lib/cache.js";
import apiRoutes from "./routes/api.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// ── Validate required env vars ────────────────────────────────
function validateEnvVars() {
  const KEYVAULT_NAME = process.env.KEYVAULT_NAME;
  const KEYVAULT_KEY_NAME = process.env.KEYVAULT_KEY_NAME;
  const GITHUB_APP_ID = process.env.GITHUB_APP_NUMERIC_ID;
  const GITHUB_INSTALL_OWNER = process.env.GITHUB_INSTALL_OWNER;

  const missing = [
    ["KEYVAULT_NAME", KEYVAULT_NAME],
    ["KEYVAULT_KEY_NAME", KEYVAULT_KEY_NAME],
    ["GITHUB_APP_NUMERIC_ID", GITHUB_APP_ID],
    ["GITHUB_INSTALL_OWNER", GITHUB_INSTALL_OWNER],
  ]
    .filter(([, v]) => !v)
    .map(([k]) => k);

  return missing;
}

const GH_TOKEN_REFRESH_MS = 50 * 60 * 1000;
const DEFAULT_PORT = 3000;
const RATE_LIMIT_WINDOW_MS = 60 * 1000; // 1 minute
const RATE_LIMIT_MAX_REQUESTS = 30;

// ── App factory ───────────────────────────────────────────────
function createApp() {
  const app = express();

  // Trust the reverse proxy (Azure App Service / front-door)
  app.set("trust proxy", 1);

  app.use(express.json());

  // ── Health check (unauthenticated) ────────────────────────────
  app.get("/health", (req, res) => {
    res.json({ status: "healthy", uptime: process.uptime() });
  });

  // ── Validate Easy Auth environment in production ──────────────
  /* v8 ignore start — only runs in production */
  if (
    process.env.NODE_ENV === "production" &&
    !process.env.WEBSITE_AUTH_ENABLED
  ) {
    console.warn(
      "WARNING: WEBSITE_AUTH_ENABLED is not set. Ensure Azure App Service Authentication is configured to prevent header spoofing.",
    );
  }
  /* v8 ignore stop */

  // ── Authentication middleware (Azure Easy Auth) ───────────────
  const PUBLIC_ROUTES = ["/health", "/favicon.ico", "/auth/logout"];

  function requireAuth(req, res, next) {
    if (PUBLIC_ROUTES.includes(req.path)) return next();
    const principal = parseEasyAuthPrincipal(req);
    if (!principal) {
      return res.status(401).json({
        error: "Authentication required. Please sign in via your organization.",
      });
    }
    req.user = principal;
    next();
  }
  app.use(requireAuth);

  // ── Auth routes ───────────────────────────────────────────────
  app.get("/auth/me", (req, res) => {
    const user = req.user;
    let responseUser = user
      ? { login: user.login, name: user.name, avatar: "" }
      : /* v8 ignore next */ null;
    if (responseUser) {
      const pmList = (process.env.RELEASE_PLAN_DASHBOARD_PM_USERS || "")
        .split(",")
        .map((u) => u.trim().toLowerCase())
        .filter(Boolean);
      responseUser.isPM = pmList.includes((user.login || "").toLowerCase());
    }
    res.json(responseUser);
  });

  app.get("/auth/logout", (_req, res) => {
    res.redirect("/.auth/logout?post_logout_redirect_uri=/");
  });

  // ── Rate limiting for API endpoints ───────────────────────────
  const apiRateLimiter = createRateLimiter({
    windowMs: RATE_LIMIT_WINDOW_MS,
    maxRequests: RATE_LIMIT_MAX_REQUESTS,
  });
  app.use("/api", apiRateLimiter);

  // ── API routes ────────────────────────────────────────────────
  app.use(apiRoutes);

  // ── Favicon (no file needed) ──────────────────────────────────
  app.get("/favicon.ico", (_req, res) => res.status(204).end());

  // ── Static files (behind auth) ────────────────────────────────
  app.use(express.static(path.join(__dirname, "public")));

  return app;
}

// ── Start server + background cache refresh ──────────────────
/* v8 ignore start — startup side-effect; tested indirectly via createApp/validateEnvVars */
async function start() {
  const missing = validateEnvVars();
  if (missing.length) {
    console.error(
      `ERROR: Missing required environment variables: ${missing.join(", ")}`,
    );
    process.exit(1);
  }

  const PORT = process.env.PORT || DEFAULT_PORT;
  const app = createApp();

  // Mint GitHub App token before accepting requests
  await mintGitHubAppToken();

  if (!process.env.GITHUB_PAT_RELEASE_PLAN && !process.env.GH_TOKEN) {
    console.warn(
      "WARNING: Neither GITHUB_PAT_RELEASE_PLAN nor GH_TOKEN is set — GitHub PR enrichment will be unavailable.",
    );
  }

  // Pre-warm cache before listening
  try {
    await apiRoutes.refreshReleasePlansCache();
  } catch (err) {
    console.error("Initial cache warm-up failed:", err.message);
  }

  app.listen(PORT, () => {
    console.log(`Release Plan Dashboard running on http://localhost:${PORT}`);
    console.log(
      "Authentication: Azure App Service Easy Auth (Microsoft Entra ID)",
    );
  });

  // Refresh GitHub App token every 50 minutes
  setInterval(() => {
    mintGitHubAppToken().catch((err) =>
      console.warn("Token refresh failed:", err.message),
    );
  }, GH_TOKEN_REFRESH_MS);

  // Refresh cache every hour in the background
  setInterval(() => {
    apiRoutes
      .refreshReleasePlansCache()
      .catch((err) =>
        console.error("Scheduled cache refresh failed:", err.message),
      );
  }, CACHE_TTL_MS);
}

const isEntryPoint =
  process.argv[1] &&
  fileURLToPath(import.meta.url) === path.resolve(process.argv[1]);

if (isEntryPoint) {
  start().catch((err) => {
    console.error("Fatal startup error:", err);
    process.exit(1);
  });
}
/* v8 ignore stop */

export { createApp, validateEnvVars };
