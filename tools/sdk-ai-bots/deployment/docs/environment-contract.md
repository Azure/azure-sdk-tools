# Environment Contract

The environment-suite (`infra/environments/environment-suite.yaml`) is the
**only** place in the deployment tree where the following values are
declared:

- environment names (`dev`, `preview`, `prod`)
- target subscription ID
- tenant ID
- fixed backend application client ID and Application ID URI
- resource group prefix
- deployment location
- whether local provision and deploy operations are allowed
- whether Bicep manages privileged authorization resources
- whether the chatbot evolution workflow is enabled
- candidate environment used by the production evolution workflow
- Teams group ID and channel IDs
- fixed Bicep resource-name and identity overrides
- per-component health paths

Every pipeline reads this file via `pipelines/templates/load-environment-suite.yml`.
Azure DevOps requires service connections during template expansion, so
`pipelines/templates/service-connection.yml` owns their aliases. Keep each
connection's target subscription synchronized with the corresponding
`subscriptionId` here.

## Schema

```yaml
environments:
    <env>:
        subscriptionId: GUID
        tenantId: GUID
        serverApplicationClientId: GUID
        serverApplicationIdUri: string # api:// or https:// URI
        resourceGroupPrefix: string
        location: string
        keyVaultName: string
        appConfigName: string
        containerRegistryName: string
        frontendSiteName: string
        agentServerSiteName: string
        functionAppName: string
        teamsAppId: GUID
        aiLocation: string
        cosmosDbLocation: string
        chatbotEvolutionAgentEnabled: bool
        candidateEnvironment: string? # required when evolution is enabled
        bicepOverrides: Record<string, string>?
        teamsGroupId: string
        teamsChannelIds: string[]
        manageAuthorizationResources: bool
        localDeployAllowed: bool

components:
    <component>:
        healthPath: string
```

Subscription IDs, names, locations, routing, identity overrides, and feature
flags actively drive provisioning and deployment. Approval is expressed by the
pipeline stage graph. Production's agent-server stabilization window is an
explicit orchestrator parameter, and release health follows the verification
steps in the [deploy runbook](runbook-deploy.md).

## Validation

Run `npm run validate-env-suite` to validate all environments, or append
`-- --environment dev` for an environment-scoped check. In addition to required
values and placeholders, the validator checks that `config/<env>/channel.yaml`
exactly matches `teamsChannelIds`, every route tenant is defined, and tenant
links target the configured Teams group.

## Local azd sync

`azd` reads `.azure/<env>/.env`, not `environment-suite.yaml` directly. To
keep the two in sync on a developer workstation:

```bash
npm run sync-env-suite -- --environment <env>
```

This script reads the per-env block from `environment-suite.yaml` and calls
`azd env set` for each mapped key (`AZURE_SUBSCRIPTION_ID`,
`AZURE_TENANT_ID`, `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`,
`CONTAINER_REGISTRY_NAME`, `KEY_VAULT_NAME`, `APP_CONFIG_NAME`,
`AZURE_APPCONFIG_ENDPOINT`, `CANDIDATE_APPCONFIG_ENDPOINT`,
`CHATBOT_EVOLUTION_AGENT_ENABLED`, `SERVER_APPLICATION_CLIENT_ID`,
`SERVER_APPLICATION_ID_URI`) plus every entry in
`bicepOverrides`. It also sets `AZD_IMAGE_TAG` to the environment name for local
native container builds. Deployment pipelines do not run this script; they set
an immutable reviewed tag immediately before `azd deploy`.

Teams routing values are suite-owned and copied into the local azd environment:
`teamsGroupId` → `TEAMS_GROUP_ID`, and the `teamsChannelIds` array →
comma-separated `TEAMS_CHANNEL_IDS`. Pipelines export the same values directly
from the suite. `serverApplicationClientId` becomes
`SERVER_APPLICATION_CLIENT_ID` for Easy
Auth. `serverApplicationIdUri` becomes `SERVER_APPLICATION_ID_URI`, and the
frontend Bicep adapter derives its scope as `<serverApplicationIdUri>/.default`
without persisting a second environment value. The Entra
application is created separately with `scripts/create-entra-app.ts`; azd and
the pipelines never manage it through Microsoft Graph.

The preprovision hook detects the active login as
`DEPLOYMENT_PRINCIPAL_ID` independently of `DEVELOPER_PRINCIPAL_ID`. This keeps
pipeline WIF grants separate from developer access. The developer principal is
configuration-owned and must be set explicitly in `bicepOverrides`; the hook
never substitutes the deployment identity when it is absent.

`manageAuthorizationResources` defaults through the environment contract and
controls Azure RBAC assignments and resource locks in Bicep. Keep it `true` for
fresh and pipeline-managed environments. The existing dev environment sets it
to `false` because its runtime assignments and delete lock are already present,
while local developers have Contributor access without
`Microsoft.Authorization/*/Write`. In that mode the final postprovision hook
also leaves protected Storage, Key Vault, App Configuration, and Search data
unchanged.

Each `infra/layers/<name>/main.bicepparam` adapts the environment variables
needed by that layer. Pipeline preview and apply use the same layer adapters.

## Environment variable ownership

Use one variable per meaning. Similar-looking variables are retained only when
they cross different contracts:

| Variable or pair | Ownership and reason |
| --- | --- |
| `AZURE_ENV_NAME` | azd-owned logical environment name. Hooks and image naming consume it; project code must not create an alias. |
| `AZD_IMAGE_TAG` | Deployment-owned immutable image tag. Local sync initializes it to the environment name; pipelines replace it with the reviewed tag. |
| `CONTAINER_REGISTRY_NAME` / `AZURE_CONTAINER_REGISTRY_ENDPOINT` | ARM resource name versus ACR login server. Azure CLI and Bicep need the name; azd image references need the endpoint. |
| `APP_CONFIG_NAME` / `AZURE_APPCONFIG_ENDPOINT` | ARM resource name versus runtime data-plane endpoint. CLI operations need the name; hosted services need the endpoint. |
| `SERVER_APPLICATION_CLIENT_ID` / `SERVER_APPLICATION_ID_URI` | Entra application client ID versus Application ID URI. Easy Auth registration and token audiences use different forms. |
| `<NAME>_OVERRIDE` / `<NAME>` | Desired resource name versus deployed layer output. The override survives `azd env refresh`; the canonical output feeds downstream layers and hooks. |
| `*_IMAGE_REPOSITORY` / `AZD_IMAGE_TAG` | Repository names derive from `azure.yaml`; provisioning baselines add an environment tag, while application deployment uses the reviewed immutable tag. |
| `SERVICE_AGENT_SERVER_IMAGE_NAME` | azd-owned full image actually published for agent-server. Bicep prefers it during reprovisioning so it does not restore a placeholder image. |
| `BOT_ID` / `AZURE_CLIENT_ID` | The frontend UAMI client ID under two external contracts: Bot Framework application ID and Azure Identity's standard selector. |

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
4. Add the environment's service-connection alias to
    `pipelines/templates/service-connection.yml` and ensure the connection targets
    the configured `subscriptionId`.
5. Authorize Azure DevOps to use the connection while compiling and running the
    deployment pipeline.
6. Restrict that connection to the intended pipelines and configure any
    required approvals or branch-control checks on the connection.
7. Re-run `npm run validate-env-suite`.
8. For a new preview or production environment, implement an approved
    first-state bootstrap pipeline. The normal full-stack preview requires
    existing layer state, and local deployment is disabled for those
    environments.
