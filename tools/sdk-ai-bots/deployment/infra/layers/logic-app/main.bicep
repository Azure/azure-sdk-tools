targetScope = 'resourceGroup'

@description('Azure region for the Logic App and managed API connections.')
param location string

@description('Teams team (group) ID to monitor.')
param teamsGroupId string

@description('Teams channel IDs to subscribe to.')
param teamsChannelIds array

@description('Azure MCP Server Teams team (group) ID to monitor. Leave empty with no channel IDs to omit the second workflow.')
param azureMcpTeamsGroupId string = ''

@description('Azure MCP Server Teams channel IDs to subscribe to.')
param azureMcpTeamsChannelIds array = []

@description('Base URL of the agent server.')
param serverBaseUrl string

@description('Application ID URI used as the managed-identity token resource for the agent server.')
param serverApplicationIdUri string

@description('Base URL of the bot.')
param botBaseUrl string

@description('Client ID (audience) for authenticating to the bot.')
param botAudience string

@description('Name of the shared identity used for agent-server calls and the Cosmos DB connection.')
param managedIdentityName string

@description('Name of the bot managed identity.')
param botIdentityName string

@description('Enable the workflow only when the Teams API connection has completed OAuth consent.')
param workflowEnabled bool = false

@description('When false, skip PUT on the Teams `Microsoft.Web/connections` resource. Once the Teams connection has been authorized interactively (OAuth consent — status "Connected"), re-issuing the PUT can invalidate or reset the bound access token. hooks/logic-app-preprovision.ts probes the connection status and, if already Connected, sets this to false via `azd env set CREATE_TEAMS_CONNECTION false` so provision leaves the authorized connection untouched. Default true so the first provision creates the connection shell.')
param createTeamsConnection bool = true

// Resource-name overrides — see the shared-resources layer.
@description('Name of the Logic Apps integration account.')
param integrationAccountNameOverride string = ''

@description('Name of the Teams managed API connection.')
param teamsConnectionNameOverride string = ''

@description('Name of the Logic App workflow.')
param logicAppWorkflowNameOverride string = ''

@description('Name of the Azure MCP Server Logic App workflow.')
param azureMcpLogicAppWorkflowNameOverride string = ''

@description('Name of the metric alert on the Logic App workflow.')
param logicAppAlertNameOverride string = ''

@description('Name of the metric alert on the Azure MCP Server Logic App workflow.')
param azureMcpLogicAppAlertNameOverride string = ''

@description('Name of the action group receiving Logic App failure alerts. Created by the shared-resources layer.')
param actionGroupName string = 'qabot-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var integrationAccountName = !empty(integrationAccountNameOverride) ? integrationAccountNameOverride : 'azuresdkqabot-ia-${suffix}'
var teamsConnectionName = !empty(teamsConnectionNameOverride) ? teamsConnectionNameOverride : 'teams-${suffix}'
var logicAppWorkflowName = !empty(logicAppWorkflowNameOverride) ? logicAppWorkflowNameOverride : 'azuresdkqabot-logicapp-${suffix}'
var azureMcpLogicAppWorkflowName = !empty(azureMcpLogicAppWorkflowNameOverride) ? azureMcpLogicAppWorkflowNameOverride : 'azuremcpserver-qabot-logicapp-${suffix}'
var logicAppAlertName = !empty(logicAppAlertNameOverride) ? logicAppAlertNameOverride : 'azuresdkqabot-logicapp-alert-${suffix}'
var azureMcpLogicAppAlertName = !empty(azureMcpLogicAppAlertNameOverride) ? azureMcpLogicAppAlertNameOverride : '${azureMcpLogicAppWorkflowName}-alert'
var deployAzureMcpWorkflow = !empty(azureMcpTeamsGroupId) && length(azureMcpTeamsChannelIds) > 0


// Resource IDs the workflow authenticates with via managed identity. Computed
// from names so the layer resolves correctly in any subscription / RG.
var serverIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', managedIdentityName)
var botIdentityResourceId = resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', botIdentityName)

// Computed ID for the Teams managed API connection. Used instead of
// `teamsConnection.id` in the workflow parameters because `teamsConnection`
// is a conditional resource (see createTeamsConnection); referencing a
// conditional resource symbolically fails when the condition is false.
var teamsConnectionResourceId = resourceId('Microsoft.Web/connections', teamsConnectionName)

var workflowDefinition = json(loadTextContent('./workflowDefinition.json'))

var workflowParameters = {
  '$connections': {
    value: {
      teams: {
        connectionId: teamsConnectionResourceId
        connectionName: teamsConnectionName
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
  serverApplicationIdUri: {
    value: serverApplicationIdUri
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
}

var azureMcpWorkflowParameters = {
  '$connections': {
    value: {
      teams: {
        connectionId: teamsConnectionResourceId
        connectionName: teamsConnectionName
        connectionProperties: {}
        id: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/teams'
      }
    }
  }
  teamsGroupId: {
    value: azureMcpTeamsGroupId
  }
  teamsChannelIds: {
    value: azureMcpTeamsChannelIds
  }
  serverBaseUrl: {
    value: serverBaseUrl
  }
  serverApplicationIdUri: {
    value: serverApplicationIdUri
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
}

resource integrationAccount 'Microsoft.Logic/integrationAccounts@2019-05-01' = {
  name: integrationAccountName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {}
}

// Teams managed API connection. Uses OAuth (delegated user consent) — once
// a person has completed the consent flow and the status is "Connected", the
// bound token lives in ARM state that is NOT part of `parameterValues`, but
// re-issuing this PUT can still reset the auth binding. So this resource is
// created only when `createTeamsConnection` is true — the default for the
// first provision. hooks/logic-app-preprovision.ts detects a Connected connection and
// sets CREATE_TEAMS_CONNECTION=false so subsequent provisions leave the
// authorized connection untouched.
resource teamsConnection 'Microsoft.Web/connections@2016-06-01' = if (createTeamsConnection) {
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
    state: workflowEnabled ? 'Enabled' : 'Disabled'
    integrationAccount: {
      id: integrationAccount.id
    }
    definition: workflowDefinition
    parameters: workflowParameters
  }
}

resource azureMcpWorkflow 'Microsoft.Logic/workflows@2019-05-01' = if (deployAzureMcpWorkflow) {
  name: azureMcpLogicAppWorkflowName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${botIdentityResourceId}': {}
      '${serverIdentityResourceId}': {}
    }
  }
  properties: {
    state: workflowEnabled ? 'Enabled' : 'Disabled'
    integrationAccount: {
      id: integrationAccount.id
    }
    definition: workflowDefinition
    parameters: azureMcpWorkflowParameters
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

resource azureMcpMetricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = if (deployAzureMcpWorkflow) {
  name: azureMcpLogicAppAlertName
  location: 'global'
  properties: {
    severity: 3
    enabled: true
    scopes: [
      azureMcpWorkflow!.id
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

output TEAMS_CONNECTION_NAME string = teamsConnectionName
output LOGIC_APP_WORKFLOW_NAME string = logicAppWorkflowName
output AZURE_MCP_SERVER_LOGIC_APP_WORKFLOW_NAME string = deployAzureMcpWorkflow ? azureMcpLogicAppWorkflowName : ''
