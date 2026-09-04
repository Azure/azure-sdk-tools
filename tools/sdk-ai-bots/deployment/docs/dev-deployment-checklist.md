# Dev Deployment Checklist

Use this checklist to create a fresh `dev` environment in the Microsoft tenant
without modifying the existing QA bot resource groups.

Live values and access checks in this document were verified on 2026-08-26.
Recheck quota, permissions, policies, and name availability immediately before
deployment.

## Approved target

- [x] Tenant: Microsoft (`72f988bf-86f1-41af-91ab-2d7cd011db47`).
- [x] Subscription: `Azure SDK Engineering System`
      (`a18897a6-7e44-457d-9260-f2854c0aca42`).
- [x] Create exactly one resource group: `azure-sdk-qa-bot-dev-20260826`.
- [x] Resource group location: `centralus`.
- [x] AI and model deployment location: `eastus2`.
- [x] Cosmos DB location: `eastus2`.
- [x] Preserve `azure-sdk-qa-bot`, `azure-sdk-qa-bot-test`, and
      `azure-sdk-qa-bot-dev` without changes.

## Configuration and validation

- [x] Point the `dev` block in
      [environment-suite.yaml](../infra/environments/environment-suite.yaml) at
      the approved subscription, tenant, resource group, and regions.
- [x] Use globally unique names for Key Vault, App Configuration, ACR, and all
      three web apps.
- [ ] Replace the remaining `REPLACE_WITH_*` values for other environments, or
      update validation so a dev-only run does not fail on preview placeholders.
- [ ] Recreate and sync the local `azd` environment after configuration changes:

  ```bash
  azd env remove dev --force
  azd env new dev \
    --subscription a18897a6-7e44-457d-9260-f2854c0aca42 \
    --location centralus \
    --no-prompt
  pwsh deployment/scripts/sync-env-suite.ps1 -Environment dev
  ```

- [ ] Confirm the local environment has no outputs inherited from the old TME
      deployment.
- [ ] Complete the `azure-validate` workflow and record fresh validation proof
      for this target before provisioning.
- [ ] Run a resource-group-only preview and verify it proposes only the approved
      resource group.

## Azure permissions

The provisioning identity must create a subscription-scope deployment and
resource group, deploy resources, create role assignments, and create a resource
lock.

- [ ] Activate one of these temporary subscription-scope role combinations:
  - `Owner`; or
  - `Contributor` + `User Access Administrator`; or
  - `Contributor` + `Role Based Access Control Administrator` +
    `Locks Contributor`.
- [ ] Keep the elevated assignment active through infrastructure provisioning
      and hosted-agent deployment. Postdeploy hooks grant roles to identities
      that do not exist earlier.
- [ ] Refresh Azure CLI credentials after activating a PIM assignment.
- [ ] Verify `Microsoft.Resources/deployments/whatIf/action` works at
      subscription scope.
- [ ] Verify the deployment identity can read source keys, seed Key Vault, seed
      App Configuration, and create all required resource-level role
      assignments.
- [x] Required resource providers are registered.
- [x] No inherited lock or deny assignment applies to the new resource group.
- [x] Applicable Storage and App Configuration policies match the Bicep
      configuration.

## AI capacity

The Bicep deployment currently requests five GlobalStandard model deployments.
The following live quota was observed in `eastus2`:

| Model | Used | Limit | Remaining | Requested | Status |
|---|---:|---:|---:|---:|---|
| `gpt-4.1` | 1,198 | 3,000 | 1,802 | 1 | Ready |
| `gpt-5.6-sol` | 500 | 1,000 | 500 | 500 | Ready |
| `gpt-5.1` | 2,130 | 3,000 | 870 | 1 | Ready |
| `gpt-5-mini` | 1,110 | 3,000 | 1,890 | 1 | Ready |
| `text-embedding-3-small` | 120 | 3,000 | 2,880 | 1 | Ready |

- [x] Replace the exhausted `gpt-5.4` deployment with `gpt-5.6-sol`.
- [x] Replace the exhausted `text-embedding-ada-002` deployment with the
      supported `text-embedding-3-small` successor.
- [ ] Re-run the live quota check immediately before provisioning.

GlobalStandard quota is shared across regions. The remaining `gpt-5.6-sol`
capacity exactly matches this deployment's request, so any intervening quota
consumption will block provisioning.

## Entra application

- [ ] Run `npm run create-entra-app -- ...` with an authorized interactive
      identity, outside the deployment pipeline.
- [ ] Copy the generated client ID to `serverApplicationClientId` and the chosen
      Application ID URI to `serverApplicationIdUri` in the environment suite.
- [ ] Confirm the server application has the intended sign-in audience and a
      home-tenant service principal.
- [ ] Configure the server identifier URI, delegated scope, application role,
      and Azure CLI preauthorization through the bootstrap script.
- [ ] Confirm Azure Bot uses the frontend UAMI client ID with
      `msaAppType=UserAssignedMSI`; no separate bot app registration is used.
- [ ] After the UAMIs exist, rerun the bootstrap with `--caller-client-id` for
      each identity that must receive `access_as_application`.
- [ ] Confirm the Azure DevOps service connection has no Microsoft Graph
      application permissions; backend app management is not a pipeline task.
- [ ] Verify the configured Service Management Reference is valid for the new
      server application.

## Azure DevOps setup

- [x] The current user can create, edit, and queue YAML pipelines under
      `\tools\sdk-ai-bots`.
- [ ] Register
      [qa-bot-all.yml](../pipelines/orchestrators/qa-bot-all.yml)
      as `tools - sdk-ai-bots - provision-and-deploy-all`.
