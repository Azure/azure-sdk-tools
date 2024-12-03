param name string
param location string = resourceGroup().location
param principalId string
param containers array = [
  {
    name: 'originals'
  }
  {
    name: 'codefiles'
  }
  {
    name: 'usagesamples'
  }
]

resource storage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: name
  location: location
  sku: { name: 'Standard_LRS' }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowCrossTenantReplication: true
    allowSharedKeyAccess: true
    defaultToOAuthAuthentication: false
    dnsEndpointType: 'Standard'
    minimumTlsVersion: 'TLS1_2'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }

  resource blobServices 'blobServices' = if (!empty(containers)) {
    name: 'default'
    properties: {
      deleteRetentionPolicy: {}
    }
    resource container 'containers' = [
      for container in containers: {
        name: container.name
        properties: {
          publicAccess: container.?publicAccess ?? 'None'
        }
      }
    ]
  }
  kind: 'StorageV2'
}

var contributorRoleDefinitionId = '/providers/Microsoft.Authorization/roleDefinitions/ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource blobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid('blob-rbac', storage.id, resourceGroup().id, principalId, contributorRoleDefinitionId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
    )
    principalId: principalId
  }
}

output name string = storage.name
output primaryEndpoints object = storage.properties.primaryEndpoints
