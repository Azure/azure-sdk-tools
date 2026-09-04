# sdk-ai-bots Deployment

This folder is the **single source of truth** for deploying the sdk-ai-bots
chatbot system to Azure.

> **Status:** provisioning uses seven ordered azd layers. Each layer has its own
> Bicep entry point and receives the same environment values through
> `sync-env-suite.ps1`.

## Layout

```text
deployment/
├─ DEPLOYMENT_TRANSFORMATION.md  ← master plan, inventory, ordering, rollout/rollback
├─ azure.yaml                    ← top-level azd manifest
├─ package.json                  ← devDeps for hook scripts (tsx, typescript)
├─ infra/
│  ├─ layers/                    ← seven azd provisioning entry points
│  └─ environments/              ← single source of truth for env metadata
│     └─ environment-suite.yaml
├─ hooks/                        ← azd lifecycle hooks (ts)
├─ pipelines/
│  ├─ templates/                 ← reusable provision/CI/CD/rollout steps
│  └─ orchestrators/
│     ├─ qa-bot-all.yml          ← full-stack provision and deployment
│     └─ <component>/            ← component CI + provision/deploy pipelines
├─ scripts/                      ← validate / drift / smoke / rollback helpers
└─ docs/                         ← runbooks + environment-contract + readiness checklist
```

## Quick links

- [Master plan](DEPLOYMENT_TRANSFORMATION.md)
- [Manual setup guide](docs/manual-setup.md) — **start here for a new ADO project / subscription**
- [Dev deployment checklist](docs/dev-deployment-checklist.md)
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

# Or update one layer independently
azd provision agent-server --environment dev --no-prompt

# 4. Remotely build and deploy the application services
azd deploy frontend     --environment dev --no-prompt
azd deploy function-app --environment dev --no-prompt
azd deploy agent        --environment dev --no-prompt

# Native remote build publishes the hosted agent image during deploy:
azd deploy agent --environment dev --no-prompt
```

> `azd` 1.29.0 preserves and executes service-level hooks on `azure.ai.agent`.
> The agent uses native ACR remote builds, and its postdeploy hook reconciles
> runtime RBAC, environment variables, and Entra authorization.

For preview / prod, use the component pipelines under
`pipelines/orchestrators/<component>/<component>.yml`. Each pipeline refreshes
the selected layer's dependencies, previews and applies only that component's
infrastructure layer, then deploys the component. Configure approvals, branch
controls, and pipeline permissions on the corresponding service connection.
