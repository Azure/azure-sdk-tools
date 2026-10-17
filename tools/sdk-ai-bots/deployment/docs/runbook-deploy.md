# Deploy Runbook

Use this runbook for routine deployments after the one-time
[manual setup](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/manual-setup.md) is complete.

## 1. Select the Deployment Path

Use the deployment pipeline with `component=all` when infrastructure contracts
or multiple services change. Select one component for an isolated application
or infrastructure change. Dev/prod agent-server deployments include the
scheduled knowledge-sync WebJob.

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
| Knowledge-sync-only fallback (dev/prod) | `azure-sdk-qa-bot-agent/pipelines/server-cd.yml`, `deploymentTarget=knowledge-sync` |
| Generated wiki | `azure-sdk-qa-bot-wiki-index/build_wiki.yml` |
| Hosted-agent deployment | `azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` |
| Feedback/evolution jobs | `azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` |

The knowledge-sync-only fallback requires the backend CD definition's existing
variable groups and service connection, plus previously configured App Service
settings and SSH key. It does not provision those prerequisites. Ensure no
other knowledge-sync schedule writes to the same environment concurrently.

## 2. Pre-deployment Checks

1. Record the candidate source revision.
2. Confirm the relevant component build and test checks passed for that revision.
3. Confirm the target environment contains no unresolved placeholders.
4. Run `npm run validate-env-suite -- --environment <env>` for local verification.
5. For production, complete the
   [operational readiness checklist](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md).
6. Confirm the source revision is permitted by the target service connection's
   branch-control policy.
7. Before the first dev/prod WebJob deployment, disable and drain the prior
   Azure DevOps knowledge-sync schedule and confirm the deployment pipeline can
   use the `EMU-SSH-PRIVATE-KEY` secure file.

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
preview postprovision writes to App Configuration, Key Vault, Search, or bot
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
frontend or knowledge-sync WebJob deployment, the pipeline acquires an Easy
Auth token and retries agent-server `/ping`; a failed readiness probe stops the
rollout.

Each service uses an `azd` native remote build or the shared hosted-agent
deployment helper. Bicep owns both complete Logic App workflows. The dev/prod
agent-server path tests, packages, and deploys the `knowledge-sync` WebJob.
Pipeline deployments normally set `AZD_SKIP_TEAMS_PROVISION=1`; complete Teams
catalog publication and group installation separately using the manual setup
procedure. The frontend postdeploy hook performs that work only when enabled
in an authenticated Microsoft 365 session.

The rollout deploys directly to each service's production target. Frontend
postdeploy also probes `/health`; release completion requires the full
verification sequence below.

The pipeline does not perform a slot swap or automatic rollback. Stop promotion
and use the rollback runbook if verification fails.

## 5. Verify the Deployment

1. Confirm the pipeline used the expected source revision and environment.
2. Confirm frontend `/health` is healthy.
3. Call agent-server `/ping` with an Easy Auth token for the backend
   Application ID URI.
4. In dev/prod, confirm the `knowledge-sync` WebJob has the expected schedule
   and its latest run completed successfully.
5. Confirm both Logic Apps are enabled, their shared Teams connection is
   `Connected`, and test activities from both Teams groups reach the
   agent-server `/activity/convert` endpoint.
6. Send a test Teams message through each newly changed route.
7. Confirm the hosted chat and Azure MCP Server agent versions are active. For
   production, also confirm the evolution-agent version and its
   primary/candidate RBAC.
8. Inspect Application Insights for new 4xx/5xx, authentication, dependency, or
   startup errors.
9. In production, confirm the daily feedback pipeline runs with
   `CHATBOT_EVOLUTION_AGENT_ENABLED=true` and its latest scan completed.
10. Record the source revision and resulting App Service or Foundry versions.

For a knowledge or wiki run, also confirm the expected blobs were written and
the corresponding Search indexer start request was accepted.

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

These commands do not deploy the separate Azure MCP Server agent or the
knowledge-sync WebJob. The unified pipeline owns those steps, including SSH-key
injection. Use it for a complete deployment or follow the hosted-agent path
above for an isolated hosted-agent update. Preview and production use their
mapped pipelines and service connections.

## 7. Re-provisioning

After infrastructure apply, run the corresponding application deployment
stages and repeat post-deployment verification. This establishes the selected
images and the scheduled WebJob.

If verification fails, stop promotion and follow the
[rollback runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-rollback.md).
