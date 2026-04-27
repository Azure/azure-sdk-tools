# APIView — Contributor Guide

This guide helps contributors get up and running with APIView development. For architecture and internals, see [overview.md](docs/overview.md).

---

## Prerequisites

You must be part of the **Azure SDK GitHub organization** and have the following installed:

| Tool | Purpose |
|------|---------|
| Visual Studio | Backend development |
| VS Code or terminal | Frontend development |
| Node.js | Angular build tooling |
| Angular CLI | Frontend CLI (`npm install -g @angular/cli`) |

> You do *not* need to provision Azure resources.
> You do *not* need to modify or run language parsers (C#, Java, Python, etc.).

---

## Staging Environment Permissions

You need RBAC permissions on the following staging resources:

| Resource | Required Roles |
|----------|---------------|
| **Key Vault** (`apiviewstagingkv`) | Key Vault Secrets User; Access Policy with Get, List, Set for secrets |
| **Storage Account** (`apiviewstagingstorage`) | Storage Blob Data Contributor |
| **App Configuration** (`apiviewstagnkvconfig`) | App Configuration Data Reader |
| **Cosmos DB** (`apiviewstaging`) | DocumentDB Account Contributor; Cosmos DB SQL Built-in Data Contributor |

---

## Local Setup

Follow these four steps to get APIView running locally.

**Step 1 — Create a GitHub OAuth App**

1. Go to [GitHub > Settings > Developer Settings > OAuth Apps](https://github.com/settings/developers) and click **"New OAuth App"**
2. Set **App name** to `APIViewLocal`, **Homepage URL** and **Authorization callback URL** both to `http://localhost:5000`
3. Copy the **Client ID** and **Client Secret** for the next step

<br>

**Step 2 — Configure User Secrets**

Right-click `APIViewWeb` in Visual Studio > **Manage User Secrets**, then insert:

```json
{
  "AppConfigUrl": "https://<your-app-config>.azconfig.io",
  "Github:ClientId": "<YOUR_CLIENT_ID>",
  "Github:ClientSecret": "<YOUR_CLIENT_SECRET>",
  "APIVIew-Host-Url": "http://localhost:5000",
  "APIVIew-SPA-Host-Url": "https://localhost:4200"
}
```

Secrets and service connections load automatically via App Configuration and Key Vault.

> **Need the App Config URL?** Reach out to the APIView engineering team for the user secrets.

<br>

**Step 3 — Build and Run**

```bash
# Build frontend
cd src\dotnet\APIView\APIViewWeb\Client
npm install
npm run-script build

# (Optional) Run Angular dev server for hot reload
cd src\dotnet\APIView\ClientSPA
npm install
npm run-script build
npx ng serve --ssl
```

Start the backend by opening `APIViewWeb` in Visual Studio and pressing **F5**.

> Backend changes require a server restart. Angular hot reload works automatically.

<br>

**Step 4 — Verify**

1. Log in via GitHub OAuth
2. Confirm you see the staging review list
3. Upload a token file or language-specific package
4. Verify the review appears and renders correctly

> **Tip:** Use **Swagger UI** (`/swagger`) to explore and test backend endpoints.


## Making Changes

**Where to modify:**

| Area | When to Modify |
|------|----------------|
| `APIViewWeb/` | API logic, endpoints, or auth |
| `ClientSPA/` | UI components or views |
| `APIView/` | Shared functionality — token models, diff, rendering |

**How to test:**

| Area | Method |
|------|--------|
| Backend | Visual Studio debugger; Swagger UI for endpoints; Test Explorer for unit tests |
| Frontend | `ng serve` for live preview; `ng test` for unit tests |

**Test projects:**

| Project | Status | Notes |
|---------|--------|-------|
| `ApiViewUnitTests` | Active | Backend logic and functionality |
| Angular .spec.ts files | Active | Run with `ng test` |
| `APIViewIntegrationTests` | Obsolete | Required Cosmos/Storage emulators |
| `APIViewUITests` | Obsolete | Selenium-based; replaced by Angular test suite |

<br>

## Logs and Monitoring

Monitor production issues via [Application Insights](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/microsoft.insights/components/APIView/logs) in the Azure Portal.

**Useful Kusto queries:**

```kusto
-- Exceptions grouped by problem
exceptions
| summarize count() by problemId

-- Errors and warnings grouped by message
traces
| where severityLevel > 1
| summarize count() by message
```

For additional internal documentation, visit the [Internal Wiki](https://dev.azure.com/azure-sdk/internal/_wiki/wikis/internal.wiki/356/ApiView).
