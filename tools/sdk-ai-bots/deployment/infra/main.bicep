// ─────────────────────────────────────────────────────────────────────────────
// Main azd orchestration template for the sdk-ai-bots deployment.
//
// Per-env defaults (location, resource group) come from the parameters
// files in environments/*.parameters.json, consumed by either
// `azd provision` or `az deployment sub create`.
//
// Layer order (encoded by module dependsOn):
//   1. shared-resources  →  2. agent  →  3. frontend  →  4. backend
//                                                ↓
//                                       5. function-app  →  6. logic-app
// ─────────────────────────────────────────────────────────────────────────────

targetScope = 'subscription'

@description('Azure region for all resources.')
param location string = 'westus2'

@description('Name of the resource group to deploy into.')
param resourceGroupName string

@description('Teams team (group) ID the Logic App monitors.')
param teamsGroupId string

@description('Teams channel IDs the Logic App subscribes to.')
param teamsChannelIds array

@description('Client ID (audience) for authenticating to the agent server. External Entra app registration.')
param serverAudience string

@description('Function App container image repository and tag.')
param functionImageRepository string

@description('Backend container image repository and tag.')
param ragBasedBackendImageRepository string

@description('Agent server (slot) container image repository and tag.')
param agentBasedImageRepository string

@description('Frontend (Teams bot) container image repository and tag.')
param frontendImageRepository string

// ── Optional per-env overrides for locations and resource names ──────────────
// All defaults reproduce the current dev/preview generated names. Prod's
// pre-existing RG has different naming (e.g. `azuresdkqabotstorage` instead of
// `qabotstorage<hash>`), so `environments/prod.parameters.json` overrides each
// of these to attach Bicep to the existing resources without a rename.

@description('Azure region for the Cognitive Services / Foundry AI resources. Defaults to `location`; override when model availability requires a different region (prod uses `swedencentral`).')
param aiLocation string = location

// Shared resources
param managedIdentityName string = ''
param actionGroupName string = ''
param keyVaultName string = ''
param appConfigName string = ''
param searchServiceName string = ''
param containerRegistryName string = ''
param storageAccountName string = ''
param cosmosDbAccountName string = ''

// Agent
param aiResourceName string = ''
param aiProjectName string = ''
param agentLogWorkspaceName string = ''
param agentAppInsightsName string = ''

// Frontend
param frontendBaseName string = ''
param frontendAppInsightsName string = ''
param frontendEmailActionGroupName string = ''
param frontendDiagnosticSettingName string = ''
param frontendHealthTestName string = ''
param frontendServerErrorsAlertName string = ''
param frontendHealthCheckAlertName string = ''
param frontendDeleteLockName string = ''

// Backend
param backendAppServicePlanName string = ''
param backendLogWorkspaceName string = ''
param backendSiteName string = ''
param backendSlotAppInsightsName string = ''
param backendAlertName string = ''
param backendAgentAlertName string = ''

// Function App
param functionAppServicePlanName string = ''
param functionLogWorkspaceName string = ''
param functionAppName string = ''

// Logic App
param integrationAccountName string = ''
param teamsConnectionName string = ''
param azureBlobConnectionName string = ''
param documentDbConnectionName string = ''
param logicAppWorkflowName string = ''
param logicAppAlertName string = ''

// ── Resource Group ─────────────────────────────────────────────────────────────
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// Shared suffix reproducing the module-internal `substring(uniqueString(resourceGroup().id), 0, 6)`
// pattern so the default names main.bicep passes to modules match what modules
// would generate on their own.
var _suffix = substring(uniqueString(rg.id), 0, 6)

