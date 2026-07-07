// ─────────────────────────────────────────────────────────────────────────────
// ADE entry template — RESOURCE-GROUP scoped.
//
// Azure Deployment Environments (ADE) creates and owns a resource group per
// environment, then deploys this template INTO it. That is why this file is
// `targetScope = 'resourceGroup'` and does NOT create a resource group (unlike
// the subscription-scoped ../main.bicep used by azd / az deployment sub).
//
// It wires the same six layer modules as ../main.bicep, but always lets
// resource names auto-generate from `uniqueString(resourceGroup().id)` — which
// is exactly what ephemeral per-PR environments want (unique, collision-free
// names, no prod name-override parameters).
//
// ┌───────────────────────────────────────────────────────────────────────────┐
// │ SOURCE OF TRUTH: ../main.bicep                                             │
// │ The inter-layer module wiring here MUST stay in sync with ../main.bicep.   │
// │ If you add/rename a module input there, mirror it here.                    │
// └───────────────────────────────────────────────────────────────────────────┘
//
// Layer order (encoded by module references):
//   1. shared-resources → 2. agent → 3. frontend → 4. backend
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

@description('Teams team (group) ID the Logic App monitors.')
param teamsGroupId string

@description('Teams channel IDs the Logic App subscribes to.')
param teamsChannelIds array

@description('Frontend (Teams bot) container image repository and tag.')
param frontendImageRepository string

@description('Backend (RAG) container image repository and tag.')
param ragBasedBackendImageRepository string

@description('Agent server (slot) container image repository and tag.')
param agentBasedImageRepository string

@description('Function App container image repository and tag.')
param functionImageRepository string

// Unique suffix — same pattern as ../main.bicep so generated names are stable
// per environment (resource group) and collision-free across PR environments.
var _suffix = substring(uniqueString(resourceGroup().id), 0, 6)

// ── Layer 1: Shared resources ──────────────────────────────────────────────────
module sharedResources '../modules/qaBotSharedResources/sharedResources.bicep' = {
  name: 'shared-resources'
  params: {
    location:              location
    managedIdentityName:   'qabot-identity-${_suffix}'
    actionGroupName:       'qabot-alert-${_suffix}'
    keyVaultName:          'qabot-keyvault-${_suffix}'
    appConfigName:         'qabot-config-${_suffix}'
    searchServiceName:     'qabot-search-${_suffix}'
    containerRegistryName: 'qabotcontainer${_suffix}'
    storageAccountName:    'qabotstorage${_suffix}'
    cosmosDbAccountName:   'qabot-db-${_suffix}'
  }
}

// ── Layer 2: Agent / AI services ───────────────────────────────────────────────
module agent '../modules/qaBotAgent/component.bicep' = {
  name: 'agent'
  params: {
    location: aiLocation
    managedIdentityPrincipalId: sharedResources.outputs.managedIdentityPrincipalId
    storageAccountName: sharedResources.outputs.storageAccountName
    storageBlobEndpoint: sharedResources.outputs.storageBlobEndpoint
    aiResourceName:        'qabot-ai-resource-${_suffix}'
    aiProjectName:         'qabot-ai'
    agentLogWorkspaceName: 'qabot-agent-log-${_suffix}'
    agentAppInsightsName:  'qabot-agent-${_suffix}'
  }
}

// ── Layer 3: Frontend (Teams Bot) ──────────────────────────────────────────────
module frontend '../modules/qaBotFrontend/userAssignedIdentity.bicep' = {
  name: 'frontend'
  params: {
    storageAccountName: sharedResources.outputs.storageAccountName
    containerRegistryName: sharedResources.outputs.containerRegistryName
    frontendImageRepository: frontendImageRepository
    frontendBaseName:              'azsdkqabot-${_suffix}'
    frontendAppInsightsName:       'azsdkqabot-insights-${_suffix}'
    frontendEmailActionGroupName:  'azsdkqabot-email-alerts-${_suffix}'
    frontendDiagnosticSettingName: 'azsdkqabot-diagnostic-${_suffix}'
    frontendHealthTestName:        'azsdkqabot-health-test-${_suffix}'
    frontendServerErrorsAlertName: 'azsdkqabot-server-errors-${_suffix}'
    frontendHealthCheckAlertName:  'azsdkqabot-health-check-failure-${_suffix}'
    frontendDeleteLockName:        'azsdkqabot-delete-lock-${_suffix}'
  }
}

// ── Layer 4: Backend ───────────────────────────────────────────────────────────
module backend '../modules/qaBotBackend/serverfarm.bicep' = {
  name: 'backend'
  params: {
    location: location
    ragBasedBackendImage: '${sharedResources.outputs.containerRegistryLoginServer}/${ragBasedBackendImageRepository}'
    agentBasedBackendImage: '${sharedResources.outputs.containerRegistryLoginServer}/${agentBasedImageRepository}'
    managedIdentityClientId: sharedResources.outputs.managedIdentityClientId
    serverAudience: serverAudience
    sharedIdentityName: sharedResources.outputs.managedIdentityName
    frontendIdentityName: frontend.outputs.botIdentityName
    aiResourceName: agent.outputs.aiResourceName
    aiProjectName: agent.outputs.aiProjectName
    searchServiceName: sharedResources.outputs.searchServiceName
    cosmosDbAccountName: sharedResources.outputs.cosmosDbAccountName
    storageAccountName: sharedResources.outputs.storageAccountName
    keyVaultName: sharedResources.outputs.keyVaultName
    appConfigName: sharedResources.outputs.appConfigName
    actionGroupName: sharedResources.outputs.actionGroupName
    backendAppServicePlanName:  'azuresdkqabot-appserviceplan-${_suffix}'
    backendLogWorkspaceName:    'azuresdkqabot-log-${_suffix}'
    backendSiteName:            'azuresdkqabot-server-${_suffix}'
    backendSlotAppInsightsName: 'azuresdkqabot-server202510300250-${_suffix}'
    backendAlertName:           'azuresdkqabot-alert-${_suffix}'
    backendAgentAlertName:      'azuresdkqabot-agent-alert-${_suffix}'
  }
}

