# Dev Deployment Checklist

Use this checklist to operate and finish validation of the deployed `dev`
environment in the Microsoft tenant.

Control-plane state, pipeline registration, and public runtime probes were
verified on 2026-09-04. Checks requiring a Teams user or additional data-plane
access remain unchecked.

## Approved target

- [x] Tenant: Microsoft (`72f988bf-86f1-41af-91ab-2d7cd011db47`).
- [x] Subscription: `Azure SDK Developer Playground`
      (`faa080af-c1d8-40ad-9cce-e1a450ca5b57`).
- [x] Service connection: `azure-sdk-tests-playground` using Workload Identity
      Federation and targeting the subscription above.
- [x] Resource group `azure-sdk-qa-bot-dev-20260826` exists in `centralus` with
      provisioning state `Succeeded`.
- [x] AI and model deployment location: `eastus2`.
- [x] Cosmos DB location: `eastus2`.
- [x] Frontend App Service, its plan, and frontend monitoring resources are in
      `westus2`; shared resources and backend services are primarily in
      `centralus`.
- [x] The existing `azure-sdk-qa-bot`, `azure-sdk-qa-bot-test`, and
      `azure-sdk-qa-bot-dev` resource groups remain present in the separate
      `Azure SDK Engineering System` subscription.

### Live resource names

| Component | Resource | Location |
|---|---|---|
| Frontend | `azsdkqabot-dev-20260826` | `westus2` |
| Agent server | `azuresdkqabot-server-dev-20260826` | `centralus` |
| Function App | `azuresdkqabot-function-dev-20260826` | `centralus` |
| Logic App | `azuresdkqabot-logicapp-ccxebs` | `centralus` |
| AI account / project | `qabot-ai-resource-ccxebs` / `qabot-ai` | `eastus2` |
| Search | `qabot-search-ccxebs` | `centralus` |
| Cosmos DB | `qabot-db-ccxebs` | `eastus2` |
| Storage | `qabotstorageccxebs` | `centralus` |
| Key Vault | `azsdkqabot-kv-dev260826` | `centralus` |
| App Configuration | `azsdkqabot-config-dev-20260826` | `centralus` |
| Container Registry | `azsdkqabotacrdev20260826` | `centralus` |

### Live endpoints

| Component | Endpoint |
|---|---|
| Frontend | `https://azsdkqabot-dev-20260826.azurewebsites.net` |
| Agent server | `https://azuresdkqabot-server-dev-20260826.azurewebsites.net` |
| Function App | `https://azuresdkqabot-function-dev-20260826.azurewebsites.net` |
| Foundry project | `https://qabot-ai-resource-ccxebs.services.ai.azure.com/api/projects/qabot-ai` |
| Hosted-agent responses | `https://qabot-ai-resource-ccxebs.services.ai.azure.com/api/projects/qabot-ai/agents/azure-sdk-chat-agent/endpoint/protocols/openai/responses?api-version=v1` |

## Configuration and validation

- [x] The `dev` block in
      [environment-suite.yaml](../infra/environments/environment-suite.yaml)
      matches the active service connection, subscription, tenant, resource
      group, and regions.
- [x] Use globally unique names for Key Vault, App Configuration, ACR, and all
      three web apps.
- [ ] Replace the remaining `REPLACE_WITH_*` values for other environments, or
      update validation so a dev-only run does not fail on preview placeholders.
- [ ] Before local provisioning, create or select `dev` in the live subscription
      and run the suite sync script:

  ```bash
  azd env remove dev --force
      azd env new dev --subscription faa080af-c1d8-40ad-9cce-e1a450ca5b57 --location centralus --no-prompt
  pwsh deployment/scripts/sync-env-suite.ps1 -Environment dev
  ```

- [x] Full-stack preflight and apply completed successfully in pipeline run
      `6787575`.
- [ ] Record a fresh validation run after the Teams consent checker fix is
      submitted.

## Azure permissions

The pipeline identity is service principal
`30511c9d-ba1a-4c7b-b422-5b543da11b3f` (`azure-sdk-tests`).

- [x] The service principal has `Owner` at the Developer Playground subscription
      scope.
