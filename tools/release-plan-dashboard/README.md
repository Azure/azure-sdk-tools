# Release Plan Dashboard

A single-page web dashboard for viewing Azure SDK Release Plan work items from Azure DevOps. It provides a real-time overview of release plans across all Azure SDK languages, grouped by status and plane type.

**Short link:** <https://aka.ms/azsdk/releaseplan-dashboard>

## Features

### Core

- **Live Azure DevOps integration** — queries Release Plan work items (In Progress, New, Not Started, and recently Finished) from the `azure-sdk` org's `Release` project
- **Management / Data Plane split** — release plans categorized by plane type, displayed in separate columns
- **Status grouping** — In Progress, Partially Released, New/Not Started, and Recently Finished sections
- **Auto-refresh** — data refreshes automatically every hour; "Last refreshed" timestamp shown in the header

### Release Plan Cards

- **Expandable detail cards** — click a release plan card to see full details:
  - Per-language SDK pull request status (PR checks, approvals, merge status)
  - API Spec PR link and status
  - Spec project / TypeSpec path
  - Release status, package version, and package feed link
  - Product details with Service Tree link
- **Current stage tracking** — each release plan shows its progression stage (API Spec In Progress → SDK To Be Generated → SDK Review In Progress → SDK Ready To Be Released, etc.)
- **Action required indicator** — shows who needs to act (Spec PR Reviewer, SDK PR Reviewer, or Service Team)
- **Duplicate detection** — identifies potentially duplicate release plans and annotates them
- **SDK type badges** — highlights Beta vs Stable releases

### SDK Details Table

- **Per-language rows** — Language, Package, SDK PR, PR Status, APIView, Release Status, Version, Package Link, Action Required
- **Package feed links** — direct links to NuGet, PyPI, npm, Maven Central, or GitHub (Go) with icons and labels; shown only for released packages
- **Released version display** — shows version from `ReleasedVersionFor<Language>` field; shows "Not available" when released but version field is empty
- **PR status labels** — Approved, Ready to merge, failed checks (with deduplication on lazy-load)
- **Action buttons** — contextual actions per language:
  - ⚡ **Generate SDK** — when no PR exists
  - 🔗 **Link PR** — when PR is closed (with guidance to find/link correct PR)
  - 🔧 **Fix Checks** — when PR has failing checks
  - 🚀 **Release** — when PR is merged but not yet released
  - ✅ **Merge PR** — when PR is approved and ready

### PM View

- **PM Action tab** — visible only to whitelisted PM users (configured via `RELEASE_PLAN_DASHBOARD_PM_USERS` env var)
- **Possible PM actions** only shown when expanding a release plan from the Action Required tab

### Search & Navigation

- **Search & filter** — filter by title, product name, owner, or release plan ID
- **URL parameters** — `?releasePlan=<id>` to view a single plan, `?filter=<keyword>` to pre-filter
- **Share** — share links to specific release plans

## Architecture

```
├── server.js              # Express entry point: env validation, session, OAuth, middleware
├── lib/
│   ├── auth.js            # GitHub App JWT minting (via Azure Key Vault), OAuth helpers
│   ├── cache.js           # Shared cache state and TTL constants
│   ├── devops-api.js      # Azure DevOps WIQL queries and work item mapping
│   ├── github-api.js      # GitHub API: PR status, details, batch fetching with retry
│   └── rate-limit.js      # Sliding-window rate limiter (per-user)
├── routes/
│   └── api.js             # All /api/* route handlers, cache refresh logic
├── public/
│   ├── index.html         # Single-page HTML shell
│   ├── app.js             # Client-side rendering, interaction, and action popups
│   └── style.css          # Dashboard styles
├── tests/                 # Vitest unit tests (161+ tests)
├── package.json
└── .env.example           # Template for environment variables
```

### Data Flow
1. **Startup:** Server validates env vars → mints GitHub App token via Key Vault → fetches release plans from ADO → caches data
2. **Serving:** `/api/release-plans` returns cached data; `/api/pr-details` fetches GitHub PR details on demand
3. **Token refresh:** GitHub App token re-minted every 50 minutes
4. **Cache refresh:** Release plans re-fetched every hour (server-side); client auto-refreshes every hour
5. **Authentication:** GitHub OAuth with org membership check (Microsoft or Azure)

### Security
- GitHub OAuth with org membership gating
- CSRF protection via origin/referer validation
- Session cookies with `httpOnly`, `secure`, `sameSite: lax`
- Rate limiting: 30 requests/minute per user on `/api/*` endpoints
- Input validation with allowlist-based WIQL query parameters
- Error responses sanitized (no stack traces in production)

## Prerequisites

