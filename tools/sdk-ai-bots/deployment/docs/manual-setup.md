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
- Bash and `yq` v4 for environment loading, extension setup, and deployment checks
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

Choose the subscription, tenant, resource-group name, and locations for each of
`dev`, `preview`, and `prod`. Environments may share a subscription, but they
must have isolated resource groups and globally unique resource names.

Review the model names, versions, SKUs, and capacities in the agent Bicep layer
and the selected environment's `bicepOverrides`. Use the environment-specific
capacities, not only the layer defaults, when checking availability and quota
in each selected AI region:

```bash
az cognitiveservices usage list \
  --subscription <subscription-id> \
  --location <ai-region> \
  --output table
```

Request quota or choose another supported region before proceeding.

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

Populate the selected environment in
`deployment/infra/environments/environment-suite.yaml` according to the
[environment contract](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/environment-contract.md). Use `bicepOverrides` for
adopted resource names and set `candidateEnvironment` when chatbot evolution is
enabled.

Update `deployment/config/<env>/channel.yaml` and `tenant.yaml` for the same
Teams routes. The postprovision hook substitutes resource placeholders and
uploads these files to the `bot-configs` container. Do not hard-code a different
environment's backend endpoint.

```bash
npm run validate-env-suite -- --environment <env>
```

All `REPLACE_WITH_*` values for the selected environment must be resolved.

## 5. Create Federated Service Connections

Create an Azure Resource Manager workload-identity-federated service connection
for each environment. Define its name in
`pipelines/templates/service-connection.yml` and ensure it targets the
environment suite's `subscriptionId`.

The provisioning identity must be able to:

- create the target resource group for a new environment;
- create and update the resources declared by all six Bicep layers;
- create role assignments for workload and deployment identities;
- read/write the required App Configuration, Storage, Search, ACR,
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
candidate service connection.

Agent deployment reuses existing Foundry agent-write permissions. If a grant is
missing, the signed-in deployment identity must be authorized to assign Foundry
User at the target project scope, or an administrator must create that grant
before the deployment runs.

## 6. Register the Pipeline Definitions

Create these 5 definitions from their existing YAML paths and use the exact
names shown.

| Purpose | Pipeline name | YAML |
| --- | --- | --- |
| Application provision/deploy | `tools - sdk-ai-bots - deploy` | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` |
| Wiki CI | `tools - sdk-ai-bots-wiki-index - ci` | `azure-sdk-qa-bot-wiki-index/ci.yml` |
| Wiki build | `tools - sdk-ai-bots-wiki-index - build` | `azure-sdk-qa-bot-wiki-index/build_wiki.yml` |
| Hosted-agent deploy | `tools - sdk-ai-bots-hosted-agent - deploy` | `azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` |
| Feedback jobs | `tools - sdk-ai-bots-feedback-jobs` | `azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` |

Component CI remains in existing package-owned workflows and is not registered
from `deployment/pipelines/orchestrators`.

The application deployment definition accepts `component=all` (the default),
`shared-resources`, `agent`, `frontend`, `agent-server`, or `logic-app`. Use
`all` for the full environment. Selecting `agent` deploys the chat and Azure MCP
Server agents. Dev/prod `agent-server` deployments also update the scheduled
knowledge-sync WebJob; preview deploys only the server. `frontend` deploys the
frontend, while `shared-resources` and `logic-app` are provision-only selections.

Wiki generation, hosted-agent deployment, and feedback jobs use the
package-owned definitions listed in the [deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md).

## 7. Bootstrap Dev Layer State

The normal full-stack preflight refreshes existing layer state. Bootstrap a new
dev environment once from an authorized workstation. Before creating a preview
or production environment, provide an approved first-state bootstrap pipeline;
those environments disable local provision and deploy through
`localDeployAllowed`.

The command below is for dev only:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env new dev \
  --subscription <dev-subscription-id> \
  --location <dev-region> \
  --no-prompt
npm run sync-env-suite -- --environment dev
npm run validate-env-suite -- --environment dev
azd provision --environment dev --no-prompt
```

Provisioning creates the six-layer resource graph. The final postprovision
hook also:

- uploads source-controlled bot routing;
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

For local dev, authorize in the portal and reapply the workflow after
provisioning if consent was completed separately:

```bash
npm run deploy:logic-app -- --env dev
```

### Teams app publication

Bicep creates the Azure Bot and `MsTeamsChannel`. Verify both after the first
apply. Publish the generated Teams package to the tenant catalog and install it
in both configured Teams groups through an authorized Microsoft 365 session.
Pipeline deployments skip interactive Teams provisioning by default. When
enabled in an authenticated session, the frontend postdeploy hook can install
or upgrade an approved package.

### External credentials

- The GitHub App signing key is expected in the vault configured by
  `GITHUB_APP_KEYVAULT_URL` and `GITHUB_APP_KEY_NAME`; this deployment does not
  create it. The frontend identity selected by the WebJob's `BOT_CLIENT_ID`
  needs key `Sign` permission through a legacy access policy, or Key Vault
  Crypto User on an RBAC-enabled vault, for the remote signing API used by the
  frontend and WebJob.
- Authorize `tools - sdk-ai-bots - deploy` to use the
  `EMU-SSH-PRIVATE-KEY` secure file. The dev/prod agent-server path injects it
  into the App Service without placing the key in pipeline arguments or logs.
- Before the first WebJob deployment, disable and drain the previous dev/prod
  Azure DevOps knowledge-sync schedules to prevent concurrent writers.
- Do not create a Cosmos DB connection-string secret for the Logic App; its
  connection uses managed identity.

Storage soft delete and seven-day continuous Cosmos backup are provisioned. If
the recovery plan requires blob-version restoration, enable and test blob
versioning before production use.

### Existing-environment cutover

Keep a legacy Function App until both Logic Apps have successfully called
agent-server `/activity/convert`. After verification, remove obsolete Function
App resources and the `ado-token` secret through an approved cleanup change.
They are not runtime dependencies of this deployment.

## 9. Validate Readiness and Deploy

Complete the [operational readiness checklist](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md),
then follow the [deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md). The runbook owns routine
apply, application deployment, verification, and re-provisioning procedures.
