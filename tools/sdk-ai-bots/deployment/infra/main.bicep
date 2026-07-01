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

@description('Environment name (dev | preview | prod). Suffix on resource names for multi-env deployability.')
param envName string

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

// ── Resource Group ─────────────────────────────────────────────────────────────
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// ── Layer 1: Shared resources ──────────────────────────────────────────────────
module sharedResources './modules/qaBotSharedResources/sharedResources.bicep' = {
  name: 'shared-resources'
  scope: rg
  params: {
    envName: envName
  }
}

// ── Layer 2: Agent / AI services ───────────────────────────────────────────────
module agent './modules/qaBotAgent/component.bicep' = {
  name: 'agent'
  scope: rg
  params: {
    envName: envName
    managedIdentityPrincipalId: sharedResources.outputs.managedIdentityPrincipalId
    storageAccountName: sharedResources.outputs.storageAccountName
    storageBlobEndpoint: sharedResources.outputs.storageBlobEndpoint
  }
}

// ── Layer 3: Frontend (Teams Bot) ──────────────────────────────────────────────
module frontend './modules/qaBotFrontend/userAssignedIdentity.bicep' = {
  name: 'frontend'
  scope: rg
  params: {
    envName: envName
  }
  dependsOn: [sharedResources]
}

// ── Layer 4: Backend ───────────────────────────────────────────────────────────
module backend './modules/qaBotBackend/serverfarm.bicep' = {
  name: 'backend'
  scope: rg
  params: {
    envName: envName
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
  }
}

// ── Layer 5: Function App ──────────────────────────────────────────────────────
module functionApp './modules/qaBotFunctionApp/serverfarm.bicep' = {
  name: 'function-app'
  scope: rg
  params: {
    envName: envName
    location: location
    containerImage: '${sharedResources.outputs.containerRegistryLoginServer}/${functionImageRepository}'
    managedIdentityClientId: sharedResources.outputs.managedIdentityClientId
    managedIdentityResourceId: sharedResources.outputs.managedIdentityResourceId
    storageAccountName: sharedResources.outputs.storageAccountName
  }
}

// ── Layer 6: Logic App ─────────────────────────────────────────────────────────
module logicApp './modules/qaBotLogicApp/logicAppResources.bicep' = {
  name: 'logic-app'
  scope: rg
  params: {
    envName: envName
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
