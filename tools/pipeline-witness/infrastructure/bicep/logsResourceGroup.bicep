param location string
param logsStorageAccountName string
param devOpsEventHubNamespaceName string
param gitHubEventHubNamespaceName string
param kustoClusterName string
param kustoDatabaseName string
param webAppName string
param subnetId string
param appIdentityPrincipalId string
param useVnet bool
param forceUpdateTag string = utcNow()

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
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    networkAcls: useVnet
      ? {
        bypass: 'AzureServices'
        virtualNetworkRules: [{ id: subnetId }]
        defaultAction: 'Deny'
      }
      : null
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
resource devOpsEventHubNamespace 'Microsoft.EventHub/namespaces@2022-01-01-preview' = {
  name: devOpsEventHubNamespaceName
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

// Event Hub
resource gitHubEventHubNamespace 'Microsoft.EventHub/namespaces@2022-01-01-preview' = {
  name: gitHubEventHubNamespaceName
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
  tags: {
    'NRMS.KustoPlatform.Classification.1P': 'Corp'
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

  resource managedEndpoint 'managedPrivateEndpoints' = if(useVnet) {
    name: logsStorageAccountName
    properties: {
      groupId: 'blob'
      privateLinkResourceId: logsStorageAccount.id
      requestMessage: ''
    }
  }
}

// Resources per table
resource kustoScriptInvocation 'Microsoft.Kusto/clusters/databases/scripts@2022-02-01' = {
  name: 'intitializeDatabase'
  parent: kustoCluster::database
  properties: {
      scriptContent: loadTextContent('../artifacts/merged.kql')
      forceUpdateTag: forceUpdateTag
  }
}

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

resource devOpsKustoEventHubsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(eventHubsDataReceiverRoleDefinition.id, kustoClusterName, devOpsEventHubNamespace.id)
  scope: devOpsEventHubNamespace
  properties:{
    principalId: kustoCluster.identity.principalId
    roleDefinitionId: eventHubsDataReceiverRoleDefinition.id
    description: 'Blob Contributor for Kusto ingestion'
  }
}

resource gitHubKustoEventHubsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' =  {
  name: guid(eventHubsDataReceiverRoleDefinition.id, kustoClusterName, gitHubEventHubNamespace.id)
  scope: gitHubEventHubNamespace
  properties:{
    principalId: kustoCluster.identity.principalId
    roleDefinitionId: eventHubsDataReceiverRoleDefinition.id
    description: 'Blob Contributor for Kusto ingestion'
  }
}

// Data Explorer needs to a per-table cursor when importing data. Because the read cursor for Event Hubs is the
// consumer group and the basic tier for event hubs is limited to 1 consumer group per event hub and 10 event hubs per
// namespace, we need an event hub per table, so we split our tables across two namespaces.
// https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-quotas
module devOpsTables 'tableResources.bicep' = {
  name: '${deployment().name}-devOpsTables'
  scope: resourceGroup()
  dependsOn:[ kustoScriptInvocation ]
  params: {
    location: location
    logsStorageAccountName: logsStorageAccountName
    eventHubNamespaceName: devOpsEventHubNamespace.name
    eventGridTopicName: eventGridTopic.name
    kustoClusterName: kustoCluster.name
    kustoDatabaseName: kustoCluster::database.name
    tables: [
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
  }
}

module gitHubTables 'tableResources.bicep' = {
  name: '${deployment().name}-gitHubTables'
  scope: resourceGroup()
  dependsOn:[ kustoScriptInvocation ]
  params: {
    location: location
    logsStorageAccountName: logsStorageAccountName
    eventHubNamespaceName: gitHubEventHubNamespace.name
    eventGridTopicName: eventGridTopic.name
    kustoClusterName: kustoCluster.name
    kustoDatabaseName: kustoCluster::database.name
    tables: [
      {
        name: 'GitHubActionsRun'
        container: 'githubactionsruns'
      }
      {
        name: 'GitHubActionsJob'
        container: 'githubactionsjobs'
      }
      {
        name: 'GitHubActionsStep'
        container: 'githubactionssteps'
      }
      {
        name: 'GitHubActionsLogLine'
        container: 'githubactionslogs'
      }
    ]
  }
}
