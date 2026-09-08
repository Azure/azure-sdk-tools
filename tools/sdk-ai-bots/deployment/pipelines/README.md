# Pipelines — overview

```text
pipelines/
├─ templates/         ← reusable steps; all component pipelines compose these
└─ orchestrators/
    └─ qa-bot-deploy.yml  ← full-stack or component provision and deployment
```

## Composition

The application deployment pipeline selects either the complete deployment
sequence or one component sequence:

```yaml
parameters:
        - name: component
            default: all
            values: [all, shared-resources, agent, frontend, agent-server, function-app, logic-app]
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
applies only its matching infrastructure layer. Application selections then
deploy that component; `shared-resources` and `logic-app` stop after apply.
`qa-bot-deploy.yml` with `component=all` provisions every layer before remotely
building and deploying every application component.

| Layer/component                 | Orchestrator                                  | CI                                     |
| ------------------------------- | --------------------------------------------- | -------------------------------------- |
| resource-group + shared-resources | `qa-bot-deploy.yml` (`component=shared-resources`) | n/a                            |
| agent                            | `qa-bot-deploy.yml` (`component=agent`)       | `../../azure-sdk-qa-bot-agent/pipelines/server-ci.yml` |
| frontend                         | `qa-bot-deploy.yml` (`component=frontend`)    | existing package checks               |
| agent-server                     | `qa-bot-deploy.yml` (`component=agent-server`) | `../../azure-sdk-qa-bot-agent/pipelines/server-ci.yml` |
| function-app                     | `qa-bot-deploy.yml` (`component=function-app`) | existing package checks              |
| logic-app                        | `qa-bot-deploy.yml` (`component=logic-app`)   | n/a                                    |
| knowledge-sync                   | `../../azure-sdk-qa-bot-knowledge-sync/sync_knowledge.yml` (scheduled) | `../../azure-sdk-qa-bot-knowledge-sync/ci.yml` |
| generated wiki                   | `../../azure-sdk-qa-bot-wiki-index/build_wiki.yml` (scheduled) | `../../azure-sdk-qa-bot-wiki-index/ci.yml` |
| chatbot evolution agent          | `qa-bot-deploy.yml` with `component=all` for prod or `../../azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` | `../../azure-sdk-qa-bot-agent/pipelines/server-ci.yml` |
| feedback/evolution loop          | `../../azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` (scheduled) | `../../azure-sdk-qa-bot-agent/pipelines/server-ci.yml` |

Knowledge sync and wiki generation are data jobs, not long-running `azd`
services. Their existing pipelines remain outside the deployment orchestrator
set and trigger the corresponding Search indexers. The feedback job does not
queue knowledge sync.

## Existing pipelines (phase 1 coexistence)

The following pipelines are **not** removed by this transformation. Cut them
over to the new structure in phase 2, after dev has been validated:

- `tools/sdk-ai-bots/azure-sdk-qa-bot-agent/pipelines/{server-ci,server-cd,logicapp-cd}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot-knowledge-sync/{ci,sync_knowledge}.yml`
- `tools/sdk-ai-bots/azure-sdk-qa-bot/teamsapp.yml` (Teams manifest publish only — the ARM step is replaced by the consolidated orchestrator)
- `tools/sdk-ai-bots/{online,offline}-evaluation.yml` (evaluation framework; not in scope)
