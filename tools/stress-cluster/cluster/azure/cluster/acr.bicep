param registryName string
param location string = resourceGroup().location
param objectIds array

resource registry 'Microsoft.ContainerRegistry/registries@2019-12-01-preview' = {
  name: registryName
  location: location
  tags: {
    displayName: 'Stress Test Container Registry'
  }
  sku: {
    name: 'Basic'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    adminUserEnabled: false
  }
}

// Add AcrPush and AcrPull roles to access groups
resource acrPushRole 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for objectId in objectIds: {
  name: '${guid('azureContainerRegistryPushRole', objectId, resourceGroup().id)}'
  scope: registry
  properties: {
    roleDefinitionId: '${subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')}'
    principalId: objectId
  }
}]

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = [for objectId in objectIds: {
  name: '${guid('azureContainerRegistryPullRole', objectId, resourceGroup().id)}'
  scope: registry
  properties: {
    roleDefinitionId: '${subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')}'
    principalId: objectId
  }
}]

output containerRegistryName string = registry.name
