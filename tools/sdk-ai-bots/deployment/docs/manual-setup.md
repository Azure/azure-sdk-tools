# Manual Setup Guide

One-time setup steps to bring the `sdk-ai-bots` deployment environment from
zero to ready-to-deploy. Run through each section in order; later steps
assume earlier ones are complete.

> Anything that **can** be automated by Bicep / pipelines is already
> automated. This document covers only the steps that cannot.

---

## 0. Prerequisites (local workstation)

Needed only if you plan to provision dev from your laptop. Pipelines have
these pre-installed on Microsoft-hosted Linux agents.

- [ ] **Azure CLI** ≥ 2.60 (`az --version`)
- [ ] **Azure Developer CLI (azd)** ≥ 1.10 — `curl -fsSL https://aka.ms/install-azd.sh | bash`
- [ ] **Bicep CLI** ≥ 0.30 — `az bicep install`
- [ ] **Node.js** ≥ 20, with `npm`
- [ ] **PowerShell 7+** (for `scripts/*.ps1`)
- [ ] **Docker** (only required if you build images locally; pipelines use ACR build)
- [ ] **`yq`** (used by [load-environment-suite.yml](../pipelines/templates/load-environment-suite.yml); not required locally)

Install hook dev dependencies once:

```bash
cd tools/sdk-ai-bots/deployment
npm install
```

---

## 1. Azure subscriptions

- [ ] Confirm you have **three subscriptions** (or three logically separated
  RGs in one subscription if your org consolidates):
  - `azuresdkqabot-dev`
  - `azuresdkqabot-preview`
  - `azuresdkqabot-prod`
- [ ] Note each **subscription GUID** and the **tenant GUID** — you will paste
  them into the environment-suite in step 4.
- [ ] Verify regional **quota** for Cognitive Services in the deployment
  region (default `westus2`):

  ```bash
  az cognitiveservices usage list --location westus2 -o table
  ```

  Required model deployments: `gpt-4.1`, `gpt-4.1-mini`, `o4-mini`,
  `text-embedding-3-large`. Request quota increase if needed.

---

## 2. Azure DevOps — service connections

Create one **federated identity** service connection per environment.
Use Workload Identity Federation (do not create client-secret connections).

- [ ] `azuresdkqabot-dev` → dev subscription, scoped to `rg-azuresdkqabot-dev`
- [ ] `azuresdkqabot-preview` → preview subscription, scoped to `rg-azuresdkqabot-preview`
- [ ] `azuresdkqabot-prod` → prod subscription, scoped to `rg-azuresdkqabot-prod`

For each connection:

1. Grant `Contributor` on the target resource group only (not the
   subscription).
2. Grant `User Access Administrator` on the same RG (required by Bicep
   role assignments).
3. For prod, **deny** the service principal from being usable outside the
   pipeline (Azure DevOps → Project Settings → Service connections →
   Approvals & checks → "Pipeline permissions" → restrict to specific
   pipelines).

---

## 3. Azure DevOps — environments (approval gates)

Create three ADO Environments under your ADO project. Each
`deployment:` job in the pipelines targets `sdk-ai-bots-<env>` to enforce
approval.

- [ ] `sdk-ai-bots-dev` — no approvers needed
- [ ] `sdk-ai-bots-preview` — add ≥ 1 reviewer
- [ ] `sdk-ai-bots-prod` — add ≥ 2 reviewers; enable
  "Required reviewers" check; enable "Branch control" → only `main`

---

## 4. Fill in environment-suite placeholders

Edit [infra/environments/environment-suite.yaml](../infra/environments/environment-suite.yaml)
and replace every `REPLACE_WITH_*` value:

- [ ] `subscriptionId` for `dev`, `preview`, `prod`
- [ ] `tenantId` if your tenant is not the default Microsoft one
- [ ] `serverAudience` per env (AAD app registration GUID — see step 6)

Also fill in the parameter files:

- [ ] [dev.parameters.json](../infra/environments/dev.parameters.json)
- [ ] [preview.parameters.json](../infra/environments/preview.parameters.json)
- [ ] [prod.parameters.json](../infra/environments/prod.parameters.json) (most fields already final)

Validate:

```bash
pwsh ./scripts/validate-env-suite.ps1
```

---

