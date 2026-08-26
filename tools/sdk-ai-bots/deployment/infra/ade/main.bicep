// ─────────────────────────────────────────────────────────────────────────────
// ADE entry template — RESOURCE-GROUP scoped.
//
// Azure Deployment Environments (ADE) creates and owns a resource group per
// environment, then deploys this template INTO it. That is why this file is
// `targetScope = 'resourceGroup'` and does NOT create a resource group (unlike
// the subscription-scoped ../layers/*/main.bicep entries used by azd).
//
// It composes the same six workload layers as azd, but always lets
// resource names auto-generate from `uniqueString(resourceGroup().id)` — which
// is exactly what ephemeral per-PR environments want (unique, collision-free
// names, no prod name-override parameters).
//
// ┌───────────────────────────────────────────────────────────────────────────┐
// │ SOURCE OF TRUTH: ../layers/*/main.bicep                                    │
// │ The composition here MUST stay in sync with the azd layer entry points.   │
// │ If you add or rename a layer input there, mirror it here.                  │
// └───────────────────────────────────────────────────────────────────────────┘
//
// Layer order (encoded by Bicep references):
//   1. shared-resources → 2. agent → 3. frontend → 4. agent-server
//                                          ↓
//                                 5. function-app → 6. logic-app
// ─────────────────────────────────────────────────────────────────────────────

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = 'westus2'

@description('Azure region for the Cognitive Services / Foundry AI resources. Defaults to `location`.')
param aiLocation string = location

@description('Client ID (audience) for authenticating to the agent server. External Entra app registration.')
param serverAudience string

@description('Entra application client ID used by Teams and Azure Bot Service. The app is multitenant only when Azure and Teams use different tenants.')
param botAppId string

@description('Teams team (group) ID the Logic App monitors.')
param teamsGroupId string

@description('Teams channel IDs the Logic App subscribes to.')
param teamsChannelIds array

@description('When false, skip PUT on the Teams `Microsoft.Web/connections` resource so an already-authorized OAuth connection is left untouched. See the logic-app layer for the full contract.')
param createTeamsConnection bool = true

@description('Frontend (Teams bot) container image repository and tag.')
param frontendImageRepository string

@description('Agent server container image repository and tag.')
param agentServerImageRepository string

@description('Function App container image repository and tag.')
param functionImageRepository string

// Unique suffix — same pattern as ../main.bicep so generated names are stable
// per environment (resource group) and collision-free across PR environments.
var _suffix = substring(uniqueString(resourceGroup().id), 0, 6)

// ── Shared resources ───────────────────────────────────────────────────────────
module sharedResources '../layers/shared-resources/main.bicep' = {
  name: 'shared-resources'
  params: {
    location:              location
    managedIdentityNameOverride:   'qabot-identity-${_suffix}'
    actionGroupNameOverride:       'qabot-alert-${_suffix}'
    keyVaultNameOverride:          'qabot-keyvault-${_suffix}'
    appConfigNameOverride:         'qabot-config-${_suffix}'
    searchServiceNameOverride:     'qabot-search-${_suffix}'
    containerRegistryNameOverride: 'qabotcontainer${_suffix}'
    storageAccountNameOverride:    'qabotstorage${_suffix}'
    cosmosDbAccountNameOverride:   'qabot-db-${_suffix}'
  }
}

// ── Layer 2: Agent / AI services ───────────────────────────────────────────────
module agent '../layers/agent/main.bicep' = {
  name: 'agent'
  params: {
    location: aiLocation
    managedIdentityPrincipalId: sharedResources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
    storageAccountName: sharedResources.outputs.STORAGE_ACCOUNT_NAME
    storageBlobEndpoint: sharedResources.outputs.STORAGE_BLOB_ENDPOINT
    containerRegistryName: sharedResources.outputs.CONTAINER_REGISTRY_NAME
    aiResourceNameOverride:        'qabot-ai-resource-${_suffix}'
    aiProjectNameOverride:         'qabot-ai'
    agentLogWorkspaceNameOverride: 'qabot-agent-log-${_suffix}'
    agentAppInsightsNameOverride:  'qabot-agent-${_suffix}'
  }
}