// ── Layer 1: Shared resources ──────────────────────────────────────────────────
module sharedResources './modules/qaBotSharedResources/sharedResources.bicep' = {
  name: 'shared-resources'
  scope: rg
  params: {
    managedIdentityName:   !empty(managedIdentityName)   ? managedIdentityName   : 'qabot-identity-${_suffix}'
    actionGroupName:       !empty(actionGroupName)       ? actionGroupName       : 'qabot-alert-${_suffix}'
    keyVaultName:          !empty(keyVaultName)          ? keyVaultName          : 'qabot-keyvault-${_suffix}'
    appConfigName:         !empty(appConfigName)         ? appConfigName         : 'qabot-config-${_suffix}'
    searchServiceName:     !empty(searchServiceName)     ? searchServiceName     : 'qabot-search-${_suffix}'
    containerRegistryName: !empty(containerRegistryName) ? containerRegistryName : 'qabotcontainer${_suffix}'
    storageAccountName:    !empty(storageAccountName)    ? storageAccountName    : 'qabotstorage${_suffix}'
    cosmosDbAccountName:   !empty(cosmosDbAccountName)   ? cosmosDbAccountName   : 'qabot-db-${_suffix}'
  }
}

// ── Layer 2: Agent / AI services ───────────────────────────────────────────────
module agent './modules/qaBotAgent/component.bicep' = {
  name: 'agent'
  scope: rg
  params: {
    location: aiLocation
    managedIdentityPrincipalId: sharedResources.outputs.managedIdentityPrincipalId
    storageAccountName: sharedResources.outputs.storageAccountName
    storageBlobEndpoint: sharedResources.outputs.storageBlobEndpoint
    aiResourceName:         !empty(aiResourceName)         ? aiResourceName         : 'qabot-ai-resource-${_suffix}'
    aiProjectName:          !empty(aiProjectName)          ? aiProjectName          : 'qabot-ai'
    agentLogWorkspaceName:  !empty(agentLogWorkspaceName)  ? agentLogWorkspaceName  : 'qabot-agent-log-${_suffix}'
    agentAppInsightsName:   !empty(agentAppInsightsName)   ? agentAppInsightsName   : 'qabot-agent-${_suffix}'
  }
}

// ── Layer 3: Frontend (Teams Bot) ──────────────────────────────────────────────
module frontend './modules/qaBotFrontend/userAssignedIdentity.bicep' = {
  name: 'frontend'
  scope: rg
  params: {
    storageAccountName: sharedResources.outputs.storageAccountName
    containerRegistryName: sharedResources.outputs.containerRegistryName
    frontendImageRepository: frontendImageRepository
    frontendBaseName:               !empty(frontendBaseName)               ? frontendBaseName               : 'azsdkqabot-${_suffix}'
    frontendAppInsightsName:        !empty(frontendAppInsightsName)        ? frontendAppInsightsName        : 'azsdkqabot-insights-${_suffix}'
    frontendEmailActionGroupName:   !empty(frontendEmailActionGroupName)   ? frontendEmailActionGroupName   : 'azsdkqabot-email-alerts-${_suffix}'
    frontendDiagnosticSettingName:  !empty(frontendDiagnosticSettingName)  ? frontendDiagnosticSettingName  : 'azsdkqabot-diagnostic-${_suffix}'
    frontendHealthTestName:         !empty(frontendHealthTestName)         ? frontendHealthTestName         : 'azsdkqabot-health-test-${_suffix}'
    frontendServerErrorsAlertName:  !empty(frontendServerErrorsAlertName)  ? frontendServerErrorsAlertName  : 'azsdkqabot-server-errors-${_suffix}'
    frontendHealthCheckAlertName:   !empty(frontendHealthCheckAlertName)   ? frontendHealthCheckAlertName   : 'azsdkqabot-health-check-failure-${_suffix}'
    frontendDeleteLockName:         !empty(frontendDeleteLockName)         ? frontendDeleteLockName         : 'azsdkqabot-delete-lock-${_suffix}'
  }
}

// ── Layer 4: Backend ───────────────────────────────────────────────────────────
module backend './modules/qaBotBackend/serverfarm.bicep' = {
  name: 'backend'
  scope: rg
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
    backendAppServicePlanName:  !empty(backendAppServicePlanName)  ? backendAppServicePlanName  : 'azuresdkqabot-appserviceplan-${_suffix}'
    backendLogWorkspaceName:    !empty(backendLogWorkspaceName)    ? backendLogWorkspaceName    : 'azuresdkqabot-log-${_suffix}'
    backendSiteName:            !empty(backendSiteName)            ? backendSiteName            : 'azuresdkqabot-server-${_suffix}'
    backendSlotAppInsightsName: !empty(backendSlotAppInsightsName) ? backendSlotAppInsightsName : 'azuresdkqabot-server202510300250-${_suffix}'
    backendAlertName:           !empty(backendAlertName)           ? backendAlertName           : 'azuresdkqabot-alert-${_suffix}'
    backendAgentAlertName:      !empty(backendAgentAlertName)      ? backendAgentAlertName      : 'azuresdkqabot-agent-alert-${_suffix}'
  }
}