## 5. Entra ID app registrations

The agent server uses Entra ID (EasyAuth) to authenticate Bot Service and
Logic App callers.

- [ ] Create one **AAD app registration** per environment:
  - `sdk-ai-bots-agent-server-dev`
  - `sdk-ai-bots-agent-server-preview`
  - `sdk-ai-bots-agent-server-prod` (or reuse the existing `899da762-...`
    already pinned in [prod.parameters.json](../infra/environments/prod.parameters.json))
- [ ] Note each app's **Application (client) ID** and paste it into the
  matching `serverAudience` in [environment-suite.yaml](../infra/environments/environment-suite.yaml)
  and `*.parameters.json`.
- [ ] No client secret needed — authentication is bearer-token via federated
  identity.

---

## 6. Register the 18 pipelines

In Azure DevOps → Pipelines → "New pipeline" → "Existing Azure Pipelines
YAML file", create the following. **Name them exactly** per the repo
convention (`tools - <tool-name> - <action>`).

### Component CI (5)

- [ ] `tools - sdk-ai-bots-frontend - ci` → [frontend.ci.yml](../component-pipelines/frontend/frontend.ci.yml)
- [ ] `tools - sdk-ai-bots-backend - ci` → [backend.ci.yml](../component-pipelines/backend/backend.ci.yml)
- [ ] `tools - sdk-ai-bots-function-app - ci` → [function-app.ci.yml](../component-pipelines/function-app/function-app.ci.yml)
- [ ] `tools - sdk-ai-bots-agent - ci` → [agent.ci.yml](../component-pipelines/agent/agent.ci.yml)
- [ ] `tools - sdk-ai-bots-knowledge-sync - ci` → [knowledge-sync.ci.yml](../component-pipelines/knowledge-sync/knowledge-sync.ci.yml)

### Component CD (5)

- [ ] `tools - sdk-ai-bots-frontend - cd` → [frontend.cd.yml](../component-pipelines/frontend/frontend.cd.yml)
- [ ] `tools - sdk-ai-bots-backend - cd` → [backend.cd.yml](../component-pipelines/backend/backend.cd.yml)
- [ ] `tools - sdk-ai-bots-function-app - cd` → [function-app.cd.yml](../component-pipelines/function-app/function-app.cd.yml)
- [ ] `tools - sdk-ai-bots-agent - cd` → [agent.cd.yml](../component-pipelines/agent/agent.cd.yml)
- [ ] `tools - sdk-ai-bots-knowledge-sync - cd` → [knowledge-sync.cd.yml](../component-pipelines/knowledge-sync/knowledge-sync.cd.yml) (scheduled)

### Component provision (5)

- [ ] `tools - sdk-ai-bots-frontend - provision` → [frontend.provision.yml](../component-pipelines/frontend/frontend.provision.yml)
- [ ] `tools - sdk-ai-bots-backend - provision` → [backend.provision.yml](../component-pipelines/backend/backend.provision.yml)
- [ ] `tools - sdk-ai-bots-function-app - provision` → [function-app.provision.yml](../component-pipelines/function-app/function-app.provision.yml)
- [ ] `tools - sdk-ai-bots-agent - provision` → [agent.provision.yml](../component-pipelines/agent/agent.provision.yml)
- [ ] `tools - sdk-ai-bots-knowledge-sync - provision` → [knowledge-sync.provision.yml](../component-pipelines/knowledge-sync/knowledge-sync.provision.yml)

### Orchestrators (3, optional)

- [ ] `tools - sdk-ai-bots - deploy-all-dev` → [deploy-all-dev.yml](../pipelines/orchestrators/deploy-all-dev.yml)
- [ ] `tools - sdk-ai-bots - deploy-all-preview` → [deploy-all-preview.yml](../pipelines/orchestrators/deploy-all-preview.yml)
- [ ] `tools - sdk-ai-bots - deploy-all-prod` → [deploy-all-prod.yml](../pipelines/orchestrators/deploy-all-prod.yml)

### Cross-repo authorization

For [knowledge-sync.cd.yml](../component-pipelines/knowledge-sync/knowledge-sync.cd.yml),
authorize the pipeline to use the three resource repositories on first run:

- [ ] `1ESPipelineTemplates/1ESPipelineTemplates`
- [ ] `internal/azure-sdk-docs-eng.ms`
- [ ] `internal/internal.wiki`