// ── Layer 5: Function App ──────────────────────────────────────────────────────
module functionApp '../modules/qaBotFunctionApp/serverfarm.bicep' = {
  name: 'function-app'
  params: {
    location: location
    containerImage: '${sharedResources.outputs.containerRegistryLoginServer}/${functionImageRepository}'
    managedIdentityClientId: sharedResources.outputs.managedIdentityClientId
    managedIdentityResourceId: sharedResources.outputs.managedIdentityResourceId
    storageAccountName: sharedResources.outputs.storageAccountName
    functionAppServicePlanName: 'azuresdkqabot-functionserviceplan-${_suffix}'
    functionLogWorkspaceName:   'azuresdkqabot-function-log-${_suffix}'
    functionAppName:            'azuresdkqabot-function-${_suffix}'
  }
}

// ── Layer 6: Logic App ─────────────────────────────────────────────────────────
module logicApp '../modules/qaBotLogicApp/logicAppResources.bicep' = {
  name: 'logic-app'
  params: {
    location: location
    teamsGroupId: teamsGroupId
    teamsChannelIds: teamsChannelIds
    serverAudience: serverAudience
    serverBaseUrl: backend.outputs.serverBaseUrl
    botBaseUrl: frontend.outputs.botBaseUrl
    botAudience: frontend.outputs.botAudience
    blobStorageAccountName: sharedResources.outputs.storageAccountName
    managedIdentityName: sharedResources.outputs.managedIdentityName
    botIdentityName: frontend.outputs.botIdentityName
    functionAppName: functionApp.outputs.functionAppName
    integrationAccountName:   'azuresdkqabot-ia-${_suffix}'
    teamsConnectionName:      'teams-${_suffix}'
    azureBlobConnectionName:  'azureblob-${_suffix}'
    documentDbConnectionName: 'documentdb-${_suffix}'
    logicAppWorkflowName:     'azuresdkqabot-logicapp-${_suffix}'
    logicAppAlertName:        'azuresdkqabot-logicapp-alert-${_suffix}'
  }
}

// ── Outputs consumed by azd / hooks (mirror ../main.bicep) ─────────────────────
output CONTAINER_REGISTRY_LOGIN_SERVER string = sharedResources.outputs.containerRegistryLoginServer
output CONTAINER_REGISTRY_NAME string = sharedResources.outputs.containerRegistryName
output AZURE_RESOURCE_GROUP string = resourceGroup().name
output AZURE_LOCATION string = location

output MANAGED_IDENTITY_NAME string = sharedResources.outputs.managedIdentityName
output MANAGED_IDENTITY_CLIENT_ID string = sharedResources.outputs.managedIdentityClientId
output MANAGED_IDENTITY_RESOURCE_ID string = sharedResources.outputs.managedIdentityResourceId
output MANAGED_IDENTITY_PRINCIPAL_ID string = sharedResources.outputs.managedIdentityPrincipalId
output STORAGE_ACCOUNT_NAME string = sharedResources.outputs.storageAccountName
output STORAGE_BLOB_ENDPOINT string = sharedResources.outputs.storageBlobEndpoint
output KEY_VAULT_NAME string = sharedResources.outputs.keyVaultName
output APP_CONFIG_NAME string = sharedResources.outputs.appConfigName
output SEARCH_SERVICE_NAME string = sharedResources.outputs.searchServiceName
output COSMOSDB_ACCOUNT_NAME string = sharedResources.outputs.cosmosDbAccountName
output ACTION_GROUP_NAME string = sharedResources.outputs.actionGroupName

output AI_RESOURCE_NAME string = agent.outputs.aiResourceName
output AI_PROJECT_NAME string = agent.outputs.aiProjectName

output BOT_IDENTITY_NAME string = frontend.outputs.botIdentityName
output BOT_BASE_URL string = frontend.outputs.botBaseUrl
output BOT_AUDIENCE string = frontend.outputs.botAudience

output SERVER_BASE_URL string = backend.outputs.serverBaseUrl

output FUNCTION_APP_NAME string = functionApp.outputs.functionAppName

output SERVER_AUDIENCE string = serverAudience
output TEAMS_GROUP_ID string = teamsGroupId
output TEAMS_CHANNEL_IDS string = join(teamsChannelIds, ',')
output RAG_BASED_BACKEND_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${ragBasedBackendImageRepository}'
output AGENT_BASED_BACKEND_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${agentBasedImageRepository}'
output FUNCTION_CONTAINER_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${functionImageRepository}'