// ── Layer 3: Frontend (Teams Bot) ──────────────────────────────────────────────
module frontend '../layers/frontend/main.bicep' = {
  name: 'frontend'
  params: {
    storageAccountName: sharedResources.outputs.STORAGE_ACCOUNT_NAME
    containerRegistryName: sharedResources.outputs.CONTAINER_REGISTRY_NAME
    frontendImageRepository: frontendImageRepository
    frontendBaseNameOverride:              'azsdkqabot-${_suffix}'
    botServiceNameOverride:                 'azsdkqabot-bot-${_suffix}'
    frontendAppInsightsNameOverride:       'azsdkqabot-insights-${_suffix}'
    frontendEmailActionGroupNameOverride:  'azsdkqabot-email-alerts-${_suffix}'
    frontendDiagnosticSettingNameOverride: 'azsdkqabot-diagnostic-${_suffix}'
    frontendHealthTestNameOverride:        'azsdkqabot-health-test-${_suffix}'
    frontendServerErrorsAlertNameOverride: 'azsdkqabot-server-errors-${_suffix}'
    frontendHealthCheckAlertNameOverride:  'azsdkqabot-health-check-failure-${_suffix}'
    frontendDeleteLockNameOverride:        'azsdkqabot-delete-lock-${_suffix}'
    azureTableNameForConversation: 'TeamsChannelConversationsDev'
    ragServiceScope:               'api://${serverAudience}/.default'
    botAppId:                       botAppId
  }
}

// ── Layer 4: Agent server ──────────────────────────────────────────────────────
module agentServer '../layers/agent-server/main.bicep' = {
  name: 'agent-server'
  params: {
    location: location
    containerImage: '${sharedResources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT}/${agentServerImageRepository}'
    managedIdentityClientId: sharedResources.outputs.MANAGED_IDENTITY_CLIENT_ID
    frontendIdentityClientId: frontend.outputs.BOT_MANAGED_IDENTITY_CLIENT_ID
    serverAudience: serverAudience
    sharedIdentityName: sharedResources.outputs.MANAGED_IDENTITY_NAME
    frontendIdentityName: frontend.outputs.BOT_IDENTITY_NAME
    aiResourceName: agent.outputs.AI_RESOURCE_NAME
    aiProjectName: agent.outputs.AI_PROJECT_NAME
    cosmosDbAccountName: sharedResources.outputs.COSMOSDB_ACCOUNT_NAME
    storageAccountName: sharedResources.outputs.STORAGE_ACCOUNT_NAME
    appConfigName: sharedResources.outputs.APP_CONFIG_NAME
    actionGroupName: sharedResources.outputs.ACTION_GROUP_NAME
    agentServerAppServicePlanNameOverride: 'azuresdkqabot-appserviceplan-${_suffix}'
    agentServerLogWorkspaceNameOverride:   'azuresdkqabot-log-${_suffix}'
    agentServerSiteNameOverride:           'azuresdkqabot-server-${_suffix}'
    agentServerAppInsightsNameOverride:    'azuresdkqabot-server202510300250-${_suffix}'
    agentServerAlertNameOverride:          'azuresdkqabot-alert-${_suffix}'
  }
}

// ── Layer 5: Function App ──────────────────────────────────────────────────────
module functionApp '../layers/function-app/main.bicep' = {
  name: 'function-app'
  params: {
    location: location
    containerImage: '${sharedResources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT}/${functionImageRepository}'
    managedIdentityClientId: sharedResources.outputs.MANAGED_IDENTITY_CLIENT_ID
    botId: frontend.outputs.BOT_ID
    managedIdentityResourceId: sharedResources.outputs.MANAGED_IDENTITY_RESOURCE_ID
    storageAccountName: sharedResources.outputs.STORAGE_ACCOUNT_NAME
    keyVaultName: sharedResources.outputs.KEY_VAULT_NAME
    functionAppServicePlanNameOverride: 'azuresdkqabot-functionserviceplan-${_suffix}'
    functionLogWorkspaceNameOverride:   'azuresdkqabot-function-log-${_suffix}'
    functionAppNameOverride:            'azuresdkqabot-function-${_suffix}'
  }
}