---

## 7. Repository hygiene

- [ ] Add owners to [.github/CODEOWNERS](../../../../.github/CODEOWNERS):

  ```text
  /tools/sdk-ai-bots/deployment/   @owner1 @owner2
  ```

- [ ] Update root [README.md](../../../../README.md) index table to mention
  `tools/sdk-ai-bots/deployment/` (per
  [.github/copilot-instructions.md](../../../../.github/copilot-instructions.md)).
- [ ] Configure **branch protection** on `main` for
  `tools/sdk-ai-bots/deployment/**`:
  - require CODEOWNER review
  - require ≥ 1 additional approver
  - require linear history

---

## 8. First provision (dev)

Run from your workstation, signed in to the dev subscription:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env new dev --location westus2 --subscription <dev-sub-guid> --no-prompt

# Sync subscription / RG / region / ACR names from environment-suite.yaml
# into the azd env (.azure/dev/.env). Run this any time the suite changes;
# preprovision will fail fast if local azd vars drift from the suite.
pwsh ./scripts/sync-env-suite.ps1 -Environment dev

azd provision --environment dev --no-prompt
```

The `postprovision` hook runs the infra-layer pipeline
([hooks/postprovision.ts](../hooks/postprovision.ts)). On success you will
have:

- Resource group `rg-azuresdkqabot-dev`
- ACR `azsdkqabotacrdev`
- Cosmos DB, Key Vault, App Configuration, Search, Storage, Log Analytics
- AI Services account + model deployments
- Backend App Service (+ `authoring`, `agent` slots)
- Function App (+ `staging` slot)
- Logic App (workflow disabled until step 10)

---

## 9. Seed Key Vault secrets

The Bicep modules create the Key Vault but cannot supply runtime secrets.
Seed them once per environment. Substitute `<env>` accordingly:

```bash
KV="azsdkqabot-kv-<env>"

# GitHub App private key (for the GitHub integration in the backend)
az keyvault secret set --vault-name "$KV" --name GitHubAppPrivateKey \
  --file <path>/github-app.pem

# Teams webhook URL (used by notify.yml template)
az keyvault secret set --vault-name "$KV" --name TeamsWebhookUrl \
  --value "https://outlook.office.com/webhook/..."

# Cosmos DB connection string (consumed by Logic App)
az keyvault secret set --vault-name "$KV" --name CosmosDbConnectionString \
  --value "$(az cosmosdb keys list --type connection-strings \
              --name <cosmos-name> --resource-group rg-azuresdkqabot-<env> \
              --query 'connectionStrings[0].connectionString' -o tsv)"
```

Full inventory is referenced in [hooks/postprovision.ts](../hooks/postprovision.ts)
under `seedKeyVaultSecrets()`.

---

## 10. Logic App — OAuth consent

The Logic App uses three managed-API connections that **cannot** be
authorized via Bicep. After provisioning, an operator with the relevant
tenant rights must complete OAuth consent:

- [ ] **Microsoft Teams** connection — sign in as the service account that
  will post on behalf of the bot
- [ ] **Azure Blob Storage** connection — sign in with an identity that has
  `Storage Blob Data Contributor` on the storage account
- [ ] **Azure Cosmos DB** connection — already uses managed identity; verify
  in the portal that the connection shows "Connected"

After consent:

```bash
az logic workflow update \
  --name azuresdkqabot-logicapp \
  --resource-group rg-azuresdkqabot-<env> \
  --state Enabled
```

---

## 11. App Configuration seed values

Populate the runtime config keys consumed by the backend. Stubbed today
in [hooks/postprovision.ts](../hooks/postprovision.ts) `updateAppConfiguration()`.

```bash
APPCFG="azsdkqabot-config-<env>"

