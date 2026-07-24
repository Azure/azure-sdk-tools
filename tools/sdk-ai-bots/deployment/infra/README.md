# `infra/` — sdk-ai-bots deployment infrastructure

This folder owns the Bicep source of truth for every Azure resource in the
sdk-ai-bots system. Pipelines and `azd` both consume `main.bicep`; nothing
else should mutate these resources.

## Layout

```text
infra/
├─ main.bicep                ← subscription-scope orchestrator (deploys RG + 6 modules)
├─ main.bicepparam           ← default azd parameters (dev)
├─ modules/                  ← per-layer Bicep
│  ├─ qaBotSharedResources/
│  ├─ qaBotAgent/
│  ├─ qaBotFrontend/
│  ├─ qaBotBackend/
│  ├─ qaBotFunctionApp/
│  └─ qaBotLogicApp/
└─ environments/
   ├─ environment-suite.yaml  ← single source of truth for env metadata
   ├─ dev.parameters.json
   ├─ preview.parameters.json
   └─ prod.parameters.json
```

## Apply

```bash
# Local (dev only)
azd provision --environment dev --no-prompt

# Pipeline (any env)
az deployment sub create \
  --location westus2 \
  --template-file infra/main.bicep \
  --parameters @infra/environments/<env>.parameters.json \
  --name "sdk-ai-bots-<env>-$(date +%s)"
```

## What-if (always run before prod apply)

```bash
az deployment sub what-if \
  --location westus2 \
  --template-file infra/main.bicep \
  --parameters @infra/environments/prod.parameters.json
```

## Environment-suite

Every pipeline reads `environments/environment-suite.yaml` rather than
hard-coding subscription IDs or region lists. See
[`../docs/environment-contract.md`](../docs/environment-contract.md).
