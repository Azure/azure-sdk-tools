# Rollback Runbook

> Rollback NEVER rebuilds. It re-points the app/slot at a previously-built
> image tag recorded in App Configuration as
> `Deployment:<component>:LastKnownGoodTag`.

## When to roll back

| Signal                                                          | Action                              |
| --------------------------------------------------------------- | ----------------------------------- |
| `/health` or `/ping` returns non-200 for > 5 min after a deploy | Roll back the deployed component    |
| 5xx rate exceeds env-suite `minSuccessRate` budget over 5 min   | Roll back                           |
| p95 latency exceeds env-suite `latencyP95Ms` over 5 min         | Investigate; roll back if causal    |
| Cosmos / Search / Key Vault credential or schema error in logs  | Roll back; raise data-recovery flag |
| Operator request                                                | Roll back                           |

## Rollback paths per component

| Component      | Primary path                                                                   | Manual fallback                                                                     |
| -------------- | ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------- |
| frontend       | run `frontend.cd.yml` with the failure block (auto on CD failure)              | `pwsh deployment/scripts/rollback.ps1 -Component frontend -Environment prod`        |
| backend        | slot swap back to previous prod via `swap-slot.yml`                            | `pwsh ./scripts/rollback.ps1 -Component backend -Environment prod`                  |
| function-app   | slot swap back via `swap-slot.yml`                                             | `pwsh ./scripts/rollback.ps1 -Component function-app -Environment prod`             |
| agent-server   | redeploy LastKnownGoodTag                                                      | `pwsh ./scripts/rollback.ps1 -Component agent-server -Environment prod -Slot agent` |
| hosted agent   | redeploy LastKnownGoodTag via `azd deploy agent` (azd records previous deploy) | open Foundry portal → revert revision                                               |
| logic-app      | re-apply previous ARM template (Bicep history in git)                          | `az deployment group create --template-file <prev.json>`                            |
| knowledge-sync | restore previous Storage blob snapshot, re-run sync                            | manual: re-run sync from previous knowledge manifest                                |

## Pipeline-driven rollback (preferred)

Every component CD pipeline has an `on: failure:` block that automatically
invokes `pipelines/templates/rollback.yml`, which:

1. Reads `Deployment:<component>:LastKnownGoodTag` from App Configuration.
2. Calls `az webapp config container set` with that tag.
3. Restarts the app.

To trigger rollback manually:

- For App Service slot-based components, **re-run the swap-slot step**
  pointing back at the previous source slot. This is the fastest path —
  no image pull required.
- Otherwise, run `scripts/rollback.ps1` (see prod safety guard inside).

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
2. Identify root cause; do NOT redeploy the same tag without a fix.
3. Update `docs/operational-readiness-checklist.md` if a new gate would have
   prevented the issue.
