# Manual Setup Guide

Use this guide once for each new deployment environment. Complete the sections
in order. Routine deployments use the [deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md).

## 1. Install Local Prerequisites

Local tooling is needed only for dev bootstrap or maintenance from a
workstation. Azure DevOps agents install their own dependencies.

- Azure CLI 2.60 or later
- Azure Developer CLI (`azd`) 1.32.0 or later
- Bicep CLI 0.30 or later (`az bicep install`)
- Node.js 20 or later and npm
- PowerShell 7 or later
- `yq` v4
- Docker only for optional local image builds; pipelines build remotely in ACR

Install and validate the deployment tooling:

```bash
cd tools/sdk-ai-bots/deployment
npm ci
bash ./scripts/install-azd-extensions.sh
az --version
azd version
```

## 2. Choose Azure Targets and Check Quota

Choose the subscription, tenant, resource-group name, and regions for each of
`dev`, `preview`, and `prod`. Environments may share a subscription, but they
must have isolated resource groups and globally unique resource names.

The agent layer deploys these models serially:

- `gpt-4.1` version `2025-04-14`, capacity 1
- `gpt-5.6-sol` version `2026-07-09`, capacity 500
- `gpt-5.1` version `2025-11-13`, capacity 1
- `gpt-5-mini` version `2025-08-07`, capacity 1
- `text-embedding-3-small` version `1`, capacity 1

Check availability and quota in each selected AI region before provisioning:

```bash
az cognitiveservices usage list \
  --subscription <subscription-id> \
  --location <ai-region> \
  --output table
```

Request quota or choose another supported region before proceeding. Model child
deployments are intentionally serialized because concurrent writes to one AI
Services account can return `RequestConflict`.

## 3. Bootstrap the Backend Entra Application

The agent-server Easy Auth application is external deployment input. Create one
per environment from an identity authorized to create applications in the
target tenant:

```bash
cd tools/sdk-ai-bots/deployment
npm run create-entra-app -- \
  --display-name azuresdkqabot-server-<env> \
  --application-id-uri "api://<tenant-id>/azure-sdk-qa-bot-<env>" \
  --tenant-id <tenant-id> \
  --service-management-reference <reference>
```

The script exposes delegated and application permissions and preauthorizes
Azure CLI. It prints `serverApplicationClientId` and
`serverApplicationIdUri`; record both for the environment suite. Run with
`--dry-run` first when modifying an existing registration.

This backend application is separate from the Azure Bot identity. Azure Bot
uses the frontend user-assigned managed identity directly. The supported
deployment keeps Azure resources and Teams in the same Entra tenant and does
not create a multitenant bot or client secret.

## 4. Define the Environment and Bot Routing

Edit `deployment/infra/environments/environment-suite.yaml`. For the selected
environment, set:

- service-connection alias, subscription ID, and tenant ID;
- backend application client ID and Application ID URI from step 3;
- resource group, region, AI region, and Cosmos DB region;
- globally unique Key Vault, App Configuration, ACR, and site names;
- Teams group and channel IDs;
- approval, local-deploy, and production-pipeline policies;
- existing resource names under `bicepOverrides` when adopting resources;
- `candidateEnvironment` when chatbot evolution is enabled.

Update `deployment/config/<env>/channel.yaml` and `tenant.yaml` for the same
Teams routes. The postprovision hook substitutes resource placeholders and
uploads these files to the `bot-configs` container. Do not hard-code a different
environment's backend endpoint.

Read the [environment contract](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/environment-contract.md), then validate:

```pwsh
pwsh ./scripts/validate-env-suite.ps1 -Environment <env>
```

All `REPLACE_WITH_*` values for the selected environment must be resolved.

## 5. Create Federated Service Connections

Create an Azure Resource Manager workload-identity-federated service connection
for each environment. Its name must match both the environment suite's
`subscription` value and `pipelines/templates/service-connection.yml`.

The provisioning identity must be able to:

- create the target resource group for a new environment;
- create and update the resources declared by all seven Bicep layers;
- create role assignments for workload and deployment identities;
- read/write the required App Configuration, Key Vault, Storage, Search, ACR,
  Cosmos DB, and AI Services control/data planes.

For a new environment, resource-group creation requires subscription-scope
permission during bootstrap. After the resource group exists, narrow the
connection to the target scope where organizational policy permits. Role
assignment creation requires `Microsoft.Authorization/roleAssignments/write`,
typically supplied by User Access Administrator or Role Based Access Control
Administrator at the assignment scope.

Authorize only the intended pipeline definitions. Add service-connection
approval and branch-control checks for preview and production. The pipelines
also include their own preview-to-apply manual gate.

The production evolution and feedback workflows also need the configured
candidate service connection. Grant the feedback pipeline's build identity
**Queue builds** permission on the knowledge-sync definition.

## 6. Register the Pipeline Definitions

Create these 16 definitions from their existing YAML paths and use the exact
names shown.