- [Node.js](https://nodejs.org/) 22 or later
- A Managed Identity (or service principal) with read access to work items in the `azure-sdk` Azure DevOps organization
- A GitHub App configured for OAuth and token signing
- Azure Key Vault access for GitHub App JWT signing

## Environment Variables

All environment variables required by the application:

| Variable | Description | Required |
|---|---|---|
| `KEYVAULT_NAME` | Azure Key Vault name for GitHub App JWT signing | **Yes** |
| `KEYVAULT_KEY_NAME` | Key name in the vault used for signing | **Yes** |
| `GITHUB_APP_NUMERIC_ID` | GitHub App ID (numeric) for token minting | **Yes** |
| `GITHUB_INSTALL_OWNER` | GitHub organization where the App is installed | **Yes** |
| `GITHUB_APP_CLIENT_ID` | GitHub OAuth App client ID | **Yes** |
| `GITHUB_APP_CLIENT_SECRET` | GitHub OAuth App client secret | **Yes** |
| `GITHUB_PAT_RELEASE_PLAN` | GitHub PAT for PR enrichment (fallback if App token fails) | No |
| `SESSION_SECRET` | Express session secret (random generated if not set) | No |
| `NODE_ENV` | Set to `production` for secure cookies | No |
| `REDIRECT_URL` | OAuth redirect base URL (defaults to `https://releaseplan-dashboard.azurewebsites.net`) | No |
| `RELEASE_PLAN_DASHBOARD_PM_USERS` | Comma-separated GitHub logins with PM view access | No |
| `PORT` | HTTP port to listen on (default: 3000) | No |

The server will **exit with an error** if any required variable is missing.

## Setup

1. Install dependencies:

   ```bash
   npm install
   ```

2. Copy `.env.example` to `.env` and fill in values:

   ```bash
   cp .env.example .env
   # Edit .env with your values
   ```

3. Start the server:

   ```bash
   npm start
   ```

4. Open <http://localhost:3000> in your browser.

## Testing

The project uses [Vitest](https://vitest.dev/) for unit testing with 161+ test cases covering:
- Cache eviction and TTL logic
- Rate limiter (sliding window)
- Authentication helpers
- Azure DevOps API (WIQL queries, work item mapping, released version fields)
- GitHub API (PR status extraction, batch fetching, check run classification)
- API route handlers (caching, enrichment, error handling)
- Server integration (middleware, OAuth flow, static serving)
- Package feed URL generation (NuGet, PyPI, npm, Maven, Go)
- Action determination logic (generate, link-pr, fix-checks, release, merge)

### Running Tests

```bash
# Run all tests
npm test

# Run a specific test file
npx vitest run tests/package-feed.test.js

# Run tests with verbose output
npx vitest run --reporter=verbose

# Run tests matching a pattern
npx vitest run --testPathPattern="github"
```

### Test Files

| File | Coverage |
|---|---|
| `tests/cache.test.js` | Cache state, eviction, TTL |
| `tests/rate-limit.test.js` | Sliding window rate limiter |
| `tests/auth.test.js` | Token minting, OAuth, org check |
| `tests/devops-api.test.js` | WIQL queries, work item mapping |
| `tests/github-api.test.js` | PR status, details, retry logic |
| `tests/github-api-extended.test.js` | PR status extraction, batch operations |
| `tests/api-routes.test.js` | API route handlers, cache refresh |
| `tests/server.test.js` | Express middleware, OAuth, static files |
| `tests/package-feed.test.js` | Feed URLs, version display, actions |

## Authentication

GitHub OAuth authentication is **required**. Users must be public members of the **Microsoft** or **Azure** GitHub organization.

To set up a GitHub OAuth App:
1. Go to GitHub → Settings → Developer settings → OAuth Apps → New OAuth App
2. Set the Authorization callback URL to `https://<your-domain>/auth/github/callback`
3. Use the generated Client ID and Client Secret as environment variables

## Deployment

### Azure App Service

1. Build the deployable zip:
   ```bash
   # Creates deploy.zip with server.js, package files, lib/, routes/, and public/
   # Compress source files (App Service runs npm install automatically via Oryx)
   ```
2. Deploy to Azure App Service (Node.js 22+ runtime)
3. Set all required environment variables in App Service → Configuration → Application settings
4. The app runs `npm start` which launches `server.js`

### Any Node.js Host

1. Copy `server.js`, `package.json`, `package-lock.json`, `lib/`, `routes/`, and `public/` to the host
2. Run `npm install --omit=dev`
3. Set all required environment variables
4. Run `npm start`

## URL Parameters

| Parameter | Description | Example |
|---|---|---|
| `releasePlan` | Show a single release plan by ID | `?releasePlan=2171` |
| `filter` | Pre-fill the search box with a keyword | `?filter=storage` |

Both can be combined: `?releasePlan=2171&filter=storage`
