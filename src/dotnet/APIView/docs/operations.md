# APIView — Operations Guide

This document covers deployment, test environments, configuration, secrets, and troubleshooting for the APIView engineering team. For contributor setup, see [contributing.md](contributing.md). For architecture, see [overview.md](overview.md). For language parser updates and releases, see [parser-guide.md](parser-guide.md).

---

## Service Overview

| | Production | Staging |
|---|---|---|
| **Website** | [Prod](https://apiview.dev) | [Staging](https://staging.apiview.dev) |
| **Resources** | [Prod (Azure Portal)](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview) | [Staging (Azure Portal)](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview) |
| **Source Code** | [azure-sdk-tools/src/dotnet/APIView](https://github.com/Azure/azure-sdk-tools/tree/main/src/dotnet/APIView) | |

> **Staging terminology:** Two distinct "staging" concepts are used in this document. `staging.apiview.dev` is a **shared staging environment** (its own dedicated deployment with its own database, used for testing). The **staging slot** (`apiview-staging.azurewebsites.net`) is an [Azure App Service deployment slot](https://learn.microsoft.com/azure/app-service/deploy-staging-slots) on the production App Service used to warm up new builds before swapping them to production.

---

## Deployment

APIView runs as an Azure App Service in the Azure SDK Engineering Systems subscription.

**Step 1 — Deploy to staging slot**

APIView uses an [Azure DevOps release pipeline](https://dev.azure.com/azure-sdk/internal/_release?definitionId=60). A CI pipeline runs whenever APIView changes are merged to `main` and automatically creates a new release. The release pipeline requires approval before deploying.

The pipeline deploys to the **staging slot** (not the same as the apiview staging instance used for testing).

**Step 2 — Verify staging slot**

1. Go to the [APIView staging slot](https://apiview-staging.azurewebsites.net) and click **Browse** in the overview page
2. Download the `azure-template` package wheel from [PyPI](https://pypi.org/project/azure-template/)
3. Create a review using the **Create a review** button
4. If the review is created successfully, proceed to Step 3
5. If it fails, check [Application Insights](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/microsoft.insights/components/APIView/logs) for the staging slot to identify the failure

**Step 3 — Swap staging to production**

1. Go to the [staging slot in Azure Portal](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/Microsoft.Web/sites/apiview/slots/staging)
2. Click **Swap** in the overview page

---

## Test Environment (UX)

A safe test environment with its own dedicated database, updated daily with production data. You can deploy freely provided no active testing is underway.

| Resource | Link |
|----------|------|
| **URI** | https://apiviewuxtest.com/ |
| **Cosmos DB** | https://cosmos.azure.com/ (Subscription: Azure SDK Engineering System, Account: `apiviewuitest`) |
| **App Config** | [apiviewuikvconfig](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/Microsoft.AppConfiguration/configurationStores/apiviewuikvconfig) |

**How to deploy to the test environment:**

1. Run the [APIView pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=1136) manually
2. Set **Commit** to the full SHA of the latest commit from your branch or PR
3. Wait for the **Build and Test** stage to complete — it will run **Publish Test UI** automatically
4. Done — your changes are deployed

---

## Configuration

### Approvers

API revision approvers are the architects and deputy architects tracked in the GitHub team [azure-sdk-api-approvers](https://github.com/orgs/Azure/teams/azure-sdk-api-approvers).

Approver permissions are managed at runtime via Cosmos DB through the `PermissionsManager`. Users are granted the approver role through group membership configuration stored in the Cosmos DB "Permissions" container.

> **Note:** The GitHub team is not currently hooked up to APIView directly. The `APIVIEW_APPROVERS` environment variable is listed in the service configuration but the actual authorization checks use the Cosmos DB permissions system. For the current list of approvers by language, see [user-guide.md](user-guide.md#who-can-approve).

### Copilot Review Required

To configure which languages require Copilot Review:

1. Open [APIView App Configuration](https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/Microsoft.AppConfiguration/configurationStores/apiviewkvconfig)
2. Edit the `CopilotReviewIsRequired` key:
   - Specific languages: `Python,JavaScript,TypeSpec,C#`
   - All languages: `*` or `true`
   - Disable: (empty) or `false`
3. Edit the **Sentinel** key — change to any new value (triggers reload)

> Changes take effect up to **5 minutes** after updating the Sentinel key.

**Language name values:** `Python`, `JavaScript`, `TypeSpec`, `C#`, `Java`, `Go`, `Swift`, `C++`, `C`

---

## Environment Variables

| Name | Purpose |
|------|---------|
| `allowedList-bot-github-accounts` | |
| `APIVIEW_ApiKey` | |
| `APIVIEW_APPROVERS` | List of approved API reviewers (legacy — actual approver checks use Cosmos DB PermissionsManager) |
| `APIVIEW_BLOB__CONNECTIONSTRING` | Storage account connection string |
| `APIVIEW_COSMOS__CONNECTIONSTRING` | Cosmos account connection string |
| `APIVIEW_GITHUB__CLIENTID` | User login via GitHub OAuth |
| `APIVIEW_GITHUB__CLIENTSECRET` | User login via GitHub OAuth |
| `APPINSIGHTS_INSTRUMENTATIONKEY` | Authentication to App Insights |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Authentication to App Insights |
| `Azure-Devops-PAT` | Authentication to Azure DevOps |
| `github-access-token` | Authentication to GitHub as common app principal for PR management |
| `letsencrypt:ClientId` | |
| `letsencrypt:ClientSecret` | |
| `letsencrypt:ResourceGroupName` | |
| `letsencrypt:ServicePlanResourceGroupName` | |
| `letsencrypt:SiteSlot` | |
| `letsencrypt:SubscriptionId` | |
| `letsencrypt:Tenant` | |
| `letsencrypt:UseIPBasedSSL` | |

## Secrets

| Name | Content | Purpose |
|------|---------|---------|
| *(See Key Vault for current entries)* | | |

## Principals

| Name | Type | Configured via | Notes |
|------|------|----------------|-------|
| `azuresdk@microsoft.com` | AAD/ADO user | Environment variable | Azure SDK bot account |
| Per-user | GitHub user | OAuth authorization code flow | Browser-based APIView user |
| *(GitHub app)* | GitHub app | Environment variables | |

## Resources

| Type | Name | Principal | Purpose |
|------|------|-----------|---------|
| Cosmos account | `apiview` / `apiviewstaging` | Key in environment variable | |
| Storage Account | `apiviewstorage` / `apiviewstagingstorage` | Key in environment variable | |
| ADO Account | `azure-sdk` | `azuresdk@microsoft.com` | ADO API interaction |

---

## Troubleshooting (Engineering Team)

> **User-facing errors:** http://aka.ms/azsdk/engsys/apireview/faq
> **Eng team troubleshooting:** [troubleshooting guide (eng sys)](https://dev.azure.com/azure-sdk/internal/_wiki/wikis/internal.wiki/356/ApiView)

### APIView is not accessible

Possible causes: deployment in progress, Cosmos DB or Azure Storage Blob not accessible, or bad deployment.

**Step 1 — Check for in-progress deployment**

Check the status of the APIView web instance in Azure Portal. If it's not running, check the [release pipeline](https://dev.azure.com/azure-sdk/internal/_release?definitionId=60) for any in-progress deployments. Wait for completion, then re-check.

**Step 2 — Check Application Insights**

[Application Insights](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/a18897a6-7e44-457d-9260-f2854c0aca42/resourceGroups/apiview/providers/microsoft.insights/components/APIView/logs) should surface errors if Cosmos DB is not accessible or if there's a system crash during startup. If Cosmos errors appear, check if Cosmos DB is accessible (this has happened during Cosmos DB outages). For startup exceptions, debug using a local instance.

### Can we override or disable release check?

Yes, but **only after consulting the release owner and architect** for each language — an override can lead to release of breaking changes or unapproved APIs.

To disable:
1. In the pipeline run, click **Variables** under Advanced options
2. Click **Add variables**
3. Set **Name** to `Skip.CreateApiReview` and **Value** to `true`
4. Click **Create**, then click back

### Python sandboxing review not generating

If a Python wheel upload stays at "being generated" for more than 5 minutes:

1. Check the **tools - generate-Python-apireview** [pipeline](https://dev.azure.com/azure-sdk/internal/_build) for failures (search by pipeline name in Azure DevOps)

   > **Note:** Verify the exact pipeline definition ID before bookmarking a direct link.
2. Common causes: the uploaded wheel has import issues, or the DevOps pipeline queue is overloaded
3. Known limitation: pipeline failures are not reported back to the APIView UI