- [x] Subscription-scope what-if and all seven provisioning layers succeeded in
      run `6787575`.
- [x] Postprovision granted the pipeline principal Key Vault and App Configuration
      data-owner roles and completed seeding in run `6787575`.
- [x] Required resource providers are registered.
- [ ] Revalidate deny assignments and applicable policies before destructive or
      privilege-changing operations.

## AI deployments

The following live `GlobalStandard` deployments in
`qabot-ai-resource-ccxebs` were verified as `Succeeded`:

| Deployment | Model version | Capacity |
|---|---|---:|
| `gpt-4.1` | `2025-04-14` | 1 |
| `gpt-5.6-sol` | `2026-07-09` | 500 |
| `gpt-5.1` | `2025-11-13` | 1 |
| `gpt-5-mini` | `2025-08-07` | 1 |
| `text-embedding-3-small` | `1` | 1 |

- [x] All five requested model deployments are provisioned successfully.
- [ ] Re-run live quota checks before changing model capacity or adding a
      deployment.

## Entra application

- [x] Backend application `azuresdkqabot-server-playground` exists with client
      ID `f9b6590e-3710-402a-a17a-d6b99de129a3` and a home-tenant service
      principal.
- [x] Sign-in audience is `AzureADMyOrg`; identifier URI is
      `api://72f988bf-86f1-41af-91ab-2d7cd011db47/azure-sdk-qa-bot-playground`.
- [x] Delegated scope `access_as_user` and application role
      `access_as_application` exist.
- [x] Azure CLI is preauthorized for the delegated scope.
- [x] Bot Service `azsdkqabot-dev-20260826-federated` uses frontend UAMI client
      ID `c41a6864-03fe-4a08-8a3c-c1e733508398` with
      `msaAppType=UserAssignedMSI`.
- [ ] Verify every required managed identity has an assignment for
      `access_as_application` and verify the Service Management Reference.

## Azure DevOps setup

- [x] The current user can create, edit, and queue YAML pipelines under
      `\tools\sdk-ai-bots`.
- [x] Full-stack pipeline [qa-bot-all.yml](../pipelines/orchestrators/qa-bot-all.yml)
      is enabled as definition `8265`.
- [x] Shared-resources, Logic App, Function App, frontend, agent-server, and
      hosted-agent component pipelines are enabled as definitions `8367` through
      `8372`.
- [x] `azure-sdk-tests-playground` is ready, authorized, and uses Workload
      Identity Federation.
- [x] Full-stack run `6787575` completed preflight, approval, provisioning, and
      all four deployment stages successfully.
- [ ] Register the component CI and knowledge-sync pipelines if they are still
      required; no definitions using those new YAML paths were found.
- [ ] Disable or delete stale definitions `8266` and `8357`, which still point
      to removed `provision-all-dev.yml`.
- [ ] Rerun failed build `6788293` after submitting the corrected dev
      subscription ID, and confirm the Teams consent gate opens.

Local `azd` provisioning does not require a service connection, but it still
requires the Azure and Entra permissions above.

## Infrastructure provision

- [x] Resource group and expected resources exist in Developer Playground.
- [x] All seven layers were provisioned in dependency order:
  1. `resource-group`
  2. `shared-resources`
  3. `agent`
  4. `frontend`
  5. `agent-server`
  6. `function-app`
  7. `logic-app`
- [x] Latest layer deployments are `Succeeded`; the latest Logic App deployment
      is `dev-logic-app-1788443884`.
- [x] Azure resource inventory is contained in the approved resource group.
- [ ] Verify every Bicep and hook-created role assignment after propagation.
- [x] Deletion lock `azsdkqabot-delete-lock-ccxebs` exists at `CanNotDelete`.
- [ ] Continue reviewing every preflight and reject unexpected Modify or Delete
      operations before future applies.

## Application and runtime deployment

- [x] ACR contains frontend, agent-server, Function App, and hosted-agent image
      repositories with deployed tags.
- [x] Frontend, agent-server, and Function App resources report `Running`.
- [x] Frontend `/health` returns HTTP 200.
- [x] Agent-server `/ping` rejects anonymous requests with HTTP 401, as EasyAuth
      requires.
