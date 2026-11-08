# Deploy Runbook

Use this runbook for routine deployments after the one-time
[manual setup](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/manual-setup.md) is complete.

## 1. Select the Deployment Path

Use the deployment pipeline with `component=all` when infrastructure contracts
or multiple services change. Select one component for an isolated application
or infrastructure change. Agent-server deployments update only the server.

| Scope | Pipeline | `component` |
| --- | --- | --- |
| Full environment | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `all` |
| Agent platform, chat agent, and Azure MCP Server agent | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent` |
| Agent-server | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent-server` |
| Frontend | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `frontend` |
| Shared resources | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `shared-resources` |
| Logic App | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `logic-app` |

Production evolution-agent deployment is included in the full-stack path. Wiki
generation and feedback processing use their specialized scheduled pipelines.

| Related job | YAML definition |
| --- | --- |
| External knowledge-sync WebJob (dev/prod) | `azure-sdk-qa-bot-agent/pipelines/server-cd.yml`, `deploymentTarget=knowledge-sync` |
| Generated wiki | `azure-sdk-qa-bot-wiki-index/build_wiki.yml` |
| Hosted-agent deployment | `azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` |
| Feedback/evolution jobs | `azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` |

Knowledge-sync deployment is owned by that external backend CD definition, not
`qa-bot-deploy.yml`. Its variable groups, service connection, App Service
settings, SSH credential, schedule, and writer permissions must be configured
through the package-owned workflow. Package CI builds and tests only; Search
schema and indexer provisioning remain in this deployment. Existing external
jobs and Azure role assignments are not removed.

## 2. Pre-deployment Checks

1. Record the candidate source revision.
2. Confirm the relevant component build and test checks passed for that revision.
3. Confirm the target environment contains no unresolved placeholders.
4. Run `npm run validate-env-suite -- --environment <env>` for local verification.
5. For production, complete the
   [operational readiness checklist](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md).
6. Confirm the source revision is permitted by the target service connection's
   branch-control policy.
7. Verify that the selected resource group already exists and is readable by
   the deployment identity. The pipeline will not create it.
8. When migrating dev to the shared SDK/MCP listener, disable any
   retired standalone MCP workflow through an approved operator action.
   Incremental provisioning omits that workflow but does not delete or disable
   an already deployed resource.

## 3. Preview, Approve, and Apply

Queue the selected pipeline with `environment=dev`, `preview`, or `prod`. For
`qa-bot-deploy.yml`, also select `component`. Optionally set `imageTag` to use
one tag for every selected application image; keep the default `auto` to use
automatic tagging. The pipeline performs these stages:

1. Load the environment suite and authenticate with WIF.
2. Compile Bicep and validate the selected environment.
3. Run `azd provision --preview` for the complete graph or selected layer.
4. Publish the preview summary.
5. Pause for manual approval.
6. Apply with `azd provision` after approval.
7. Pause for Teams connection consent when the Logic App layer requires it.

Reject the run if the preview targets an unexpected subscription, resource
group, region, identity, or resource name, or if a Delete/Modify operation is
not understood.

The preview covers infrastructure changes and preprovision checks. It does not
preview postprovision writes to App Configuration, Search, or bot
routing, or postdeploy changes to Foundry and Teams. Review the selected
environment's reconciliation steps before approving apply.

Normal preview requires existing layer state. Use the manual setup procedure for
a new dev environment. A new preview or production environment enters the
normal deployment path after its approved first-state bootstrap pipeline has
completed.

## 4. Deploy Application Code

After apply, `qa-bot-deploy.yml` deploys the selected application service.
`shared-resources` and `logic-app` stop after provisioning. With `component=all`,
chat-agent deployment starts first. The Azure MCP Server agent follows the chat
agent, and agent-server waits for both hosted agents. The production-only
evolution agent follows the chat agent on an independent branch. Before
frontend deployment, the pipeline acquires an Easy
Auth token and retries agent-server `/ping`; a failed readiness probe stops the
rollout.

Each service uses an `azd` native remote build or the shared hosted-agent
deployment helper. Bicep owns both complete Logic App workflows.
Pipeline deployments normally set `AZD_SKIP_TEAMS_PROVISION=1`; complete Teams
catalog publication and group installation separately using the manual setup
procedure. The frontend postdeploy hook performs that work only when enabled
in an authenticated Microsoft 365 session.

