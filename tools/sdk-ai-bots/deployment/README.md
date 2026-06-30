# sdk-ai-bots Deployment

This folder is the **single source of truth** for deploying the sdk-ai-bots
chatbot system to Azure.

> **Status:** scaffolding. No existing pipelines are removed or rerouted by
> this PR. See [DEPLOYMENT_TRANSFORMATION.md](DEPLOYMENT_TRANSFORMATION.md)
> for the full plan, including migration phases.

## Layout

```text
deployment/
├─ DEPLOYMENT_TRANSFORMATION.md  ← master plan, inventory, ordering, rollout/rollback
├─ azure.yaml                    ← top-level azd manifest
├─ package.json                  ← devDeps for hook scripts (tsx, typescript)
├─ infra/
│  ├─ main.bicep                 ← subscription-scope orchestrator
│  ├─ main.bicepparam            ← default azd parameters (dev)
│  ├─ modules/                   ← Bicep modules per layer
│  └─ environments/              ← single source of truth for env metadata
│     ├─ environment-suite.yaml
│     ├─ dev.parameters.json
│     ├─ preview.parameters.json
│     └─ prod.parameters.json
├─ hooks/                        ← azd lifecycle hooks (ts)
├─ pipelines/
│  ├─ templates/                 ← reusable provision/CI/CD/rollout steps
│  └─ orchestrators/             ← end-to-end rollout per env
├─ component-pipelines/          ← standardized per-component provision/CI/CD
│  ├─ frontend/, backend/, function-app/, agent/, knowledge-sync/
├─ scripts/                      ← validate / drift / smoke / rollback helpers
└─ docs/                         ← runbooks + environment-contract + readiness checklist
```

## Quick links

- [Master plan](DEPLOYMENT_TRANSFORMATION.md)
- [Manual setup guide](docs/manual-setup.md) — **start here for a new ADO project / subscription**
- [Environment-contract](docs/environment-contract.md)
- [Deploy runbook](docs/runbook-deploy.md)
- [Rollback runbook](docs/runbook-rollback.md)
- [Component dependency graph](docs/component-dependency-graph.md)
- [Operational readiness checklist](docs/operational-readiness-checklist.md)

## Get started (dev)

```bash
# 1. Validate the environment-suite (replace placeholders first)
pwsh ./scripts/validate-env-suite.ps1

# 2. Create the azd env and sync it from environment-suite.yaml
npm install
azd env new dev --location westus2 --no-prompt
pwsh ./scripts/sync-env-suite.ps1 -Environment dev

# 3. Provision dev
azd provision --environment dev --no-prompt

# 4. Deploy the application services (image must already be in ACR)
AZD_SKIP_IMAGE_BUILD=1 azd deploy frontend     --environment dev --no-prompt
AZD_SKIP_IMAGE_BUILD=1 azd deploy function-app --environment dev --no-prompt
AZD_SKIP_IMAGE_BUILD=1 azd deploy agent        --environment dev --no-prompt
```

For preview / prod, use the Azure DevOps pipelines under
`component-pipelines/<component>/<component>.cd.yml` (gated by the
`sdk-ai-bots-<env>` ADO environment).
