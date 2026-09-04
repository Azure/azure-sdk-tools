# Pipelines — overview

```text
pipelines/
├─ templates/         ← reusable steps; all component pipelines compose these
└─ orchestrators/
    ├─ qa-bot-all.yml  ← full-stack provision and deployment
    └─ <component>/    ← component CI + provision/deploy pipelines
```

## Composition

Every deployment orchestrator follows the same shape:

```yaml
extends: /eng/pipelines/templates/stages/1es-redirect.yml
parameters:
    stages:
        - template: provision-with-approval.yml
        - template: component-deploy-stage.yml
          parameters:
              dependsOn: Provision
```

Dev, preview, and production all require a successful infrastructure preview
and manual approval before apply. The approval task rejects after 24 hours; the
approval stage itself remains available for three days. When no explicit
approver list is configured, any user with permission to queue the pipeline can
approve or reject the run.

After an apply that includes the Logic App layer, the provision stage checks the
Teams API connection. Connected environments continue automatically; otherwise
the pipeline pauses with an Azure portal link for delegated consent and verifies
the connection before completing the stage. The later Function App postdeploy
hook installs the complete workflow definition and enables it.

## Component → pipeline map

Each component deployment refreshes its dependency layers, previews and applies
only its matching infrastructure layer, then deploys that component. The
environment-wide `qa-bot-all.yml` pipeline provisions every layer before
remotely building and deploying every component. Provision-only layer pipelines
remain available for focused changes and use the same preview, approval, and
apply sequence.

| Layer/component                 | Orchestrator                                  | CI                                     |
| ------------------------------- | --------------------------------------------- | -------------------------------------- |
| resource-group + shared-resources | `shared-resources/shared-resources.yml`    | n/a                                    |
| agent                            | `agent/agent.yml`                             | `agent/agent.ci.yml`                   |
| frontend                         | `frontend/frontend.yml`                       | `frontend/frontend.ci.yml`             |
| agent-server                     | `agent-server/agent-server.yml`               | built by agent CI                      |
| function-app                     | `function-app/function-app.yml`               | `function-app/function-app.ci.yml`     |
| logic-app                        | `logic-app/logic-app.yml`                     | n/a                                    |
| knowledge-sync                   | `knowledge-sync/knowledge-sync.yml` (scheduled) | `knowledge-sync/knowledge-sync.ci.yml` |

## Existing pipelines (phase 1 coexistence)

The following pipelines are **not** removed by this transformation. Cut them
over to the new structure in phase 2, after dev has been validated:

- `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/{server-ci,server-cd,agent-cd,logicapp-cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/{ci,sync_knowledge}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot/teamsapp.yml` (Teams manifest publish only — the ARM step is replaced by the consolidated orchestrator)
- `tools/sdk-ai-bots/{online,offline}-evaluation.yml` (evaluation framework; not in scope)
