param webAppName string
param appServicePlanName string
param appStorageAccountName string
param cosmosAccountName string
param location string

var cosmosContributorRoleId = '00000000-0000-0000-0000-000000000002' // Built-in Contributor role

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'P1v3'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|6.0'
    }
    httpsOnly: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Storage Account for input queue
resource appStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: appStorageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    defaultToOAuthAuthentication: false
    allowCrossTenantReplication: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }

  resource blobServices 'blobServices' = {
    name: 'default'
    properties: {
      changeFeed: {
        enabled: false
      }
      restorePolicy: {
        enabled: false
      }
      containerDeleteRetentionPolicy: {
        enabled: true
        days: 7
      }
      cors: {
        corsRules: []
      }
      deleteRetentionPolicy: {
        allowPermanentDelete: false
        enabled: true
        days: 7
      }
      isVersioningEnabled: false
    }
  }

  resource queueServices 'queueServices' = {
    name: 'default'

    resource buildCompletedQueue 'queues' = {
      name: 'azurepipelines-build-completed'
    }
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-02-15-preview' = {
  name: cosmosAccountName
  location: location
  tags: {
    defaultExperience: 'Core (SQL)'
    CosmosAccountType: 'Non-Production'
  }
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'None'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: false
    virtualNetworkRules: []
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: false
    enableAnalyticalStorage: false
    analyticalStorageConfiguration: {}
    databaseAccountOfferType: 'Standard'
    enableMaterializedViews: false
    networkAclBypass: 'None'
    disableLocalAuth: false
    enablePartitionMerge: false
    enablePerRegionPerPartitionAutoscale: false
    enableBurstCapacity: false
    enablePriorityBasedExecution: false
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    cors: []
    capabilities: []
    ipRules: []
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Geo'
      }
    }
    networkAclBypassResourceIds: []
    diagnosticLogSettings: {
      enableFullTextQuery: 'None'
    }
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-02-15-preview' = {
  parent: cosmosAccount
  name: 'records'
  properties: {
    resource: {
      id: 'records'
    }
  }
}

resource pipelineRunsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-02-15-preview' = {
  parent: cosmosDatabase
  name: 'azure-pipelines-runs'
  properties: {
    resource: {
      id: 'azure-pipelines-runs'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
}

resource locksContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-02-15-preview' = {
  parent: cosmosDatabase
  name: 'locks'
  properties: {
    resource: {
      id: 'locks'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
  }
}

// Assign Storage Queue Data Contributor role for the Web App on the Queue Storage Account
resource queueContributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  // This is the Storage Blob Data Contributor role, which is the minimum role permission we can give.
  // See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage
  name: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
}

resource queueRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(queueContributorRoleDefinition.id, webAppName, appStorageAccount.id)
  scope: appStorageAccount
  properties:{
    principalId: webApp.identity.principalId
    roleDefinitionId: queueContributorRoleDefinition.id
    description: 'Queue Contributor for PipelineWitness'
  }
}

// Assign CosmosDB Contributor role for the Web App on the Cosmos Account
resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = {
  name: guid(cosmosContributorRoleId, webAppName, cosmosAccount.id)
  parent: cosmosAccount
  properties:{
    principalId: webApp.identity.principalId
    roleDefinitionId: '${resourceGroup().id}/providers/Microsoft.DocumentDB/databaseAccounts/${cosmosAccount.name}/sqlRoleDefinitions/${cosmosContributorRoleId}'
    scope: cosmosAccount.id
  }
}

output appIdentityPrincipalId string = webApp.identity.principalId
