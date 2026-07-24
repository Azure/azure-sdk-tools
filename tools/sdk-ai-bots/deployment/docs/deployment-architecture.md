# Deployment Architecture

```mermaid
graph LR
    DEV[Developer] -->|PR| GH[GitHub: main]
    GH -->|trigger| CI[Component CI Pipeline<br/>build → test → ACR push]
    CI -->|publish tag| ACR[(Azure Container Registry)]

    GH -->|trigger / manual| ORCH[Orchestrator pipeline<br/>deploy-all-&lt;env&gt;.yml]
    ORCH -->|stage 1| PROV[Provision pipeline<br/>azd provision]
    PROV -->|reads| ES[environment-suite.yaml]
    PROV -->|applies| INFRA[main.bicep + modules]
    INFRA -->|creates| RG[(Resource Group)]

    ORCH -->|stage 2..N| CD[Component CD Pipelines<br/>frontend / backend / function-app / agent]
    CD -->|reads| ES
    CD -->|pulls tag| ACR
    CD -->|deploys to slot| SLOT[Staging slot]
    SLOT -->|smoke ok| SWAP[swap to production]
    SWAP -->|record| APPCFG[(App Configuration<br/>LastKnownGoodTag)]

    CD -.->|on failure| RB[Rollback template]
    RB -->|read previous tag| APPCFG
    RB -->|repoint| SLOT
```

## Layers

| #   | Layer             | Bicep module                                         | Provision via                                                   |
| --- | ----------------- | ---------------------------------------------------- | --------------------------------------------------------------- |
| 1   | shared-resources  | `modules/qaBotSharedResources/sharedResources.bicep` | `azd provision` (full graph) or `DEPLOY_LAYER=shared-resources` |
| 2   | agent-platform    | `modules/qaBotAgent/component.bicep`                 | `DEPLOY_LAYER=agent-platform`                                   |
| 3   | frontend identity | `modules/qaBotFrontend/userAssignedIdentity.bicep`   | always in full graph                                            |
| 4   | backend           | `modules/qaBotBackend/serverfarm.bicep`              | `DEPLOY_LAYER=backend`                                          |
| 5   | function-app      | `modules/qaBotFunctionApp/serverfarm.bicep`          | full graph                                                      |
| 6   | logic-app         | `modules/qaBotLogicApp/logicAppResources.bicep`      | `DEPLOY_LAYER=logic-app`                                        |

`azd provision` always runs the full Bicep graph via `main.bicep`. The
`DEPLOY_LAYER` env var only affects which layers `hooks/postprovision.ts`
re-applies via `az deployment group create` for partial-update workflows
(useful when only one Bicep module changed and a full `what-if` isn't
needed).

## Rollout pattern matrix

| Component      | Strategy                    | Slot(s)                    | Health gate                      |
| -------------- | --------------------------- | -------------------------- | -------------------------------- |
| frontend       | slot swap                   | `staging` → `production`   | `GET /health` 200                |
| backend        | slot swap                   | `authoring` → `production` | `GET /ping` 200                  |
| function-app   | slot swap                   | `staging` → `production`   | `GET /api/health` 200            |
| agent-server   | direct (slot IS production) | `agent` slot               | `GET /ping` with EasyAuth bearer |
| hosted agent   | revision swap (Foundry)     | n/a                        | Responses `/ping`                |
| logic-app      | re-apply ARM                | n/a                        | trigger probe                    |
| knowledge-sync | scheduled job               | n/a                        | Search doc-count regression      |
