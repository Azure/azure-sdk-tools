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

Infrastructure can be provisioned centrally per environment by the
`pipelines/orchestrators/provision-all-{dev,preview,prod}.yml` pipelines or one
layer at a time through the component provision pipelines. Layer pipelines
default to `preview`; select `apply` when queuing to provision that layer.

| Layer/component  | Provision                       | CI                      | CD                                  |
| ---------------- | ------------------------------- | ----------------------- | ----------------------------------- |
| resource-group   | `resource-group.provision.yml`  | n/a                     | n/a                                 |
| shared-resources | `shared-resources.provision.yml`| n/a                     | n/a                                 |
| agent             | `agent.provision.yml`           | `agent.ci.yml`          | `agent.cd.yml`                      |
| frontend          | `frontend.provision.yml`        | `frontend.ci.yml`       | `frontend.cd.yml`                   |
| agent-server      | `agent-server.provision.yml`    | built by agent CI       | `agent-server.cd.yml`               |
| function-app      | `function-app.provision.yml`    | `function-app.ci.yml`   | `function-app.cd.yml`               |
| logic-app         | `logic-app.provision.yml`       | n/a                     | n/a                                 |
| knowledge-sync    | shared-resources                | `knowledge-sync.ci.yml` | `knowledge-sync.cd.yml` (scheduled) |

## Existing pipelines (phase 1 coexistence)

The following pipelines are **not** removed by this transformation. Cut them
over to the new structure in phase 2, after dev has been validated:

- `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/{server-ci,server-cd,agent-cd,logicapp-cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/{ci,sync_knowledge}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot/teamsapp.yml` (Teams manifest publish only — the ARM step is replaced by the centralized `provision-all-<env>` pipeline)
- `tools/sdk-ai-bots/{online,offline}-evaluation.yml` (evaluation framework; not in scope)
