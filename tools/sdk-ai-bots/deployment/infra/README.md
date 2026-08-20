# `infra/` — sdk-ai-bots deployment infrastructure

This folder owns the Bicep source of truth for every Azure resource in the
sdk-ai-bots system. Pipelines and `azd` both consume `main.bicep`; nothing
else should mutate these resources.

## Layout

```text
infra/
├─ main.bicep                ← subscription-scope orchestrator (deploys RG + 6 modules)
├─ main.bicepparam           ← shared azd-to-Bicep parameter adapter
├─ modules/                  ← per-layer Bicep
│  ├─ qaBotSharedResources/
│  ├─ qaBotAgent/
│  ├─ qaBotFrontend/
│  ├─ qaBotAgentServer/
│  ├─ qaBotFunctionApp/
│  └─ qaBotLogicApp/
└─ environments/
   └─ environment-suite.yaml  ← metadata + fixed Bicep overrides
```

## Apply

```bash
# Local (dev only)
azd provision --environment dev --no-prompt

# Pipeline (any env) loads environment-suite.yaml, then provisions through azd.
# See deployment/pipelines/templates/provision-stage.yml.
```

## Preview (always run before prod apply)

```pwsh
pwsh ../scripts/detect-drift.ps1 -Environment prod
```

## Environment-suite

Every pipeline reads `environments/environment-suite.yaml` rather than
hard-coding subscription IDs, regions, or Teams routing. See
[`../docs/environment-contract.md`](../docs/environment-contract.md).
Both `azd provision --preview` and `azd provision` resolve those values through
`main.bicepparam`.
