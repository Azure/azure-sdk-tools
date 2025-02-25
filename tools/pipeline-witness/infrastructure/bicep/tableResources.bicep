param location string
param logsStorageAccountName string
param eventHubNamespaceName string
param eventGridTopicName string
param kustoClusterName string
param kustoDatabaseName string
param tables array

resource logsStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: logsStorageAccountName
  resource blobServices 'blobServices' = {
    name: 'default'
  }
}

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2022-01-01-preview' existing = {
  name: eventHubNamespaceName
}

resource eventGridTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' existing = {
  name: eventGridTopicName
}

resource kustoCluster 'Microsoft.Kusto/Clusters@2022-02-01' existing = {
  name: kustoClusterName
  resource database 'Databases' existing = {
    name: kustoDatabaseName
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
}]
