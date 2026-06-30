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

// ── Resource Group ─────────────────────────────────────────────────────────────
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

// ── Layer 1: Shared resources ──────────────────────────────────────────────────
module sharedResources './modules/qaBotSharedResources/sharedResources.bicep' = {
  name: 'shared-resources'
  scope: rg
}

// ── Layer 2: Agent / AI services ───────────────────────────────────────────────
module agent './modules/qaBotAgent/component.bicep' = {
  name: 'agent'
  scope: rg
  params: {
    managedIdentityPrincipalId: sharedResources.outputs.managedIdentityPrincipalId
    storageAccountName: sharedResources.outputs.storageAccountName
    storageBlobEndpoint: sharedResources.outputs.storageBlobEndpoint
  }
}

// ── Layer 3: Frontend (Teams Bot) ──────────────────────────────────────────────
module frontend './modules/qaBotFrontend/userAssignedIdentity.bicep' = {
  name: 'frontend'
  scope: rg
  dependsOn: [sharedResources]
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
  }
}

// ── Outputs consumed by azd / hooks ────────────────────────────────────────────
output CONTAINER_REGISTRY_LOGIN_SERVER string = sharedResources.outputs.containerRegistryLoginServer
output CONTAINER_REGISTRY_NAME string = sharedResources.outputs.containerRegistryName
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_LOCATION string = location
