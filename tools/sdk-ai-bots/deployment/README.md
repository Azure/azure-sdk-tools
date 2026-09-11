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
│  ├─ orchestrators/             ← unified deployment entry point
│  └─ templates/                 ← reusable authentication and deployment stages
├─ scripts/                      ← validation and operator utilities
└─ test/                         ← deployment contract tests
```

## Documentation

Each document has one responsibility.

| Document | Use it for |
| --- | --- |
| [Deployment architecture](docs/deployment-architecture.md) | Deployed topology, layer graph, service order, data flows, and release model. |
| [Environment contract](docs/environment-contract.md) | Environment-suite schema, azd values, and variable ownership. |
| [Identity and access](docs/identity-and-access.md) | Principals, authentication, RBAC, consent, and grant lifecycles. |
| [Manual setup](docs/manual-setup.md) | One-time environment, identity, service-connection, pipeline, and consent setup. |
| [Deployment maintainer guide](docs/maintainer-guide.md) | azd, Bicep, hook, pipeline, and image implementation mechanics. |
| [Operational readiness](docs/operational-readiness-checklist.md) | Production approval checklist. |
| [Deploy runbook](docs/runbook-deploy.md) | Routine pipeline and local dev deployment procedures. |
| [Rollback runbook](docs/runbook-rollback.md) | Known-good redeployment and data recovery procedures. |
| [Deployment journey](docs/azd-deployment-journey-report.md) | Historical evolution, upstream issues, and superseded approaches. |
| [Bot configuration](config/README.md) | Source-controlled Teams routing uploaded during provisioning. |

## Local Utilities

Run local deployment utilities from this directory through npm. Their CLI
options use kebab-case, and their behavior is covered by `npm test`.

| Command | Purpose |
| --- | --- |
| `npm run validate-env-suite -- --environment dev` | Validate one environment and its Teams routing. Omit `--environment` to validate all environments. |
| `npm run sync-env-suite -- --environment dev` | Synchronize the suite into the selected local azd environment. |
| `npm run smoke-test -- --component frontend --environment dev` | Probe a component endpoint; add `--resolve-only` to inspect the target without Azure calls. |
| `npm run detect-drift -- --environment dev` | Preview every Bicep layer and fail on Modify or Delete operations. |

`scripts/list-risky-preview-operations.mjs` intentionally remains plain
JavaScript because Azure DevOps invokes it with Node before package-local
TypeScript tooling is guaranteed. It is the shared preview parser used by the
pipeline and drift command, not a second implementation of drift rules.
