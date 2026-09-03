# Rollback Runbook

> Deployments build remotely from source. Rollback restores a previous platform
> revision or reruns the orchestrator from a known-good source commit.

## When to roll back

| Signal                                                          | Action                              |
| --------------------------------------------------------------- | ----------------------------------- |
| `/health` or `/ping` returns non-200 for > 5 min after a deploy | Roll back the deployed component    |
| 5xx rate exceeds env-suite `minSuccessRate` budget over 5 min   | Roll back                           |
| p95 latency exceeds env-suite `latencyP95Ms` over 5 min         | Investigate; roll back if causal    |
| Cosmos / Search / Key Vault credential or schema error in logs  | Roll back; raise data-recovery flag |
| Operator request                                                | Roll back                           |

## Rollback paths per component

| Component      | Primary path                                                   | Manual fallback                                      |
| -------------- | -------------------------------------------------------------- | ---------------------------------------------------- |
| frontend       | swap back to the previous App Service slot or revision         | rerun its orchestrator from a known-good commit      |
| function-app   | swap back to the previous App Service slot or revision         | rerun its orchestrator from a known-good commit      |
| agent-server   | restore the previous App Service container revision            | rerun its orchestrator from a known-good commit      |
| hosted agent   | restore the previous Foundry hosted-agent revision             | open Foundry portal and revert the revision          |
| logic-app      | re-apply the previous Bicep revision from source control       | disable the workflow while recovering               |
| knowledge-sync | restore the previous Storage blob snapshot and rerun the sync  | rerun from the previous knowledge manifest           |

## Rollback procedure

1. Identify the last healthy platform revision and source commit from deployment
  history.
2. Prefer restoring the previous App Service slot/revision or Foundry revision.
3. If that revision is unavailable, check out the known-good commit and rerun
  the matching orchestrator. This performs a new remote build from that source.
4. Run smoke tests before restoring traffic.

## What blocks rollback

| Blocker                             | Mitigation                                                                                  |
| ----------------------------------- | ------------------------------------------------------------------------------------------- |
| Breaking Cosmos schema change       | use expand/contract migrations; if irreversible, restore from latest backup before rollback |
| Breaking Search index schema change | use index versioning (`azure-sdk-knowledge-v<n>`); switch alias instead of redefining       |
| Foundry model deprecated            | redeploy with previous model deployment tag; this is a config-only revert                   |
| Teams App manifest change           | manifest is auto-uploaded by CI; previous manifest can be re-applied via `teamsapp/update`  |
| Logic App connection re-consented   | re-run OAuth consent flow (cannot be automated)                                             |

## Post-rollback

1. File an incident in the team's tracking system.
2. Identify root cause; do not redeploy the failing source revision without a fix.
3. Update `docs/operational-readiness-checklist.md` if a new gate would have
   prevented the issue.
