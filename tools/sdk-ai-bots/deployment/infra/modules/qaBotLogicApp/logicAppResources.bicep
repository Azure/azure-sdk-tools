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

@description('When false, deploy the workflow with an empty definition (no triggers or actions). This lets `azd provision` create the workflow resource — with its identity, connections, and parameters — before the Function App container image is pushed. ARM validates the real definition against the running Function App runtime (to resolve `convertActivity`), which returns 503 until the container is live, so the full definition is applied afterwards by hooks/function-postdeploy.ts via an ARM PATCH.')
param includeWorkflowDefinition bool = false

// Resource-name overrides — see qaBotSharedResources/sharedResources.bicep.
@description('Name of the Logic Apps integration account.')
param integrationAccountName string = 'azuresdkqabot-ia-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the Teams managed API connection.')
param teamsConnectionName string = 'teams-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the Azure Blob managed API connection.')
param azureBlobConnectionName string = 'azureblob-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the Cosmos DB (documentdb) managed API connection.')
param documentDbConnectionName string = 'documentdb-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the Logic App workflow.')
param logicAppWorkflowName string = 'azuresdkqabot-logicapp-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the metric alert on the Logic App workflow.')
param logicAppAlertName string = 'azuresdkqabot-logicapp-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the action group receiving Logic App failure alerts. Created by qaBotSharedResources/sharedResources.bicep and passed through from main.bicep.')
param actionGroupName string = 'qabot-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'


// Resource IDs the workflow authenticates with via managed identity. Computed
// from names so the same module resolves correctly in any subscription / RG.
var serverIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', managedIdentityName)
var botIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', botIdentityName)
var blobIdentityResourceId = serverIdentityResourceId
var functionAppResourceId = resourceId('Microsoft.Web/sites', functionAppName)

// Logic App action `function.id` must be a literal, fully-qualified resource
// ID at deploy time — ARM validation rejects runtime `@{parameters(...)}`
// expressions there. Substitute the value into the raw JSON so validation
// sees a concrete ID while the rest of the definition stays parameterized.
var workflowDefinitionText = replace(
  loadTextContent('./workflowDefinition.json'),
  '@{parameters(\'functionAppResourceId\')}',
  functionAppResourceId
)

// Minimal skeleton used when includeWorkflowDefinition is false. The workflow
// resource still needs a valid definition schema, but with no actions there is
// nothing for ARM to validate against the Function App runtime.
var emptyWorkflowDefinition = {
  '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
  contentVersion: '1.0.0.0'
  triggers: {}
  actions: {}
  outputs: {}
}

// The workflow's `properties.parameters` values must match the parameter
// declarations inside the definition body. When we deploy the empty
// definition, we must also send an empty parameters map to avoid
// "parameter X is not declared in the definition" errors.
var workflowParameters = {
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

resource integrationAccount 'Microsoft.Logic/integrationAccounts@2019-05-01' = {
  name: integrationAccountName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

resource teamsConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: teamsConnectionName
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
  name: azureBlobConnectionName
  location: location
  properties: {
    displayName: 'azureblob'
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/azureblob'
    }
    #disable-next-line BCP089
    parameterValueSet: {
      name: 'managedIdentityAuth'
      values: {}
    }
  }
}

resource documentDbConnection 'Microsoft.Web/connections@2016-06-01' = {
  name: documentDbConnectionName
  location: location
  properties: {
    displayName: 'documentdb'
    api: {
      id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/documentdb'
    }
    #disable-next-line BCP089
    parameterValueSet: {
      name: 'managedIdentityAuth'
      values: {}
    }
  }
}

resource workflow 'Microsoft.Logic/workflows@2019-05-01' = {
  name: logicAppWorkflowName
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
    //
    // On the first provision (before `azd deploy function-app` pushes the
    // Function App container image), we deploy an empty definition so ARM
    // does not try to validate `convertActivity` against a cold host. The
    // real definition is applied afterwards by hooks/function-postdeploy.ts.
    definition: includeWorkflowDefinition ? json(workflowDefinitionText) : emptyWorkflowDefinition
    parameters: includeWorkflowDefinition ? workflowParameters : {}
  }
}

resource metricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: logicAppAlertName
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
        actionGroupId: resourceId('Microsoft.Insights/actionGroups', actionGroupName)
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
