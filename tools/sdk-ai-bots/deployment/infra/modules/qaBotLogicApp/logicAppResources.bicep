targetScope = 'resourceGroup'

@description('Azure region for the Logic App and managed API connections.')
param location string

@description('Teams team (group) ID to monitor.')
param teamsGroupId string

@description('Teams channel IDs to subscribe to.')
param teamsChannelIds array

@description('Base URL of the agent server.')
param serverBaseUrl string

@description('Client ID (audience) for authenticating to the agent server.')
param serverAudience string

@description('Base URL of the bot.')
param botBaseUrl string

@description('Client ID (audience) for authenticating to the bot.')
param botAudience string

@description('Storage account name holding the channel config blob.')
param blobStorageAccountName string

@description('Name of the agent server / blob managed identity.')
param managedIdentityName string

@description('Name of the bot managed identity.')
param botIdentityName string

@description('Name of the Function App hosting convertActivity.')
param functionAppName string


// Resource IDs the workflow authenticates with via managed identity. Computed
// from names so the same module resolves correctly in any subscription / RG.
var serverIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', managedIdentityName)
var botIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', botIdentityName)
var blobIdentityResourceId = serverIdentityResourceId
var functionAppResourceId = resourceId('Microsoft.Web/sites', functionAppName)

resource integrationAccount 'Microsoft.Logic/integrationAccounts@2019-05-01' = {
  name: 'azuresdkqabot-ia-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

resource teamsConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: 'teams-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    displayName: 'teams'
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/teams'
    }
    parameterValues: {}
  }
}

resource blobConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: 'azureblob-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    displayName: 'azureblob'
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/azureblob'
    }
    parameterValues: {
      accountName: blobStorageAccountName
      accessKey: ''
    }
  }
}

resource documentDbConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: 'documentdb-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    displayName: 'documentdb'
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/documentdb'
    }
    parameterValues: {}
  }
}

resource workflow 'Microsoft.Logic/workflows@2019-05-01' = {
  name: 'azuresdkqabot-logicapp-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${botIdentityResourceId}': {}
      '${serverIdentityResourceId}': {}
    }
  }
  properties: {
    state: 'Enabled'
    integrationAccount: {
      id: integrationAccount.id
    }
    // The workflow definition is environment-agnostic and stored separately so
    // it can be edited / diffed independently and reused across environments.
    // Every environment-specific value is injected below via workflow
    // parameters (definition.parameters <- properties.parameters), never baked
    // into the definition body.
    definition: loadJsonContent('./workflowDefinition.json')
    parameters: {
      '$connections': {
        value: {
          '${blobConnection.name}': {
            connectionId: blobConnection.id
            connectionName: blobConnection.name
            connectionProperties: {
              authentication: {
                identity: blobIdentityResourceId
                type: 'ManagedServiceIdentity'
              }
            }
            id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/azureblob'
          }
          '${documentDbConnection.name}': {
            connectionId: documentDbConnection.id
            connectionName: documentDbConnection.name
            connectionProperties: {
              authentication: {
                identity: serverIdentityResourceId
                type: 'ManagedServiceIdentity'
              }
            }
            id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/documentdb'
          }
          '${teamsConnection.name}': {
            connectionId: teamsConnection.id
            connectionName: teamsConnection.name
            connectionProperties: {}
            id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/teams'
          }
        }
      }
      teamsGroupId: {
        value: teamsGroupId
      }
      teamsChannelIds: {
        value: teamsChannelIds
      }
      serverBaseUrl: {
        value: serverBaseUrl
      }
      serverAudience: {
        value: serverAudience
      }
      serverIdentityResourceId: {
        value: serverIdentityResourceId
      }
      botBaseUrl: {
        value: botBaseUrl
      }
      botAudience: {
        value: botAudience
      }
      botIdentityResourceId: {
        value: botIdentityResourceId
      }
      functionAppResourceId: {
        value: functionAppResourceId
      }
      blobStorageAccountName: {
        value: blobStorageAccountName
      }
      blobConnectionName: {
        value: blobConnection.name
      }
    }
  }
}

resource metricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: 'azuresdkqabot-logicapp-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: 'global'
  properties: {
    severity: 3
    enabled: true
    scopes: [
      workflow.id
    ]
    evaluationFrequency: 'PT1M'
    autoMitigate: true
    targetResourceType: 'Microsoft.Logic/workflows'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: resourceId('Microsoft.Insights/actionGroups', 'azuresdkqabot-alert')
        webHookProperties: {}
      }
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          operator: 'GreaterThan'
          threshold: 0
          name: 'Metric1'
          metricNamespace: 'Microsoft.Logic/workflows'
          metricName: 'RunsFailed'
          dimensions: []
          timeAggregation: 'Total'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
  }
}
