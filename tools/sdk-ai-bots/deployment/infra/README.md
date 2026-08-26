# `infra/` — sdk-ai-bots deployment infrastructure

This folder owns the Bicep source of truth for every Azure resource in the
sdk-ai-bots system. Pipelines and `azd` consume the entry points under
`layers/`; nothing else should mutate these resources.

## Layout

```text
infra/
├─ layers/                   ← independently provisionable azd layers
│  ├─ resource-group/
│  ├─ shared-resources/
│  ├─ agent/
│  ├─ frontend/
│  ├─ agent-server/
│  ├─ function-app/
│  └─ logic-app/
└─ environments/
   └─ environment-suite.yaml  ← metadata + fixed Bicep overrides
```

## Apply

```bash
# Local (dev only)
azd provision --environment dev --no-prompt

# Iterate on one layer without reprovisioning its dependencies
azd provision agent-server --environment dev --no-prompt

# Pipeline (any env) loads environment-suite.yaml, then provisions through azd.
# See deployment/pipelines/templates/provision-stage.yml.
```

## Preview (always run before prod apply)

`azd` previews one layer at a time. The drift script refreshes each layer's
deployed outputs, runs all seven previews in dependency order, and aggregates
risky operations. A full preview therefore requires an existing deployment;
for a first deployment or migration from the deleted monolithic entry point,
queue the environment's `provision-all-*` pipeline with `bootstrapLayers=true`.
Bootstrap mode previews and applies each layer in dependency order, stopping on
any Delete or Modify operation so the next preview receives real outputs.

```pwsh
pwsh ../scripts/detect-drift.ps1 -Environment prod
```

## Environment-suite

Every pipeline reads `environments/environment-suite.yaml` rather than
hard-coding subscription IDs, regions, or Teams routing. See
[`../docs/environment-contract.md`](../docs/environment-contract.md).
Each layer's `main.bicepparam` resolves values from the same environment.

## Independent teardown

All layers deploy into one resource group. Enable deployment stacks before
using `azd down <layer>` so one layer's teardown does not delete shared
resources through resource-group deletion behavior:

```bash
azd config set alpha.deployment.stacks on
```