- [ ] Create or share a workload-identity-federated service connection named
      `Azure SDK Engineering System`.
- [ ] Give the service connection the required temporary Azure bootstrap roles,
      or create the resource group first and scope later runs to that group.
- [ ] Grant the pipeline permission to use the service connection, GitHub
      repository connection, and configured agent pool.
- [ ] Confirm the service connection can run Bicep what-if and all seven
      provisioning layers.
- [ ] Register the component provision and deployment pipelines needed after
      bootstrap.

Local `azd` provisioning does not require a service connection, but it still
requires the Azure and Entra permissions above.

## Infrastructure provision

- [ ] Confirm `azure-sdk-qa-bot-dev-20260826` still does not exist immediately
      before apply.
- [ ] Review previews for all seven layers and reject any unexpected Modify or
      Delete operation.
- [ ] Provision layers in dependency order:
  1. `resource-group`
  2. `shared-resources`
  3. `agent`
  4. `frontend`
  5. `agent-server`
  6. `function-app`
  7. `logic-app`
- [ ] Confirm all resources were created only in the approved resource group.
- [ ] Verify every Bicep and hook-created role assignment after propagation.
- [ ] Confirm the resource-group deletion lock exists.

## Application and runtime deployment

- [ ] Build and push the frontend, agent-server, Function App, and hosted-agent
      images to the new ACR.
- [ ] Deploy `agent-server`.
- [ ] Deploy `function-app` before applying the full Logic App definition.
- [ ] Deploy the hosted agent and wait for its runtime identity to exist.
- [ ] Run hosted-agent postdeploy reconciliation for data-plane RBAC and
      `AZURE_APPCONFIG_ENDPOINT`.
- [ ] Deploy `frontend`.
- [ ] Confirm the provisioning hook seeded `AI-SEARCH-APIKEY` in the
      environment Key Vault. It does not seed an Azure OpenAI account key.
- [ ] Confirm App Configuration contains all fixed and derived settings.
- [ ] Confirm postprovision created or updated the configured Search synonym
      map, index, data sources, skillsets, indexers, knowledge source, and
      knowledge base.
- [ ] Add the shared managed identity to the required Azure DevOps organization
      and projects so the Function App can mint and store `ado-token`.
- [ ] Grant the frontend and hosted-agent identities signing access to
      `azuresdkengkeyvault/keys/azure-sdk-automation`.
- [ ] Configure knowledge-sync WIF access and seed `SSH-PRIVATE-KEY` if
      knowledge sync is part of this environment.

## Teams and Logic App delegated consent

The Azure service connection creates the Logic App and API connection resources.
It cannot authorize the Teams connector. Authorization belongs to a Microsoft
365 user whose identity the connection will use at runtime.

- [ ] Select a durable Teams service account in the Microsoft tenant.
- [ ] Verify the account has a Teams license and access to every configured team
      and channel.
- [ ] Verify tenant policy permits the account to use the Teams Workflows app
      and requested connector operations.
- [ ] Provision the Teams connection shell with the workflow disabled.
- [ ] In the pipeline's **Authorize Teams API connection** validation, open the
      supplied portal link and complete Teams delegated OAuth as the selected
      service account, then resume the run.
- [ ] Do not store its password, OAuth code, refresh token, or connector token
      in pipeline variables or logs.
- [ ] Confirm the pipeline's verification job reports
      `properties.statuses[0].status` as `Connected`.
- [ ] On future provisions, verify the preprovision hook preserves the
      connected API resource.
- [ ] Verify the workflow definition is complete and its state is `Enabled`.

The pipeline pause does not handle credentials, OAuth codes, refresh tokens, or
connector tokens. The Teams service account always signs in through the Azure
portal.

## Teams application

- [ ] Generate a Teams package whose bot ID matches the newly deployed
      `BOT_ID`.
- [ ] Increment the manifest version when replacing an existing installation.
- [ ] Ensure the operator has Teams app-catalog publish/read permission and
      permission to install or upgrade the app in the target team.
- [ ] Publish or approve the package in the tenant app catalog.
- [ ] Install or upgrade it in the configured team.
- [ ] Verify the installed version and bot ID after propagation.

## Final verification

- [ ] Frontend health endpoint returns HTTP 200.
- [ ] Agent-server is running and rejects anonymous requests as configured.
- [ ] Function App is running and all expected functions are loaded.
- [ ] Hosted agent readiness succeeds and the latest version has the expected
      image and environment variables.
- [ ] Logic App is enabled with a connected Teams API connection and the full
      workflow definition.
- [ ] Live RBAC verification passes for the shared identity, frontend identity,
      Foundry project identity, hosted-agent runtime identity, developer, and
      pipeline principal.
- [ ] Key Vault, App Configuration, Storage, Cosmos DB, Search, ACR, and Foundry
      data-plane operations succeed through managed identities.
- [ ] The Teams bot responds in each configured channel.
- [ ] Existing QA bot resource groups and their resources remain unchanged.
- [ ] Record endpoints, resource names, validation proof, and any manual
      follow-up in the deployment plan.

## Re-provision and cleanup

- [ ] After any later `azd provision`, redeploy services that restore imperative
      state, especially the Function App/Logic App definition and immutable
      application image tags.
- [ ] Preserve the connected Teams API connection on every re-provision.
- [ ] Remove the resource-group lock before an approved teardown, then restore
      it if teardown is cancelled.
- [ ] Treat resource-group deletion, Key Vault purge, app-registration deletion,
      and Teams app removal as separately approved destructive actions.