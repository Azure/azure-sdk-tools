# Agent Instructions — Release Plan Dashboard

## Project Overview

This is a Node.js/Express web application (ESM, Node 22+) that displays Azure SDK Release Plan work items from Azure DevOps, enriched with GitHub PR status data. It deploys to Azure App Service.

## Tech Stack

- **Runtime:** Node.js 22+ (ESM modules — `"type": "module"` in package.json)
- **Server:** Express 5.x
- **Auth:** GitHub OAuth + Azure Managed Identity
- **GitHub API:** `@octokit/rest` for PR enrichment
- **Azure DevOps:** Native `fetch()` with `@azure/identity` (DefaultAzureCredential)
- **Key Vault:** `@azure/keyvault-keys` for JWT signing
- **Tests:** Vitest (`npm test` runs `vitest run`)
- **Frontend:** Vanilla JS SPA (`public/app.js`) — no framework currently

## File Structure

```
server.js              # Entry point — Express setup, OAuth, startup
lib/
  auth.js              # GitHub App JWT minting, OAuth helpers
  cache.js             # In-memory cache (release plans, PR details)
  devops-api.js        # Azure DevOps WIQL queries, work item mapping
  github-api.js        # GitHub PR status/details via Octokit
  rate-limit.js        # Sliding-window rate limiter middleware
routes/
  api.js               # All /api/* route handlers, cache refresh logic
public/
  index.html           # Main SPA page
  login.html           # Login page (static, error via URL params)
  app.js               # Client-side rendering (2100+ lines, vanilla JS)
  style.css            # All styles
tests/                 # Vitest test suites (9 files, 161+ tests)
```

## Coding Conventions

### ESM Modules
- Use `import`/`export` — NO `require()` or `module.exports`
- Node built-ins use `node:` prefix: `import crypto from "node:crypto"`
- Local imports include `.js` extension: `import { foo } from "./lib/bar.js"`
- Use `import.meta.url` pattern for `__dirname`:
  ```js
  import { fileURLToPath } from "node:url";
  import { dirname } from "node:path";
  const __dirname = dirname(fileURLToPath(import.meta.url));
  ```

### HTTP Requests
- **Azure DevOps:** Use `fetch()` with Bearer token from `DefaultAzureCredential`
- **GitHub API:** Use `@octokit/rest` (Octokit class) — NOT raw fetch or node:https
- **Timeouts:** Always set timeouts on outbound requests (30s default)

### Security
- Session cookie uses `sameSite: "lax"` for CSRF protection (no custom CSRF middleware)
- Never store user OAuth tokens in session — only store `{ login, name, avatar }`
- Escape all user-provided data in HTML output with `esc()` helper (client-side)
- Validate/sanitize inputs; use allowlist-based WIQL parameter validation

### Error Handling
- Server startup: fail fast with clear error messages for missing env vars
- API routes: catch errors, log them, return sanitized error responses (no stack traces)
- GitHub/DevOps API failures: log warnings, resolve with null (don't crash the server)

### Caching
- Release plans: 1 hour TTL, refreshed via setInterval
- PR details: 15 min TTL, fetched on-demand
- PR statuses: 1 hour TTL, fetched during plan enrichment
- Max 5000 entries per cache Map (LRU eviction)

### Testing
- Test framework: **Vitest** (`npm test`)
- Test files: `tests/*.test.js`
- Mock external dependencies (Azure Identity, fetch, Octokit)
- Import from vitest: `import { describe, test, expect, vi, beforeEach } from "vitest"`
- Mock pattern:
  ```js
  vi.mock("@azure/identity", () => ({ DefaultAzureCredential: vi.fn() }));
  ```

### Frontend (public/app.js)
- Vanilla JS SPA with Alpine.js for reactive UI controls (tabs, filters, stats, modal)
- Card rendering uses imperative innerHTML with `esc()` XSS helper
- All click handlers use document-level event delegation (no per-card listener binding)
- All DOM rendering uses string templates with `esc()` for XSS prevention
- `esc()` must be used on ALL user/API data inserted into HTML
- Data attributes must also be escaped: `data-id="${esc(value)}"`
- CSS variables defined in `:root` — always define before referencing

### Variable Naming
- Use descriptive names: `workItem` not `wi`, `fields` not `f`, `response` not `res` (in non-Express contexts)
- Abbreviations allowed: `pr`, `url`, `id`, `auth`, `config`

## Environment Variables (Required)

| Variable | Purpose |
|---|---|
| `KEYVAULT_NAME` | Azure Key Vault name |
| `KEYVAULT_KEY_NAME` | Signing key name |
| `GITHUB_APP_NUMERIC_ID` | GitHub App ID |
| `GITHUB_INSTALL_OWNER` | GitHub org for App installation |
| `GITHUB_APP_CLIENT_ID` | OAuth client ID |
| `GITHUB_APP_CLIENT_SECRET` | OAuth client secret |

## Key Design Decisions

1. **Managed Identity** for Azure DevOps (not PAT) — uses `DefaultAzureCredential`
2. **Server-side enrichment** — PR statuses/details are fetched server-side and cached
3. **No CSRF middleware** — `sameSite: "lax"` cookie provides protection
4. **Startup order** — Token mint → cache warm → listen (no race conditions)
5. **PR status priority** — merged > closed > draft > state (closed checked before draft because GitHub keeps `draft: true` on closed draft PRs)
6. **Release status** — exact match `=== "released"` not `.includes("released")` because "Unreleased" contains "released"
7. **Feed links** — only shown when release status is exactly "Released"

## Common Tasks

### Adding a new API field from DevOps
1. Add field name to `RELEASE_PLAN_FIELDS` array in `lib/devops-api.js`
2. Map it in `mapReleasePlan()` function
3. Use it in `public/app.js` rendering

### Adding a new action button
1. Add action type determination logic in `public/app.js` (around line 1388)
2. Add label in `langActionBtn` labels object
3. Add CSS class in `langActionBtn` classes object
4. Add case in `buildActionPopupContent` switch
5. Add CSS class in `style.css`
6. Add test in `tests/package-feed.test.js`

### Running locally
```bash
cp .env.example .env  # Fill in values
npm install
npm start             # Starts on port 3000
npm test              # Run all tests
```