- [x] Function App exposes `adoTokenRefresh`, `channels`, `convertActivity`,
      `feedbacks`, and `records`.
- [x] Hosted-agent deployment succeeded in run `6787575`; postdeploy granted its
      data-plane roles and created version 2 with `AZURE_APPCONFIG_ENDPOINT`.
- [ ] Configure `AGENT_BASE_URL` or otherwise run the hosted-agent readiness
      probe; run `6787575` skipped ping because that value was absent.
- [x] Postprovision seeded `AI-SEARCH-APIKEY` in Key Vault and wrote 40 App
      Configuration keys in run `6787575`.
- [x] Postprovision reconciled the configured Search synonym
      map, index, data sources, skillsets, indexers, knowledge source, and
      knowledge base in run `6787575`.
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
- [x] Teams (`teams-ccxebs`), Azure Blob (`azureblob-ccxebs`), and Cosmos DB
      (`documentdb-ccxebs`) connection resources exist. Blob and Cosmos report
      `Ready`; Teams reports `Error / Unauthenticated`.
- [x] The full workflow definition, trigger, connections, and parameters are
      deployed; workflow state is `Disabled` pending Teams consent.
- [ ] In the pipeline's **Authorize Teams API connection** validation, open the
      supplied portal link and complete Teams delegated OAuth as the selected
      service account, then resume the run.
- [ ] Do not store its password, OAuth code, refresh token, or connector token
      in pipeline variables or logs.
- [ ] Confirm the pipeline's verification job reports
      `properties.statuses[0].status` as `Connected`.
- [ ] On future provisions, verify the preprovision hook preserves the
      connected API resource.
- [ ] After consent, deploy the Function App or run `deploy:logic-app` and verify
      the existing full workflow becomes `Enabled`.

The pipeline pause does not handle credentials, OAuth codes, refresh tokens, or
connector tokens. The Teams service account always signs in through the Azure
portal.

## Teams application

- [x] Environment suite records Teams app ID
      `9d620152-6b1b-44f2-9635-b1f120c93c4a` for dev.
- [ ] Generate or validate a Teams package whose bot ID matches the deployed
      `BOT_ID`.
- [ ] Increment the manifest version when replacing an existing installation.
- [ ] Ensure the operator has Teams app-catalog publish/read permission and
      permission to install or upgrade the app in the target team.
- [ ] Publish or approve the package in the tenant app catalog.
- [ ] Install or upgrade it in the configured team.
- [ ] Verify the installed version and bot ID after propagation.

## Final verification

- [x] Frontend health endpoint returns HTTP 200.
- [x] Agent-server is running and rejects anonymous requests as configured.
- [x] Function App is running and all expected functions are loaded.
- [ ] Hosted-agent readiness succeeds; deployment, image, environment variable,
      and RBAC reconciliation are complete, but the ping remains unverified.
- [ ] Logic App is enabled with a connected Teams API connection and the full
      workflow definition.
- [ ] Live RBAC verification passes for the shared identity, frontend identity,
      Foundry project identity, hosted-agent runtime identity, developer, and
      pipeline principal.
- [ ] Key Vault, App Configuration, Storage, Cosmos DB, Search, ACR, and Foundry
      data-plane operations succeed through managed identities.
- [ ] The Teams bot responds in each configured channel.
- [x] Existing QA bot resource groups remain present in Engineering System;
      verifying their individual resources were unchanged remains an operator
      review item.
- [x] Resource names, endpoints, and validation evidence are recorded above;
      remaining manual follow-up stays unchecked in this checklist.

## Re-provision and cleanup

- [ ] After any later `azd provision`, redeploy services that restore imperative
      state, especially the Function App/Logic App definition and application
      images.
- [ ] After Teams consent is completed, preserve the connected API connection on
      every re-provision.
- [x] The resource-group deletion lock is currently active.
- [ ] Remove the resource-group lock before an approved teardown, then restore
      it if teardown is cancelled.
- [ ] Treat resource-group deletion, Key Vault purge, app-registration deletion,
      and Teams app removal as separately approved destructive actions.