The rollout deploys directly to each service's production target. Frontend
postdeploy also probes `/health`; release completion requires the full
verification sequence below.

The pipeline does not perform a slot swap or automatic rollback. Stop promotion
and use the rollback runbook if verification fails.

### Coordinated Feature Rollouts

Teams image input requires updated agent-server and frontend images, the
deployment-owned Logic App workflows, and Teams catalog publication and
installation of manifest version `1.0.5` (schema `1.25`, channel features
`tier1`). Use `component=all` for this rollout; a frontend-only deployment does
not update the workflows or backend. The Teams connector downloads up to four
Graph-hosted images, with a 5 MiB per-image limit. Download outputs and bot
request inputs are secured; bot requests do not use chunked transfer.
Non-image messages bypass downloads, and failed downloads retain the original
activity or successfully downloaded images.

PR-review skills are included in the hosted-agent image, while result decoding
and retrieval changes also require the matching backend image. Evolution
readiness retries and retained-history trace lookup require the backend and
production evolution-agent images; partial feedback-job failures are reported
as `SucceededWithIssues`, not a fully successful scan. Confirm the upstream knowledge configuration includes
Azure DevOps `areaPath`; the backend reads that configuration from GitHub with a
one-hour cache. Retain work-item permissions under the configured area.

## 5. Verify the Deployment

1. Confirm the pipeline used the expected source revision and environment.
2. Confirm frontend `/health` is healthy.
3. Call agent-server `/ping` with an Easy Auth token for the backend
   Application ID URI.
4. Confirm the Search index contains the expected externally ingested knowledge
   and wiki data.
5. Confirm the primary Logic App is enabled and its Teams connection is
   `Connected`. Dev uses one listener including MCP; preview and production
   each use a separate MCP listener. Preview observes the production channels
   through its own bot and backend. Verify activities from every configured group
   reach the agent-server `/activity/convert` endpoint.
6. Send a test Teams message through each newly changed route.
7. Confirm the hosted chat and Azure MCP Server agent versions are active. For
   production, also confirm the evolution-agent version and its
   primary/candidate RBAC.
8. Inspect Application Insights for new 4xx/5xx, authentication, dependency, or
   startup errors.
9. In production, confirm the daily feedback pipeline runs with
   `CHATBOT_EVOLUTION_AGENT_ENABLED=true` and its latest scan completed.
10. Record the source revision and resulting App Service or Foundry versions.

For dev branch routing, confirm the deployed version has `metadata.branchname`
matching the source branch, then send `[branch:<branch-name>] <question>`.
Preview and production must keep `ENABLE_AI_FOUNDRY_AGENT_ROUTING=false`.

For a knowledge or wiki run, also confirm the expected blobs were written and
the corresponding Search indexer start request was accepted.

For image-input rollout, send both a text-only message and a message with an
inline Teams image through each listener. Confirm the image reaches the bot
as a data URL without appearing in workflow history, and verify that a failed
image download does not prevent the message from reaching the bot.

## 6. Local Dev Deployment

Local azd commands update individual services in an already bootstrapped dev
environment. Confirm its hosted agents and external integrations are ready
before deploying consumers:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env select dev
npm run sync-env-suite -- --environment dev
npm run validate-env-suite -- --environment dev
azd provision --environment dev --no-prompt
azd deploy agent --environment dev --no-prompt
azd deploy agent-server --environment dev --no-prompt
azd deploy frontend --environment dev --no-prompt
```

These commands do not deploy the separate Azure MCP Server agent. The unified
pipeline owns that step. Use it for a complete chatbot deployment or follow the hosted-agent path
above for an isolated hosted-agent update. Preview and production use their
mapped pipelines and service connections.

## 7. Re-provisioning

After infrastructure apply, run the corresponding application deployment
stages and repeat post-deployment verification. This establishes the selected
application images. External data jobs are operated through their package-owned
workflows. If backend reprovisioning changes App Service settings used by an
external WebJob, restore those prerequisites through its owner workflow.

If verification fails, stop promotion and follow the
[rollback runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-rollback.md).