// ── Layer 5: Function App ──────────────────────────────────────────────────────
module functionApp './modules/qaBotFunctionApp/serverfarm.bicep' = {
  name: 'function-app'
  scope: rg
  params: {
    location: location
    containerImage: '${sharedResources.outputs.containerRegistryLoginServer}/${functionImageRepository}'
    managedIdentityClientId: sharedResources.outputs.managedIdentityClientId
    managedIdentityResourceId: sharedResources.outputs.managedIdentityResourceId
    storageAccountName: sharedResources.outputs.storageAccountName
    functionAppServicePlanName: !empty(functionAppServicePlanName) ? functionAppServicePlanName : 'azuresdkqabot-functionserviceplan-${_suffix}'
    functionLogWorkspaceName:   !empty(functionLogWorkspaceName)   ? functionLogWorkspaceName   : 'azuresdkqabot-function-log-${_suffix}'
    functionAppName:            !empty(functionAppName)            ? functionAppName            : 'azuresdkqabot-function-${_suffix}'
  }
}

// ── Layer 6: Logic App ─────────────────────────────────────────────────────────
module logicApp './modules/qaBotLogicApp/logicAppResources.bicep' = {
  name: 'logic-app'
  scope: rg
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
    integrationAccountName:  !empty(integrationAccountName)  ? integrationAccountName  : 'azuresdkqabot-ia-${_suffix}'
    teamsConnectionName:     !empty(teamsConnectionName)     ? teamsConnectionName     : 'teams-${_suffix}'
    azureBlobConnectionName: !empty(azureBlobConnectionName) ? azureBlobConnectionName : 'azureblob-${_suffix}'
    documentDbConnectionName:!empty(documentDbConnectionName)? documentDbConnectionName: 'documentdb-${_suffix}'
    logicAppWorkflowName:    !empty(logicAppWorkflowName)    ? logicAppWorkflowName    : 'azuresdkqabot-logicapp-${_suffix}'
    logicAppAlertName:       !empty(logicAppAlertName)       ? logicAppAlertName       : 'azuresdkqabot-logicapp-alert-${_suffix}'
  }
}

// ── Outputs consumed by azd / hooks ────────────────────────────────────────────
// Everything downstream layers need for standalone deploys (via
// postprovision's runLayerPipeline → az deployment group create) is exposed
// here so azd persists it into .azure/<env>/.env after `azd provision`.
output CONTAINER_REGISTRY_LOGIN_SERVER string = sharedResources.outputs.containerRegistryLoginServer
output CONTAINER_REGISTRY_NAME string = sharedResources.outputs.containerRegistryName
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_LOCATION string = location

// Shared-resources outputs
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

// Agent-platform outputs
output AI_RESOURCE_NAME string = agent.outputs.aiResourceName
output AI_PROJECT_NAME string = agent.outputs.aiProjectName

// Frontend outputs
output BOT_IDENTITY_NAME string = frontend.outputs.botIdentityName
output BOT_BASE_URL string = frontend.outputs.botBaseUrl
output BOT_AUDIENCE string = frontend.outputs.botAudience

// Backend outputs
output SERVER_BASE_URL string = backend.outputs.serverBaseUrl

// Function-app outputs
output FUNCTION_APP_NAME string = functionApp.outputs.functionAppName

// Inputs re-exported so standalone module deploys can source them from env
output SERVER_AUDIENCE string = serverAudience
output TEAMS_GROUP_ID string = teamsGroupId
output TEAMS_CHANNEL_IDS string = join(teamsChannelIds, ',')
output RAG_BASED_BACKEND_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${ragBasedBackendImageRepository}'
output AGENT_BASED_BACKEND_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${agentBasedImageRepository}'
output FUNCTION_CONTAINER_IMAGE string = '${sharedResources.outputs.containerRegistryLoginServer}/${functionImageRepository}'
