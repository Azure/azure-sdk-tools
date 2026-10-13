# Deploy Runbook

Use this runbook for routine deployments after the one-time
[manual setup](manual-setup.md) is complete.

## 1. Select the Deployment Path

Use the deployment pipeline with `component=all` when infrastructure contracts
or multiple services change. Select one component for an isolated application
or infrastructure change. Dev/prod agent-server deployments include the
scheduled knowledge-sync WebJob.

| Scope | Pipeline | `component` |
| --- | --- | --- |
| Full environment | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `all` |
| Agent platform and chat agent | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent` |
| Agent-server | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent-server` |
| Frontend | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `frontend` |
| Shared resources | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `shared-resources` |
| Logic App | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `logic-app` |

Production evolution-agent deployment is included in the full-stack path. Wiki
generation and feedback processing use their specialized scheduled pipelines.

| Related job | YAML definition |
| --- | --- |
| Knowledge sync fallback/manual workflow | `azure-sdk-qa-bot-knowledge-sync/sync_knowledge.yml` |
| Generated wiki | `azure-sdk-qa-bot-wiki-index/build_wiki.yml` |
| Hosted-agent deployment | `azure-sdk-qa-bot-agent/pipelines/agent-cd.yml` |
| Feedback/evolution jobs | `azure-sdk-qa-bot-agent/pipelines/feedback-job.yml` |

## 2. Pre-deployment Checks

1. Record the candidate source revision.
2. Confirm the relevant component build and test checks passed for that revision.
3. Confirm the target environment contains no unresolved placeholders.
4. Run `npm run validate-env-suite -- --environment <env>` for local verification.
5. For production, complete the
   [operational readiness checklist](operational-readiness-checklist.md).
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
agent-server path tests, packages, and deploys the `knowledge-sync` WebJob. The
frontend postdeploy hook synchronizes Teams values and installs or upgrades the
app in both configured Teams groups only when tenant catalog prerequisites are
satisfied.

The rollout deploys directly to each service's production target. Frontend
postdeploy also probes `/health`; release completion requires the full
verification sequence below.

For the first rollout of this architecture, keep the legacy Function App until
both Logic Apps have successfully called agent-server `/activity/convert`.
After verification, remove the obsolete Function App resources and `ado-token`
secret through an approved cleanup change; they are no longer deployment-owned
or runtime dependencies.

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
9. Record the source revision and resulting App Service or Foundry versions.

For a knowledge or wiki run, also confirm the expected blobs were written and
the corresponding Search indexer start request was accepted.

## 6. Local Dev Deployment

The environment contract enables local deployment for dev:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env select dev
npm run sync-env-suite -- --environment dev
npm run validate-env-suite -- --environment dev
azd provision --environment dev --no-prompt
azd deploy agent-server --environment dev --no-prompt
azd deploy agent --environment dev --no-prompt
azd deploy frontend --environment dev --no-prompt
```

The unified pipeline owns knowledge-sync WebJob packaging, SSH-key injection,
and deployment. Use it when validating the complete dev/prod deployment path.
Preview and production use their mapped pipelines and service connections.

## 7. Re-provisioning

After infrastructure apply, run the corresponding application deployment
stages and repeat post-deployment verification. This establishes the selected
images and the scheduled WebJob.

If verification fails, stop promotion and follow the
[rollback runbook](runbook-rollback.md).