az appconfig kv set --name "$APPCFG" --key BotSettings:TenantId        --value "<tenant-guid>" --yes
az appconfig kv set --name "$APPCFG" --key BotSettings:ClientId        --value "<MI-client-id>" --yes
az appconfig kv set --name "$APPCFG" --key AiServices:Endpoint         --value "https://<ai>.cognitiveservices.azure.com" --yes
az appconfig kv set --name "$APPCFG" --key Search:Endpoint             --value "https://<search>.search.windows.net" --yes
```

---

## 12. Bot Service — Teams channel

The Bicep wires the Bot Service registration, but enabling the Microsoft
Teams channel for production traffic still requires a one-time portal flip
on first setup:

- [ ] Azure portal → Bot Service `azsdkqabot-<env>` → Channels → "Microsoft
  Teams" → Apply → set scope to **Commercial**

---

## 13. Teams App — first-time publish

The Teams app manifest is built by [frontend.ci.yml](../component-pipelines/frontend/frontend.ci.yml)
as `appPackage.<env>.zip`. The first time it's installed in your tenant
you must publish it manually:

- [ ] Teams admin center → "Manage apps" → "Upload new app" → upload
  `appPackage.dev.zip` for dev tenant testing
- [ ] For production, use Teams admin center → "Teams apps" → "Setup
  policies" to push the app to the target user group

Subsequent updates flow through CI via `teamsapp/update`.

---

## 14. Storage — blob versioning

Required for knowledge-sync rollback ([runbook-rollback.md](runbook-rollback.md)).

- [ ] Portal → Storage account `azuresdkqabotstorage<env>` → Data protection
  → enable **Blob versioning** with 90-day retention.

---

## 15. Operational readiness

Before the first prod rollout, sign off
[operational-readiness-checklist.md](operational-readiness-checklist.md):

- [ ] DRI named per component
- [ ] On-call rotation includes this system
- [ ] Application Insights availability tests configured
- [ ] Alerts wired to action group
- [ ] `Deployment:<component>:LastKnownGoodTag` keys exist in prod App Config

---

## 16. Ephemeral PR environments (Azure Deployment Environments)

Optional — only if you enable per-PR ephemeral environments. This provisions
the ADE control plane that the [pr-ephemeral.yml](../pipelines/orchestrators/pr-ephemeral.yml)
and [pr-reaper.yml](../pipelines/orchestrators/pr-reaper.yml) pipelines depend on.

The pipelines and IaC are already in the repo; only the Dev Center control
plane and RBAC below must be created by hand (one-time).

### 16.1 Environment definition (already in repo)

- The ADE environment definition manifest is [infra/environment.yaml](../infra/environment.yaml).
- It runs the **RG-scoped** entry template [infra/ade/main.bicep](../infra/ade/main.bicep)
  (ADE owns the resource group; the subscription-scoped [infra/main.bicep](../infra/main.bicep)
  is used only by azd / `az deployment sub`).
- `ade/main.bicep` mirrors the layer wiring of `main.bicep`; keep them in sync.

### 16.2 Dedicated sandbox subscription

- [ ] Confirm a **sandbox subscription** for PR environments, isolated from
  dev/preview/prod so PR churn can't exhaust their quota.

### 16.3 Create the Dev Center control plane

```bash
RG=rg-devcenter; LOC=westus2
az group create -n $RG -l $LOC

# Dev Center + project
az devcenter admin devcenter create -n sdk-ai-bots-dc -g $RG -l $LOC --identity-type SystemAssigned
DC_ID=$(az devcenter admin devcenter show -n sdk-ai-bots-dc -g $RG --query id -o tsv)
az devcenter admin project create -n sdk-ai-bots --devcenter-id "$DC_ID" -g $RG -l $LOC

# Catalog pointing at deployment/infra (folder with environment.yaml)
az devcenter admin catalog create -n sdkaibots-catalog --project-name sdk-ai-bots -g $RG \
  --git-hub path="/tools/sdk-ai-bots/deployment/infra" branch="main" \
  uri="https://github.com/Azure/azure-sdk-tools.git"

# Environment type = the "pool", mapped to the sandbox subscription
az devcenter admin environment-type create -n pr-sandbox --devcenter-name sdk-ai-bots-dc -g $RG
az devcenter admin project-environment-type create -n pr-sandbox --project-name sdk-ai-bots -g $RG \
  --deployment-target-id "/subscriptions/<SANDBOX_SUBSCRIPTION_ID>" \
  --identity-type SystemAssigned --status Enabled \
  --roles '{"b24988ac-6180-42a0-ab88-20f7382dd24c":{}}'   # Contributor
