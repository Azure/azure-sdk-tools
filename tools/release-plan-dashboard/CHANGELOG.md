# Release History

## 1.0.0-dev.1

### Features Added

- **Modular architecture** — Refactored monolithic server.js into modules: `lib/auth.js`, `lib/cache.js`, `lib/github-api.js`, `lib/devops-api.js`, `lib/rate-limit.js`, `routes/api.js`
- **Rate limiting** — Sliding-window rate limiter (30 req/min per user) on all API endpoints
- **Security hardening** — CSRF protection, session security (httpOnly, secure, sameSite), trust proxy, input validation with WIQL allowlist, error sanitization
- **Unit test suite** — 161+ Jest tests covering cache, rate-limit, auth, devops-api, github-api, API routes, server integration, package feed, and action logic
- **Package feed links** — Direct links to NuGet, PyPI, npm, Maven Central, and GitHub (Go) with icons and feed name labels; shown only for released packages
- **Released version display** — Shows version from `ReleasedVersionFor<Language>` ADO field; displays "Not available" when released but version field is empty
- **SDK target month filter** — Dropdown filter to select release plans by target SDK release month; available on Release Plans and PM tabs
- **Enhanced global search** — Filter now searches across package names, spec PR URLs/numbers, spec project paths, and SDK PR URLs in addition to title/service/owner
- **URL parameter sync** — Filter and month selections reflected in URL for sharing (`?filter=...&month=...`)
- **Action buttons per language** — Contextual action guidance:
  - ⚡ Generate SDK — when no PR exists
  - 🔗 Link PR — when PR is closed (with step-by-step guidance to link or regenerate)
  - 🔧 Fix Checks — when PR has failing checks
  - 🚀 Release — when PR is merged but not yet released
  - ✅ Merge PR — when PR is approved and mergeable
- **PM view access control** — PM actions only visible to whitelisted users (via `RELEASE_PLAN_DASHBOARD_PM_USERS` env var) and only in the Attention Required tab
- **Closed draft PR fix** — PRs that were drafts when closed now correctly show "closed" status (not "draft")
- **Cancelled check runs** — GitHub check runs with "cancelled" conclusion no longer reported as failures
- **Duplicate label deduplication** — PR labels (Approved, Ready to merge) no longer duplicated on lazy-load refresh
- **Auto-refresh** — Dashboard data refreshes every hour; "Last refreshed" timestamp in header
- **Short link** — Dashboard short link displayed in header: `https://aka.ms/azsdk/releaseplan-dashboard`
- **Favicon** — Added favicon route to prevent 404 during OAuth redirect
- **Environment validation** — Server exits with clear error message listing any missing required env vars
- **Version display** — Dashboard version shown in page footer
