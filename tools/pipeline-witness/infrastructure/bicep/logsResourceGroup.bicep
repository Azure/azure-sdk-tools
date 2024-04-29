param location string
param logsStorageAccountName string
param kustoClusterName string
param kustoDatabaseName string
param webAppName string
param appIdentityPrincipalId string

var tables = [
  {
    name: 'Build'
    container: 'builds'
  }
  {
    name: 'BuildDefinition'
    container: 'builddefinitions'
  }
  {
    name: 'BuildFailure'
    container: 'buildfailures'
  }
  {
    name: 'BuildLogLine'
    container: 'buildloglines'
  }
  {
    name: 'BuildTimelineRecord'
    container: 'buildtimelinerecords'
  }
  {
    name: 'PipelineOwner'
    container: 'pipelineowners'
  }
  {
    name: 'TestRun'
    container: 'testruns'
  }
  {
    name: 'TestRunResult'
    container: 'testrunresults'
  }
]

var kustoScript = loadTextContent('../artifacts/merged.kql')

// Storage Account for output blobs
resource logsStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: logsStorageAccountName
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
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-09-01' = [for table in tables: {
  parent: logsStorageAccount::blobServices
  name: table.container
  properties: {
    immutableStorageWithVersioning: {
      enabled: false
    }
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}]

// Event Grid
resource eventGridTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  name: logsStorageAccountName
  location: location
  properties: {
    source: logsStorageAccount.id
    topicType: 'microsoft.storage.storageaccounts'
  }
}

// Event Hub
resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-01-01-preview' = {
  name: logsStorageAccountName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    minimumTlsVersion: '1.0'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    zoneRedundant: false
    isAutoInflateEnabled: false
    maximumThroughputUnits: 0
    kafkaEnabled: true
  }
}

// Kusto Cluster
resource kustoCluster 'Microsoft.Kusto/Clusters@2022-02-01' = {
  name: kustoClusterName
  location: location
  sku: {
    name: 'Standard_E2a_v4'
    tier: 'Standard'
    capacity: 3
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    trustedExternalTenants: []
    optimizedAutoscale: {
      version: 1
      isEnabled: true
      minimum: 2
      maximum: 3
    }
    enableDiskEncryption: false
    enableStreamingIngest: false
    enablePurge: false
    enableDoubleEncryption: false
    engineType: 'V3'
    acceptedAudiences: []
    restrictOutboundNetworkAccess: 'Disabled'
    allowedFqdnList: []
    publicNetworkAccess: 'Enabled'
    allowedIpRangeList: []
    enableAutoStop: false
    publicIPType: 'IPv4'
  }
  resource database 'Databases' = {
    name: kustoDatabaseName
    location: location
    kind: 'ReadWrite'
    properties: {
      hotCachePeriod: 'P31D'
    }
  }
}

// Resources per table
resource kustoScriptInvocation 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = {
  name: 'intitializeDatabase'
  parent: kustoCluster::database
  properties: {
      scriptContent: kustoScript
      forceUpdateTag: uniqueString(kustoScript)
  }
}

resource eventHubs 'Microsoft.EventHub/namespaces/eventhubs@2022-01-01-preview' = [for (table, i) in tables: {
  parent: eventHubNamespace
  name: table.container
  properties: {
    messageRetentionInDays: 7
    partitionCount: 8
    status: 'Active'
  }
}]

resource eventGridSubscriptions 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2022-06-15' = [for (table, i) in tables: {
  parent: eventGridTopic
  name: table.container
  properties: {
    destination: {
      properties: {
        resourceId: eventHubs[i].id
      }
      endpointType: 'EventHub'
    }
    filter: {
      subjectBeginsWith: '/blobServices/default/containers/${table.container}'
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
    }
    eventDeliverySchema: 'EventGridSchema'
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}]

resource kustoDataConnections 'Microsoft.Kusto/Clusters/Databases/DataConnections@2022-02-01' = [for (table, i) in tables: {
  parent: kustoCluster::database
  name: '${kustoDatabaseName}-${table.container}'
  location: location
  kind: 'EventGrid'
  properties: {
    ignoreFirstRecord: false
    storageAccountResourceId: logsStorageAccount.id
    eventHubResourceId: eventHubs[i].id
    consumerGroup: '$Default'
    tableName: table.name
    mappingRuleName: '${table.name}_mapping'
    dataFormat: 'JSON'
    blobStorageEventType: 'Microsoft.Storage.BlobCreated'
    databaseRouting: 'Single'
    managedIdentityResourceId: kustoCluster.id
  }
  dependsOn: [ kustoScriptInvocation ]
}]

// Assign roles to the Kusto cluster and App Service
resource blobContributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  // This is the Storage Blob Data Contributor role.
  // Read, write, and delete Azure Storage containers and blobs
  // See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#storage
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
}

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(blobContributorRoleDefinition.id, webAppName, logsStorageAccount.id)
  scope: logsStorageAccount
  properties:{
    principalId: appIdentityPrincipalId
    roleDefinitionId: blobContributorRoleDefinition.id
    description: 'Blob Contributor for PipelineWitness'
  }
}

resource kustoStorageAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(blobContributorRoleDefinition.id, kustoClusterName, logsStorageAccount.id)
  scope: logsStorageAccount
  properties:{
    principalId: kustoCluster.identity.principalId
    roleDefinitionId: blobContributorRoleDefinition.id
    description: 'Blob Contributor for Kusto ingestion'
  }
}

resource eventHubsDataReceiverRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  // This is the Event Hubs Data Receiver role
  // Allows receive access to Azure Event Hubs resources.
  // see https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#analytics
  name: 'a638d3c7-ab3a-418d-83e6-5f17a39d4fde'
}

resource kustoEventHubsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(blobContributorRoleDefinition.id, kustoClusterName, eventHubNamespace.id)
  scope: eventHubNamespace
  properties:{
    principalId: kustoCluster.identity.principalId
    roleDefinitionId: eventHubsDataReceiverRoleDefinition.id
    description: 'Blob Contributor for Kusto ingestion'
  }
}
