# Deploy Runbook

Use this runbook for routine deployments after the one-time
[manual setup](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/manual-setup.md) is complete.

## 1. Select the Deployment Path

Use the deployment pipeline with `component=all` when infrastructure contracts
or multiple services change. Select one component for an isolated application
or infrastructure change. Existing knowledge-sync jobs remain separate.

| Scope | Pipeline | `component` |
| --- | --- | --- |
| Full environment | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `all` |
| Agent platform and chat agent | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent` |
| Agent-server | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `agent-server` |
| Frontend | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `frontend` |
| Function App | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `function-app` |
| Shared resources | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `shared-resources` |
| Logic App | `deployment/pipelines/orchestrators/qa-bot-deploy.yml` | `logic-app` |

Production evolution-agent deployment is included in the full-stack path. Wiki
generation and feedback processing use their specialized scheduled pipelines.

## 2. Pre-deployment Checks

1. Record the candidate source revision.
2. Confirm the relevant component build and test checks passed for that revision.
3. Confirm the target environment contains no unresolved placeholders.
4. Run `validate-env-suite.ps1 -Environment <env>` for local verification.
5. For production, complete the
   [operational readiness checklist](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/operational-readiness-checklist.md).
6. Confirm the source revision is permitted by the target service connection's
   branch-control policy.

## 3. Preview, Approve, and Apply

Queue the selected pipeline with `environment=dev`, `preview`, or `prod`. For
`qa-bot-deploy.yml`, also select `component`. The pipeline performs these
stages:

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

A complete preview requires existing layer state. Use the bootstrap procedure
in the manual setup guide for a brand-new dev environment. Do not queue a new
preview or production environment until an approved first-state bootstrap path
has been implemented.

## 4. Deploy Application Code

After apply, `qa-bot-deploy.yml` deploys the selected application service.
`shared-resources` and `logic-app` stop after provisioning. With `component=all`,
the pipeline runs:

1. agent-server;
2. a 10-minute agent-server stabilization wait in production;
3. Function App;
4. chat agent;
5. production evolution agent and scoped RBAC;
6. frontend.

Each service uses an `azd` native remote build. The Function App postdeploy hook
then installs the final Logic App workflow. The frontend postdeploy hook
synchronizes Teams values and installs or upgrades an app version only when
tenant catalog prerequisites are satisfied.

The active orchestrators do not perform slot swaps or a blocking multi-service
smoke stage. Frontend postdeploy performs a non-fatal `/health` probe. Pipeline
success means remote build/deploy and hooks succeeded; complete the verification
below before declaring the release healthy.

## 5. Verify the Deployment

1. Confirm the pipeline used the expected source revision and environment.
2. Confirm frontend `/health` is healthy.
3. Call agent-server `/ping` with an Easy Auth token for the backend
   Application ID URI.
4. Confirm Function App `/api/health` and recent trigger executions are healthy.
5. Confirm the Logic App is enabled, its Teams connection is `Connected`, and a
   test activity reaches the Function App.
6. Send a test Teams message through each newly changed route.
7. Confirm the hosted chat agent version is active. For production, also confirm
   the evolution-agent version and its primary/candidate RBAC.
8. Inspect Application Insights for new 4xx/5xx, authentication, dependency, or
   startup errors.
9. Record the source revision and resulting App Service or Foundry versions.

For a knowledge or wiki run, also confirm the expected blobs were written and
the corresponding Search indexer start request was accepted.

## 6. Local Dev Deployment

Local deployment is supported for dev only:

```bash
cd tools/sdk-ai-bots/deployment
azd auth login
azd env select dev
pwsh ./scripts/sync-env-suite.ps1 -Environment dev
pwsh ./scripts/validate-env-suite.ps1 -Environment dev
azd provision --environment dev --no-prompt
azd deploy agent-server --environment dev --no-prompt
azd deploy function-app --environment dev --no-prompt
azd deploy agent --environment dev --no-prompt
azd deploy frontend --environment dev --no-prompt
```

Production guards reject local provision and deploy.

## 7. Re-provisioning

Never stop after re-provisioning an existing environment. Bicep can restore
placeholder image values and the empty Logic App shell. Run the corresponding
application deploy stages and repeat post-deployment verification.

If verification fails, stop promotion and follow the
[rollback runbook](https://github.com/Azure/azure-sdk-tools/blob/main/tools/sdk-ai-bots/deployment/docs/runbook-rollback.md).
