# Environment Contract

The environment-suite (`infra/environments/environment-suite.yaml`) is the
**only** place in the deployment tree where the following values are
declared:

- environment names (`dev`, `preview`, `prod`)
- target subscription (alias + GUID)
- tenant ID
- fixed backend application client ID and Application ID URI
- resource group prefix
- region / ring list
- approval requirement
- whether prod is pipeline-only
- whether local deploy is allowed
- whether the chatbot evolution workflow is enabled
- candidate environment used by the production evolution workflow
- Teams group ID and channel IDs
- fixed Bicep resource-name and identity overrides
- rollout and health metadata reserved for policy and future gates
- per-component image names and health metadata

Every pipeline reads this file via `pipelines/templates/load-environment-suite.yml`.
Azure DevOps requires service connections during template expansion, so
`pipelines/templates/service-connection.yml` mirrors each `subscription` alias.
Keep the two files synchronized.

## Schema

```yaml
environments:
    <env>:
        subscription: string # ADO service-connection alias
        subscriptionId: GUID
        tenantId: GUID
        serviceManagementReference: string? # one-time Entra bootstrap only
        serverApplicationClientId: GUID
        serverApplicationIdUri: string # api:// or https:// URI
        resourceGroupPrefix: string
        keyVaultName: string
        appConfigName: string
        containerRegistryName: string
        frontendSiteName: string
        agentServerSiteName: string
        functionAppName: string
        aiLocation: string
        cosmosDbLocation: string
        chatbotEvolutionAgentEnabled: bool
        candidateEnvironment: string? # required when evolution is enabled
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
        rolloutStrategy: enum # descriptive; exported but not enforced

components:
    <component>:
        imageName: string
        serviceName: string
        healthPath: string
        slot: string # reserved; active orchestrators do not swap slots
        easyAuthRequired: bool?
```

    `subscription`, IDs, names, regions, routing, identity overrides, and feature
    flags actively drive provisioning and deployment. `rolloutStrategy`, slot
    names, and the success-rate/latency thresholds are currently descriptive
    metadata. The loader exports `ROLLOUT_STRATEGY`, but no active stage consumes it
    or enforces those thresholds. Do not treat those fields as an implemented
    traffic-shifting or health gate.

## Validation

Run `scripts/validate-env-suite.ps1` to validate all environments, or pass
`-Environment dev` for an environment-scoped check. In addition to required
values and placeholders, the validator checks that `config/<env>/channel.yaml`
exactly matches `teamsChannelIds`, every route tenant is defined, and tenant
links target the configured Teams group.

## Local azd sync

`azd` reads `.azure/<env>/.env`, not `environment-suite.yaml` directly. To
keep the two in sync on a developer workstation:

```pwsh
pwsh ./scripts/sync-env-suite.ps1 -Environment <env>
```

This script reads the per-env block from `environment-suite.yaml` and calls
`azd env set` for each mapped key (`AZURE_SUBSCRIPTION_ID`,
`AZURE_TENANT_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`,
`CONTAINER_REGISTRY_NAME`, `KEY_VAULT_NAME`, `APP_CONFIG_NAME`,
`AZURE_APPCONFIG_ENDPOINT`, `CANDIDATE_APPCONFIG_ENDPOINT`,
`CHATBOT_EVOLUTION_AGENT_ENABLED`, `SERVER_AUDIENCE`,
`SERVER_APPLICATION_ID_URI`, `RAG_SERVICE_SCOPE`) plus every entry in
`bicepOverrides`.

Teams routing values are suite-owned and copied into the local azd environment:
`teamsGroupId` → `TEAMS_GROUP_ID`, and the `teamsChannelIds` array →
comma-separated `TEAMS_CHANNEL_IDS`. Pipelines export the same values directly
from the suite. `serverApplicationClientId` becomes `SERVER_AUDIENCE` for Easy
Auth. `serverApplicationIdUri` becomes `SERVER_APPLICATION_ID_URI`, and the
frontend scope is derived as `<serverApplicationIdUri>/.default`. The Entra
application is created separately with `scripts/create-entra-app.ts`; azd and
the pipelines never manage it through Microsoft Graph.

The preprovision hook detects the active login as
`DEPLOYMENT_PRINCIPAL_ID` independently of `DEVELOPER_PRINCIPAL_ID`. This keeps
pipeline WIF grants separate from developer access. The developer principal is
configuration-owned and must be set explicitly in `bicepOverrides`; the hook
never substitutes the deployment identity when it is absent.

Each `infra/layers/<name>/main.bicepparam` adapts the environment variables
needed by that layer. Pipeline preview and apply use the same layer adapters.

The `preprovision` hook detects drift on every `azd provision` and fails fast
if the azd env is out of sync, pointing you back at this script. Pipelines
are unaffected — they read the suite directly via
`pipelines/templates/load-environment-suite.yml` and never touch
`.azure/<env>/.env`.

## Adding a new environment

1. Add a top-level block under `environments:` in `environment-suite.yaml`.
2. Run `scripts/create-entra-app.ts`, then add its client ID and Application ID
    URI to the environment block.
3. Add any existing-resource pins under the environment's `bicepOverrides`.
4. Register an Azure service connection with the same alias as
   `subscription:`.
5. Add the alias to `pipelines/templates/service-connection.yml` so Azure
    DevOps can authorize it while compiling the pipeline.
6. Restrict that connection to the intended pipelines and configure any
    required approvals or branch-control checks on the connection.
7. Re-run `validate-env-suite.ps1`.
8. For a new preview or production environment, implement an approved
    first-state bootstrap pipeline. The normal full-stack preview requires
    existing layer state, and local deployment is disabled for those
    environments.