// ── Layer 6: Logic App ─────────────────────────────────────────────────────────
module logicApp '../layers/logic-app/main.bicep' = {
  name: 'logic-app'
  params: {
    location: location
    teamsGroupId: teamsGroupId
    teamsChannelIds: teamsChannelIds
    serverAudience: serverAudience
    serverBaseUrl: agentServer.outputs.SERVER_BASE_URL
    botBaseUrl: frontend.outputs.BOT_BASE_URL
    botAudience: frontend.outputs.BOT_AUDIENCE
    blobStorageAccountName: sharedResources.outputs.STORAGE_ACCOUNT_NAME
    managedIdentityName: sharedResources.outputs.MANAGED_IDENTITY_NAME
    botIdentityName: frontend.outputs.BOT_IDENTITY_NAME
    functionAppName: functionApp.outputs.FUNCTION_APP_NAME
    integrationAccountNameOverride:   'azuresdkqabot-ia-${_suffix}'
    teamsConnectionNameOverride:      'teams-${_suffix}'
    azureBlobConnectionNameOverride:  'azureblob-${_suffix}'
    documentDbConnectionNameOverride: 'documentdb-${_suffix}'
    logicAppWorkflowNameOverride:     'azuresdkqabot-logicapp-${_suffix}'
    logicAppAlertNameOverride:        'azuresdkqabot-logicapp-alert-${_suffix}'
    createTeamsConnection: createTeamsConnection
  }
}

// ── Outputs consumed by azd / hooks (mirror ../main.bicep) ─────────────────────
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = sharedResources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output CONTAINER_REGISTRY_NAME string = sharedResources.outputs.CONTAINER_REGISTRY_NAME
output AZURE_RESOURCE_GROUP string = resourceGroup().name
output AZURE_LOCATION string = location

output MANAGED_IDENTITY_NAME string = sharedResources.outputs.MANAGED_IDENTITY_NAME
output MANAGED_IDENTITY_CLIENT_ID string = sharedResources.outputs.MANAGED_IDENTITY_CLIENT_ID
output MANAGED_IDENTITY_RESOURCE_ID string = sharedResources.outputs.MANAGED_IDENTITY_RESOURCE_ID
output MANAGED_IDENTITY_PRINCIPAL_ID string = sharedResources.outputs.MANAGED_IDENTITY_PRINCIPAL_ID
output STORAGE_ACCOUNT_NAME string = sharedResources.outputs.STORAGE_ACCOUNT_NAME
output STORAGE_BLOB_ENDPOINT string = sharedResources.outputs.STORAGE_BLOB_ENDPOINT
output KEY_VAULT_NAME string = sharedResources.outputs.KEY_VAULT_NAME
output APP_CONFIG_NAME string = sharedResources.outputs.APP_CONFIG_NAME
output SEARCH_SERVICE_NAME string = sharedResources.outputs.SEARCH_SERVICE_NAME
output COSMOSDB_ACCOUNT_NAME string = sharedResources.outputs.COSMOSDB_ACCOUNT_NAME
output ACTION_GROUP_NAME string = sharedResources.outputs.ACTION_GROUP_NAME

output AI_RESOURCE_NAME string = agent.outputs.AI_RESOURCE_NAME
output AI_PROJECT_NAME string = agent.outputs.AI_PROJECT_NAME

output BOT_IDENTITY_NAME string = frontend.outputs.BOT_IDENTITY_NAME
output BOT_SERVICE_NAME string = frontend.outputs.BOT_SERVICE_NAME
output BOT_ID string = frontend.outputs.BOT_ID
output BOT_MANAGED_IDENTITY_CLIENT_ID string = frontend.outputs.BOT_MANAGED_IDENTITY_CLIENT_ID
output BOT_MANAGED_IDENTITY_PRINCIPAL_ID string = frontend.outputs.BOT_MANAGED_IDENTITY_PRINCIPAL_ID
output BOT_BASE_URL string = frontend.outputs.BOT_BASE_URL
output BOT_AUDIENCE string = frontend.outputs.BOT_AUDIENCE
output BOT_TENANT_ID string = frontend.outputs.BOT_TENANT_ID

output SERVER_BASE_URL string = agentServer.outputs.SERVER_BASE_URL

output FUNCTION_APP_NAME string = functionApp.outputs.FUNCTION_APP_NAME

output SERVER_AUDIENCE string = serverAudience
output TEAMS_GROUP_ID string = teamsGroupId
output TEAMS_CHANNEL_IDS string = join(teamsChannelIds, ',')
output AGENT_SERVER_IMAGE string = '${sharedResources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT}/${agentServerImageRepository}'
output FUNCTION_CONTAINER_IMAGE string = '${sharedResources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT}/${functionImageRepository}'
