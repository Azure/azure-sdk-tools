# SDK AI Bots Deployment

This directory is the source of truth for provisioning, deploying, and
operating the SDK AI chatbot in Azure. The supported workflow uses `azd >=
1.32.0`, seven ordered Bicep layers, Azure DevOps workload identity federation,
and lifecycle hooks for state that cannot be expressed safely in Bicep.

## Layout

```text
deployment/
├─ config/                       ← source-controlled Teams tenant/channel routing
├─ docs/                         ← setup, readiness, deploy, and rollback guides
├─ hooks/                        ← azd lifecycle reconciliation
├─ infra/
│  ├─ environments/              ← dev/preview/prod contract
│  └─ layers/                    ← seven Bicep entry points
├─ pipelines/
│  ├─ orchestrators/             ← full-stack and component entry points
│  └─ templates/                 ← reusable authentication and deployment stages
├─ scripts/                      ← validation and operator utilities
└─ test/                         ← deployment contract tests
```

## Documentation Order

Follow these documents in order. Maintainer references can be read as needed.

| Step | Document | Use it for |
| --- | --- | --- |
| 1 | [Deployment architecture](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/deployment-architecture.md) | Understand services, infrastructure dependencies, deployment order, and scheduled data flows. |
| 2 | [Environment contract](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/environment-contract.md) | Understand environment values before configuring a deployment. |
| 3 | [Manual setup](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/manual-setup.md) | Prepare subscriptions, identities, service connections, pipelines, consent, secrets, and the first environment. |
| 4 | [Operational readiness](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md) | Verify production prerequisites before approval. |
| 5 | [Deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md) | Run routine component or full-stack deployments. |
| 6 | [Pipeline reference](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/pipelines/README.md) | Select and maintain CI, provisioning, deployment, and data-job pipelines. |
| 7 | [Rollback runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-rollback.md) | Recover by redeploying a known-good source revision and restoring data when needed. |

Maintainers should also use the [infrastructure reference](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/infra/README.md) and [bot configuration reference](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/config/README.md).

## Active Flow

1. Component CI builds and tests the changed service.
2. The pipeline loads `environment-suite.yaml` and authenticates with the
	 environment's federated service connection.
3. Preflight validates configuration and runs `azd provision --preview`.
4. An operator reviews the preview and approves or rejects the apply stage.
5. `azd provision` applies the complete graph or a selected layer and its
	 dependencies.
6. Full-stack deployment runs `agent-server`, `function-app`, `agent`, the
	 production-only evolution agent, and `frontend` in that order.
7. Hooks reconcile App Configuration, Key Vault data, Search resources, RBAC,
	 Teams configuration, hosted-agent settings, and the Logic App workflow.
8. Scheduled knowledge, wiki, and feedback jobs maintain the data plane.

## Important Boundaries

- Production deployment is pipeline-only. Local deployment is supported for
	`dev`; preview and production require the mapped service connections.
- Only dev currently has a supported first-state bootstrap. A brand-new preview
	or production environment needs a dedicated approved bootstrap pipeline before
	the normal layered preview can run; do not bypass this by enabling local
	deployment.
- Every pipeline provisioning path includes a preview and manual approval
	before apply. The documented first-dev bootstrap is the exception.
- The agent-server deploys directly to its production App Service. The active
	orchestrators do not perform slot swaps or a blocking multi-service smoke
	stage. The frontend postdeploy hook has a non-fatal `/health` probe; operators
	must still complete deployment verification.
- Re-provisioning can reset images and the Logic App shell. Run the matching
	deploy stages after provisioning so postdeploy hooks restore runtime state.
- Entra application bootstrap, Teams/managed-API delegated consent, first Teams
	catalog publication, and pipeline registration remain operator actions.
- Preview cannot be deployed until all `REPLACE_WITH_*` values are resolved.

## Local Dev Entry Point

After completing the one-time setup guide:

```bash
cd tools/sdk-ai-bots/deployment
npm ci
azd auth login
azd env select dev
pwsh ./scripts/sync-env-suite.ps1 -Environment dev
pwsh ./scripts/validate-env-suite.ps1 -Environment dev
azd provision --environment dev --no-prompt
azd deploy agent-server --environment dev --no-prompt
azd deploy function-app --environment dev --no-prompt
azd deploy agent --environment dev --no-prompt
azd deploy frontend --environment dev --no-prompt
```