```

### 16.4 RBAC for the pipeline identity

Grant the service connection's identity (used by [azd-auth.yml](../pipelines/templates/azd-auth.yml))
data-plane access on the ADE project:

- [ ] **Deployment Environments User** — for [pr-ephemeral.yml](../pipelines/orchestrators/pr-ephemeral.yml)
  (create/deploy PR environments).
- [ ] **DevCenter Project Admin** — for [pr-reaper.yml](../pipelines/orchestrators/pr-reaper.yml)
  (delete any environment in the project).

### 16.5 Pipeline variables

Set on the PR pipelines (or a shared variable group):

- [ ] `DEVCENTER_NAME` = `sdk-ai-bots-dc`
- [ ] `DEVCENTER_PROJECT` = `sdk-ai-bots`
- [ ] `ADE_CATALOG_NAME` = `sdkaibots-catalog`
- [ ] `ADE_ENV_DEFINITION` = `sdk-ai-bots`
- [ ] `ADO_ORG_URL`, `ADO_REPO_ID`, `TTL_HOURS` (reaper)
- [ ] `SERVER_AUDIENCE`, `TEAMS_GROUP_ID`, `TEAMS_CHANNEL_IDS`, and the CI image
  tags (`FRONTEND_IMAGE_TAG`, `FUNCTION_IMAGE_TAG`, `AGENT_IMAGE_TAG`)

### 16.6 Wire the trigger

- [ ] Register [pr-ephemeral.yml](../pipelines/orchestrators/pr-ephemeral.yml) as a
  **branch-policy build validation** on `main` (Azure Repos YAML PR triggers
  come from branch policy, not a `pr:` block).
- [ ] The reaper runs on its own hourly schedule — no trigger wiring needed.
- [ ] Rollback rehearsed in preview within last 90 days

---

## 16. Retire old pipelines (phase 2 cutover)

After the new pipelines are validated for at least one week in dev and
preview:

- [ ] Delete or disable [azure-sdk-qa-bot-backend/pipeline/ci.yml](../../azure-sdk-qa-bot-backend/pipeline/ci.yml) and [cd.yml](../../azure-sdk-qa-bot-backend/pipeline/cd.yml)
- [ ] Delete or disable [azure-sdk-qa-bot-agent/pipelines/server-ci.yml](../../azure-sdk-qa-bot-agent/pipelines/server-ci.yml), [server-cd.yml](../../azure-sdk-qa-bot-agent/pipelines/server-cd.yml), [agent-cd.yml](../../azure-sdk-qa-bot-agent/pipelines/agent-cd.yml), [logicapp-cd.yml](../../azure-sdk-qa-bot-agent/pipelines/logicapp-cd.yml)
- [ ] Delete or disable [azure-sdk-qa-bot-knowledge-sync/ci.yml](../../azure-sdk-qa-bot-knowledge-sync/ci.yml) and [sync_knowledge.yml](../../azure-sdk-qa-bot-knowledge-sync/sync_knowledge.yml)
- [ ] Edit [azure-sdk-qa-bot/teamsapp.yml](../../azure-sdk-qa-bot/teamsapp.yml) — remove the `arm/deploy` step (the unified Bicep now owns those resources); keep the manifest publish steps.

---

## Quick checklist (1-line summary)

| # | Step | One-time? |
|---|---|---|
| 0 | Install local CLIs | per-dev |
| 1 | 3 Azure subscriptions + region quota | per ADO project |
| 2 | 3 federated service connections | per ADO project |
| 3 | 3 ADO Environments with approvers | per ADO project |
| 4 | Replace `REPLACE_WITH_*` placeholders | per fork |
| 5 | 3 Entra app registrations (agent EasyAuth audience) | per ADO project |
| 6 | Register 18 pipelines + authorize cross-repo resources | per ADO project |
| 7 | CODEOWNERS, root README, branch protection | per fork |
| 8 | First `azd provision dev` | per env |
| 9 | Seed Key Vault secrets | per env |
| 10 | Logic App OAuth consent + enable workflow | per env |
| 11 | App Configuration seed values | per env |
| 12 | Bot Service Teams channel toggle | per env |
| 13 | First Teams App publish | per env |
| 14 | Storage blob versioning | per env |
| 15 | Operational readiness sign-off | before prod |
| 16 | Retire old pipelines | once after cutover |