| Purpose | Pipeline name | YAML |
| --- | --- | --- |
| Frontend CI | `tools - sdk-ai-bots-frontend - ci` | `deployment/pipelines/orchestrators/frontend/frontend.ci.yml` |
| Function CI | `tools - sdk-ai-bots-function-app - ci` | `deployment/pipelines/orchestrators/function-app/function-app.ci.yml` |
| Agent and agent-server CI | `tools - sdk-ai-bots-agent - ci` | `deployment/pipelines/orchestrators/agent/agent.ci.yml` |
| Knowledge-sync CI | `tools - sdk-ai-bots-knowledge-sync - ci` | `deployment/pipelines/orchestrators/knowledge-sync/knowledge-sync.ci.yml` |
| Frontend provision/deploy | `tools - sdk-ai-bots-frontend - provision-and-deploy` | `deployment/pipelines/orchestrators/frontend/frontend.yml` |
| Agent-server provision/deploy | `tools - sdk-ai-bots-agent-server - provision-and-deploy` | `deployment/pipelines/orchestrators/agent-server/agent-server.yml` |
| Function provision/deploy | `tools - sdk-ai-bots-function-app - provision-and-deploy` | `deployment/pipelines/orchestrators/function-app/function-app.yml` |
| Agent provision/deploy | `tools - sdk-ai-bots-agent - provision-and-deploy` | `deployment/pipelines/orchestrators/agent/agent.yml` |
| Knowledge provision/sync | `tools - sdk-ai-bots-knowledge-sync - provision-and-sync` | `deployment/pipelines/orchestrators/knowledge-sync/knowledge-sync.yml` |
| Shared resources | `tools - sdk-ai-bots-shared-resources - provision` | `deployment/pipelines/orchestrators/shared-resources/shared-resources.yml` |
| Logic App | `tools - sdk-ai-bots-logic-app - provision` | `deployment/pipelines/orchestrators/logic-app/logic-app.yml` |
| Full stack | `tools - sdk-ai-bots - provision-and-deploy-all` | `deployment/pipelines/orchestrators/qa-bot-all.yml` |
| Wiki CI | `tools - sdk-ai-bots-wiki-index - ci` | `azure-sdk-qa-bot-wiki-index/ci.yml` |
| Wiki build | `tools - sdk-ai-bots-wiki-index - build` | `azure-sdk-qa-bot-wiki-index/build_wiki.yml` |
| Hosted-agent deploy | `tools - sdk-ai-bots-hosted-agent - deploy` | `azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` |
| Feedback jobs | `tools - sdk-ai-bots-feedback-jobs` | `azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` |

Authorize the knowledge-sync pipeline to use its declared resource repositories
on first run. Keep the exact knowledge-sync pipeline name because the feedback
job resolves it by name when restoring candidate data.

See the [pipeline reference](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/pipelines/README.md) for composition and ownership.

## 7. Bootstrap Dev Layer State

The normal full-stack preflight refreshes every layer before preview. A
brand-new environment has no state to refresh, so bootstrap it once from an
authorized workstation. This exception is supported for dev; production
remains pipeline-only.

Preview is also local-disabled. A brand-new preview or production environment
therefore has no supported first-state path in the current pipelines: full
preflight cannot refresh absent layers, while local bootstrap is prohibited.
Treat that as a deployment blocker and add a separately reviewed bootstrap
pipeline before creating either environment from scratch. Do not temporarily
relax `localDeployAllowed` or `prodDeployOnlyFromPipeline` to work around it.

The command below is for dev only:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env new dev \
  --subscription <dev-subscription-id> \
  --location <dev-region> \
  --no-prompt
pwsh ./scripts/sync-env-suite.ps1 -Environment dev
pwsh ./scripts/validate-env-suite.ps1 -Environment dev
azd provision --environment dev --no-prompt
```

Provisioning creates the seven-layer resource graph. The final postprovision
hook also:

- uploads source-controlled bot routing;
- retrieves and stores `AI-SEARCH-APIKEY` in the deployment Key Vault;
- creates or updates Search indexes, data sources, skillsets, indexers,
  knowledge source, and knowledge base;
- seeds runtime App Configuration values, including the web-fetch allow-list.

It does not deploy application images. Continue with the deploy runbook after
the first provision.

## 8. Complete Interactive and External Setup

### Teams managed-API consent

When the Logic App layer is provisioned through a pipeline, the run pauses if
the Teams connection is not authorized. Open the supplied Azure portal URL,
authorize as the Teams service account, save, and resume. The next job verifies
that the connection is `Connected`.

For local dev, authorize in the portal and apply the final workflow after the
Function App is deployed:

```bash
npm run deploy:logic-app -- --env dev
```

### Teams app publication

Bicep creates the Azure Bot and `MsTeamsChannel`. Verify both after the first
apply. Publish the generated Teams package to the tenant catalog once; later
approved versions can be installed or upgraded by the frontend postdeploy hook.

### External credentials and notifications

- The GitHub App private key is expected in the vault configured by
  `GITHUB_APP_KEYVAULT_URL` and `GITHUB_APP_KEY_NAME`. Ensure the runtime
  identities can read that existing secret; this deployment does not create it.
- `TeamsWebhookUrl` in the deployment Key Vault is optional. Add it only when
  Teams deployment notifications are required. A missing value skips
  notification without failing deployment.
- Do not create a Cosmos DB connection-string secret for the Logic App; its
  connection uses managed identity.

Storage soft-delete and seven-day continuous Cosmos backup are provisioned.
Blob versioning is currently disabled. Enable it manually before relying on
blob-version restoration as a recovery procedure.

## 9. Validate Readiness and Deploy

Complete the [operational readiness checklist](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md), then follow the [deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md).

## 10. Cut Over Existing Pipelines

Keep legacy component definitions during initial validation. After dev and
preview have run successfully and operators accept the new runbooks, disable
the superseded server, Logic App, knowledge-sync, and ARM deployment paths.
Keep evaluation pipelines separate; they are not part of this deployment.

## Re-provisioning Warning

Re-provisioning can reset application image settings and restore the Logic App
to its empty Bicep shell. After any apply, run the normal application deployment
stages so predeploy and postdeploy hooks restore the desired runtime state.

State that is re-seeded on every complete provision includes App Configuration,
Search objects, `AI-SEARCH-APIKEY`, and bot configuration blobs. Managed-API
OAuth consent persists, but verify it whenever connection resources change.
