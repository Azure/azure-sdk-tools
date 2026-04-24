# APIView — Operations Guide

This document covers deployment, test environments, configuration, and troubleshooting for the APIView engineering team. For contributor setup, see [CONTRIBUTING.md](../CONTRIBUTING.md). For architecture, see [overview.md](overview.md).

---

## Service Overview

| | Production | Staging |
|---|---|---|
| **Website** | [Prod](https://apiview.dev) | [Staging](https://apiviewstagingtest.com) |
| **Source Code** | [azure-sdk-tools/src/dotnet/APIView](https://github.com/Azure/azure-sdk-tools/tree/main/src/dotnet/APIView) | |

> **Staging terminology:** Two distinct "staging" concepts are used in this document. `apiviewstagingtest.com` is a **shared staging environment** (its own dedicated deployment with its own database, used for testing). The **staging slot** (`apiview-staging.azurewebsites.net`) is an [Azure App Service deployment slot](https://learn.microsoft.com/azure/app-service/deploy-staging-slots) on the production App Service used to warm up new builds before swapping them to production.

---

## Deployment

APIView runs as an Azure App Service in the Azure SDK Engineering Systems subscription. For deployment steps, see [APIViewWeb/CONTRIBUTING.md](../APIViewWeb/CONTRIBUTING.md#deployment-to-production).

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

Approver permissions are managed at runtime via Cosmos DB through the `PermissionsManager`. Users are granted the approver role through group membership configuration stored in the Cosmos DB "Permissions" container.

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

## Troubleshooting

See [troubleshooting.md](troubleshooting.md) for the full FAQ covering access issues, upload failures, CI/revision questions, release blocking, and engineering team diagnostics.
