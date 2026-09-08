# Rollback Runbook

The deployment does not provide an automated slot or revision rollback. The
supported code recovery is to rerun `qa-bot-deploy.yml` with the affected
component from a known-good source revision, producing a new remote build and
reapplying its deployment hooks.

## 1. Decide and Contain

Rollback when a deployment causes sustained health, authentication, message
delivery, data integrity, or latency regression.

1. Stop further promotion and record the failed source revision and pipeline
   run.
2. Identify the affected component and last verified source revision.
3. For a dangerous Logic App path, disable the workflow while recovery is in
   progress.
4. Preserve logs, deployment output, App Insights traces, and relevant data-job
   manifests before making another change.

## 2. Redeploy a Known-good Revision

Queue `qa-bot-deploy.yml` at the last verified source revision and select the
affected component. Use `component=all` only when the failure crosses component
or infrastructure contracts.

| Failure area | Recovery path |
| --- | --- |
| Frontend | Run with `component=frontend`. Reapply the prior Teams package if its manifest changed. |
| Agent-server | Run with `component=agent-server`. It deploys directly to the production site. |
| Function App | Run with `component=function-app`; its postdeploy hook reapplies the Logic App definition. |
| Chat agent | Run with `component=agent` or use the hosted-agent pipeline, then verify the new active Foundry version. |
| Evolution agent | Run the production hosted-agent deployment from the known-good revision and verify primary/candidate RBAC. |
| Logic App | Run with `component=function-app` to reapply its workflow, or keep the workflow disabled during investigation. |

Review and approve the infrastructure preview even during rollback. A prior
source revision may also contain older Bicep; do not approve unintended
infrastructure reversions merely to restore application code.

For dev-only emergency testing, check out the known-good revision and run
`azd deploy <service> --environment dev`. Production recovery remains
pipeline-only.

## 3. Recover Data Jobs

Code redeployment does not automatically restore data.

- **Knowledge sync:** rerun from the last known-good source/input snapshot. If
   blob versioning was enabled before the incident, restore the affected blob
   versions first, then start the primary Search indexer.
- **Generated wiki:** clear only the generated wiki state when required, rebuild
   from the known-good corpus, then start the wiki Search indexer.
- **Search schema:** prefer creating a compatible replacement index and moving
   consumers after validation. Do not delete a populated production index as an
   emergency first step.
- **Cosmos DB:** use the configured continuous-backup restore procedure. Restore
   to a separate account and validate before redirecting consumers.
- **Candidate evolution:** queue
   `tools - sdk-ai-bots-knowledge-sync - provision-and-sync` with
   `environment=dev` and `provisionInfrastructure=false`, then verify it
   completes before resuming feedback processing. The caller needs permission to
   queue that definition.

Blob versioning is disabled by the current Bicep configuration. Do not claim
blob-version recovery unless it was enabled and tested before the incident.

## 4. Verify Recovery

Repeat every relevant check in the deploy runbook:

1. service health endpoints;
2. Teams message and Logic App activity path;
3. hosted-agent active versions and RBAC;
4. App Insights errors and dependencies;
5. Search document availability and indexer status;
6. source and deployed version recording.

Keep traffic or the workflow disabled until verification passes. Do not
redeploy the failed revision without a reviewed fix.

## 5. Follow Up

1. File an incident and identify the failure mechanism.
2. Record the recovery source revision, pipeline runs, and data actions.
3. Add a CI, readiness, or observability gate when it would have detected the
    problem earlier.
4. Update this runbook when the platform gains an automated rollback mechanism.
