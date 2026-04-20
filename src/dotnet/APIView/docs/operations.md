# APIView — Operations Guide

This document covers deployment, test environments, configuration, and troubleshooting for the APIView engineering team. For contributor setup, see [contributing.md](contributing.md). For architecture, see [overview.md](overview.md). For language parser updates and releases, see [parser-guide.md](parser-guide.md).

---

## Service Overview

| | Production | Staging |
|---|---|---|
| **Website** | [Prod](https://apiview.dev) | [Staging](https://apiviewstagingtest.com) |
| **Source Code** | [azure-sdk-tools/src/dotnet/APIView](https://github.com/Azure/azure-sdk-tools/tree/main/src/dotnet/APIView) | |

> **Staging terminology:** Two distinct "staging" concepts are used in this document. `apiviewstagingtest.com` is a **shared staging environment** (its own dedicated deployment with its own database, used for testing). The **staging slot** (`apiview-staging.azurewebsites.net`) is an [Azure App Service deployment slot](https://learn.microsoft.com/azure/app-service/deploy-staging-slots) on the production App Service used to warm up new builds before swapping them to production.

---

## Deployment

APIView runs as an Azure App Service in the Azure SDK Engineering Systems subscription. For full contributor-level deployment details, see [APIViewWeb/CONTRIBUTING.md](../APIViewWeb/CONTRIBUTING.md#deployment-to-production).

**Step 1 — Approve the Prod stage**

The [APIView Pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=1136) runs automatically when changes are merged to `main`. Find the completed build you want to deploy and approve the **Prod** stage (the final deployment step awaiting approval).

**Step 2 — Wait for pipeline completion**

After the pipeline completes, the code is deployed to the **staging slot** of the `APIView` Web App in Azure Portal.

**Step 3 — Swap deployment slots**

1. Go to the `APIView` Web App in Azure Portal
2. Click **Swap**
3. Ensure the source is `apiview-staging` and the target is `APIView`
4. Click **Start swap**

**Step 4 — Rollback (if needed)**

In case of a regression (and when there are no database changes), repeat Step 3 to swap back. This restores the previous version.

---

## Test Environment (UX)

A safe test environment with its own dedicated database, updated daily with production data. You can deploy freely provided no active testing is underway.

| Resource | Name |
|----------|------|
| **URI** | https://apiviewuxtest.com/ |
| **Cosmos DB** | `apiviewuitest` |
| **App Config** | `apiviewuikvconfig` |

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

1. Open the `apiviewappconfig` App Configuration store in Azure Portal
2. Edit the `CopilotReviewIsRequired` key:
   - Specific languages: `Python,JavaScript,TypeSpec,C#`
   - All languages: `*` or `true`
   - Disable: (empty) or `false`
3. Edit the **Sentinel** key — change to any new value (triggers reload)

> Changes take effect up to **5 minutes** after updating the Sentinel key.

**Language name values:** `Python`, `JavaScript`, `TypeSpec`, `C#`, `Java`, `Go`, `Swift`, `C++`, `C`

---

## Troubleshooting (Engineering Team)

> **User-facing errors:** http://aka.ms/azsdk/engsys/apireview/faq
> **Eng team troubleshooting:** [troubleshooting guide (eng sys)](https://dev.azure.com/azure-sdk/internal/_wiki/wikis/internal.wiki/429/Troubleshooting-guide-EngSys-team)

### APIView is not accessible

Possible causes: deployment in progress, Cosmos DB or Azure Storage Blob not accessible, or bad deployment.

**Step 1 — Check for in-progress deployment**

Check the status of the `APIView` App Service in Azure Portal. If it's not running, check the [release pipeline](https://dev.azure.com/azure-sdk/internal/_release?_a=releases&view=mine&definitionId=73) for any in-progress deployments. Wait for completion, then re-check.

**Step 2 — Check Application Insights**

The `APIView` Application Insights resource should surface errors if Cosmos DB is not accessible or if there's a system crash during startup. If Cosmos errors appear, check if Cosmos DB is accessible (this has happened during Cosmos DB outages). For startup exceptions, debug using a local instance.

### Can we override or disable release check?

Yes, but **only after consulting the release owner and architect** for each language — an override can lead to release of breaking changes or unapproved APIs.

To disable:
1. In the pipeline run, click **Variables** under Advanced options
2. Click **Add variables**
3. Set **Name** to `Skip.CreateApiReview` and **Value** to `true`
4. Click **Create**, then click back

### Python sandboxing review not generating

If a Python wheel upload stays at "being generated" for more than 5 minutes:

1. Check the [Python sandboxing pipeline](https://dev.azure.com/azure-sdk/internal/_build?definitionId=5102) for failures
2. Common causes: the uploaded wheel has import issues, or the DevOps pipeline queue is overloaded
3. Known limitation: pipeline failures are not reported back to the APIView UI
