# Pipelines — overview

```text
pipelines/
├─ templates/         ← reusable steps; all component pipelines compose these
└─ orchestrators/     ← full-stack rollout per env (Provision + 4 component CDs)
```

## Composition

Every component pipeline follows the same shape:

```yaml
extends: /eng/pipelines/templates/stages/1es-redirect.yml
parameters:
    stages:
        - stage: X
          jobs:
              - deployment: Y
                environment: 'sdk-ai-bots-<env>' # ADO env → approval gate
                steps:
                    - template: load-environment-suite.yml
                    - template: azd-auth.yml
                    - template: <do the thing>.yml # azd-provision | webapp-deploy | azd-deploy
                    - template: smoke-test.yml
                    - template: swap-slot.yml # if preview/prod
                    - template: notify.yml
                on:
                    failure:
                        steps:
                            - template: rollback.yml
                            - template: notify.yml
```

## Component → pipeline map

| Component      | Provision                                                         | CI                      | CD                                  |
| -------------- | ----------------------------------------------------------------- | ----------------------- | ----------------------------------- |
| frontend       | `component-pipelines/frontend/frontend.provision.yml`             | `frontend.ci.yml`       | `frontend.cd.yml`                   |
| backend        | `component-pipelines/backend/backend.provision.yml`               | `backend.ci.yml`        | `backend.cd.yml`                    |
| function-app   | `component-pipelines/function-app/function-app.provision.yml`     | `function-app.ci.yml`   | `function-app.cd.yml`               |
| agent          | `component-pipelines/agent/agent.provision.yml`                   | `agent.ci.yml`          | `agent.cd.yml`                      |
| knowledge-sync | `component-pipelines/knowledge-sync/knowledge-sync.provision.yml` | `knowledge-sync.ci.yml` | `knowledge-sync.cd.yml` (scheduled) |

## Existing pipelines (phase 1 coexistence)

The following pipelines are **not** removed by this transformation. Cut them
over to the new structure in phase 2, after dev has been validated:

- `tools/sdk-ai-bots/azure-sdk-qa-bot-backend/pipeline/{ci,cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/{server-ci,server-cd,agent-cd,logicapp-cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/{ci,sync_knowledge}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot/teamsapp.yml` (Teams manifest publish only — the ARM step is replaced by `frontend.provision.yml`)
- `tools/sdk-ai-bots/{online,offline}-evaluation.yml` (evaluation framework; not in scope)
