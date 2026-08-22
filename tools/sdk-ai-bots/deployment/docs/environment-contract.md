# Environment Contract

The environment-suite (`infra/environments/environment-suite.yaml`) is the
**only** place in the deployment tree where the following values are
declared:

- environment names (`dev`, `preview`, `prod`)
- target subscription (alias + GUID)
- tenant ID
- resource group prefix
- region / ring list
- approval requirement
- whether prod is pipeline-only
- whether local deploy is allowed
- Teams group ID and channel IDs
- fixed Bicep resource-name and identity overrides
- rollout strategy (`direct`, `slot-swap`, `slot-swap-with-watch`)
- per-component image names, slot names, and health paths

Every pipeline reads this file via `pipelines/templates/load-environment-suite.yml`.
No pipeline hard-codes any of the above.

## Schema

```yaml
environments:
    <env>:
        subscription: string # ADO service-connection alias
        subscriptionId: GUID
        tenantId: GUID
        resourceGroupPrefix: string
        keyVaultName: string
        appConfigName: string
        containerRegistryName: string
        frontendSiteName: string
        agentServerSiteName: string
        functionAppName: string
        aiLocation: string
        cosmosDbLocation: string
        bicepOverrides: Record<string, string>?
        teamsGroupId: string
        teamsChannelIds: string[]
        regions:
            - name: string # e.g. westus2
              ring: string # canary | broad | single
              enabled: bool
        approvalRequired: bool
        prodDeployOnlyFromPipeline: bool
        localDeployAllowed: bool
        healthChecks:
            minSuccessRate: float # 0.0 .. 1.0
            latencyP95Ms: int
            stabilizationWindowMinutes: int? # prod only
        rolloutStrategy: enum

components:
    <component>:
        imageName: string
        serviceName: string
        healthPath: string
        slot: string
        easyAuthRequired: bool?
```

## Validation

Run `scripts/validate-env-suite.ps1` to confirm no placeholder values remain
and all required keys are populated. CI runs this on every PR that touches
`infra/environments/**`.

## Local azd sync

`azd` reads `.azure/<env>/.env`, not `environment-suite.yaml` directly. To
keep the two in sync on a developer workstation:

```pwsh
pwsh ./scripts/sync-env-suite.ps1 -Environment <env>
```

This script reads the per-env block from `environment-suite.yaml` and calls
`azd env set` for each mapped key (`AZURE_SUBSCRIPTION_ID`,
`AZURE_TENANT_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`,
`CONTAINER_REGISTRY_NAME`, `KEY_VAULT_NAME`, `APP_CONFIG_NAME`) plus every
entry in `bicepOverrides`.

Teams routing values are suite-owned and copied into the local azd environment:
`teamsGroupId` → `TEAMS_GROUP_ID`, and the `teamsChannelIds` array →
comma-separated `TEAMS_CHANNEL_IDS`. Pipelines export the same values directly
from the suite. Pin an existing Entra application with
`bicepOverrides.SERVER_AUDIENCE`; when it is omitted, preprovision creates or
finds the environment application and persists `SERVER_AUDIENCE` in azd.

`main.bicepparam` is the only adapter from these environment variables to
`main.bicep`. Both pipeline preview and apply invoke azd through that adapter.

The `preprovision` hook detects drift on every `azd provision` and fails fast
if the azd env is out of sync, pointing you back at this script. Pipelines
are unaffected — they read the suite directly via
`pipelines/templates/load-environment-suite.yml` and never touch
`.azure/<env>/.env`.

## Adding a new environment

1. Add a top-level block under `environments:` in `environment-suite.yaml`.
2. Add any existing-resource pins under the environment's `bicepOverrides`.
3. Create an Azure DevOps Environment named `sdk-ai-bots-<env>` (used by
   `deployment:` jobs for approval gates).
4. Register an Azure service connection with the same alias as
   `subscription:`.
5. Re-run `validate-env-suite.ps1`.
