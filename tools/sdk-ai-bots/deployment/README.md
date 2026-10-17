# SDK AI Bots Deployment

This directory is the source of truth for provisioning, deploying, and
operating the SDK AI chatbot in Azure. The supported workflow uses `azd >=
1.32.0`, six ordered Bicep layers, Azure DevOps workload identity federation,
and lifecycle hooks for state that cannot be expressed safely in Bicep.

## Layout

```text
deployment/
├─ config/                       ← source-controlled Teams tenant/channel routing
├─ docs/                         ← setup, readiness, deploy, and rollback guides
├─ hooks/                        ← azd lifecycle reconciliation
├─ infra/
│  ├─ environments/              ← dev/preview/prod contract
│  └─ layers/                    ← six Bicep entry points
├─ pipelines/
│  ├─ orchestrators/             ← unified deployment entry point
│  └─ templates/                 ← reusable authentication and deployment stages
├─ scripts/                      ← validation and operator utilities
└─ test/                         ← deployment contract tests
```

## Documentation

Start with setup for a new environment or the deploy runbook for an existing
one. The references describe the configuration, permissions, and lifecycle
steps used by those procedures.

| Document | Use it for |
| --- | --- |
| [Manual setup](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/manual-setup.md) | One-time environment, identity, service-connection, pipeline, and consent setup. |
| [Environment contract](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/environment-contract.md) | Environment-suite schema, azd values, and variable ownership. |
| [Identity and access](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/identity-and-access.md) | Principals, authentication, RBAC, consent, and grant lifecycles. |
| [Deployment architecture](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/deployment-architecture.md) | Deployed topology, layer graph, service order, data flows, and release model. |
| [Deployment process reference](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/maintainer-guide.md) | Pipeline execution, lifecycle hooks, image selection, and operational boundaries. |
| [Operational readiness](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md) | Production approval checklist. |
| [Deploy runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-deploy.md) | Routine pipeline and local dev deployment procedures. |
| [Rollback runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-rollback.md) | Known-good redeployment and data recovery procedures. |
| [Bot configuration](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/config/README.md) | Source-controlled Teams routing uploaded during provisioning. |

## Local Utilities

Run local deployment utilities from this directory through npm after completing
the tooling setup. Their CLI options use kebab-case.

| Command | Purpose |
| --- | --- |
| `npm run validate-env-suite -- --environment dev` | Validate one environment and its Teams routing. Omit `--environment` to validate all environments. |
| `npm run sync-env-suite -- --environment dev` | Synchronize the suite into the selected local azd environment. |
| `npm run smoke-test -- --component frontend --environment dev` | Probe a component endpoint; add `--resolve-only` to inspect the target without Azure calls. |
| `npm run detect-drift -- --environment dev` | Preview every Bicep layer and fail on Modify or Delete operations. |
