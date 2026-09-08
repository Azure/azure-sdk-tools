# Pipelines — overview

```text
pipelines/
├─ templates/         ← reusable steps; all component pipelines compose these
└─ orchestrators/
    ├─ qa-bot-deploy.yml  ← full-stack or component provision and deployment
    ├─ <component>/       ← component CI pipelines
    └─ ...               ← specialized layer and data-job pipelines
```

## Composition

The application deployment pipeline selects either the complete deployment
sequence or one component sequence:

```yaml
parameters:
        - name: component
            default: all
            values: [all, agent-server, function-app, agent, frontend]
```

Dev, preview, and production all require a successful infrastructure preview
and manual approval before apply. The approval task rejects after 24 hours; the
approval stage itself remains available for three days. When no explicit
approver list is configured, any user with permission to queue the pipeline can
approve or reject the run. The approval dialog includes a concise preflight
summary and links to the full logs. Azure DevOps fixes the action labels as
`Resume` and `Reject`; the pipeline labels the task **Approve or reject** and
documents `Resume` as the approval action.

After an apply that includes the Logic App layer, the provision stage checks the
Teams API connection. Connected environments continue automatically; otherwise
the pipeline pauses with an Azure portal link for delegated consent and verifies
the connection before completing the stage. The later Function App postdeploy
hook installs the complete workflow definition and enables it.

## Component → pipeline map

Each non-`all` component selection refreshes its dependency layers, previews and
applies only its matching infrastructure layer, then deploys that component.
`qa-bot-deploy.yml` with `component=all` provisions every layer before remotely
building and deploying every component. Provision-only layer pipelines remain
available for focused changes and use the same preview, approval, and apply
sequence.

| Layer/component                 | Orchestrator                                  | CI                                     |
| ------------------------------- | --------------------------------------------- | -------------------------------------- |
| resource-group + shared-resources | `shared-resources/shared-resources.yml`    | n/a                                    |
| agent                            | `qa-bot-deploy.yml` (`component=agent`)       | `agent/agent.ci.yml`                   |
| frontend                         | `qa-bot-deploy.yml` (`component=frontend`)    | `frontend/frontend.ci.yml`             |
| agent-server                     | `qa-bot-deploy.yml` (`component=agent-server`) | built by agent CI                     |
| function-app                     | `qa-bot-deploy.yml` (`component=function-app`) | `function-app/function-app.ci.yml`    |
| logic-app                        | `logic-app/logic-app.yml`                     | n/a                                    |
| knowledge-sync                   | `knowledge-sync/knowledge-sync.yml` (scheduled) | `knowledge-sync/knowledge-sync.ci.yml` |
| generated wiki                   | `../../azure-sdk-qa-bot-wiki-index/build_wiki.yml` (scheduled) | `../../azure-sdk-qa-bot-wiki-index/ci.yml` |
| chatbot evolution agent          | `qa-bot-deploy.yml` with `component=all` for prod or `../../azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` | `agent/agent.ci.yml` |
| feedback/evolution loop          | `../../azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` (scheduled) | `agent/agent.ci.yml` |

Knowledge sync and wiki generation are data jobs, not long-running `azd`
services. Both load the environment suite, authenticate through the mapped WIF
service connection, and trigger their provisioned Search indexer. The feedback
job queues sync-only dev knowledge runs before and after candidate analysis.

## Existing pipelines (phase 1 coexistence)

The following pipelines are **not** removed by this transformation. Cut them
over to the new structure in phase 2, after dev has been validated:

- `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/{server-ci,server-cd,logicapp-cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/{ci,sync_knowledge}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot/teamsapp.yml` (Teams manifest publish only — the ARM step is replaced by the consolidated orchestrator)
- `tools/sdk-ai-bots/{online,offline}-evaluation.yml` (evaluation framework; not in scope)
