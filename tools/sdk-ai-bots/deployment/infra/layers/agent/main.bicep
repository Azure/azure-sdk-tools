targetScope = 'resourceGroup'

@description('Azure region for the Cognitive Services / Foundry account, its model deployments, and backing Application Insights. May differ from the primary `location` because model availability is region-specific (prod uses swedencentral).')
param location string

@description('Name of the shared storage account connected to the Foundry project.')
param storageAccountName string

@description('Primary blob service endpoint from the shared-resources layer.')
param storageBlobEndpoint string

@description('Name of the Log Analytics workspace backing the agent Application Insights.')
param agentLogWorkspaceNameOverride string = ''

@description('Location of the Log Analytics workspace backing the agent Application Insights. Defaults to the agent resource location.')
param agentLogWorkspaceLocation string = location

@description('Name of the Application Insights component for the agent.')
param agentAppInsightsNameOverride string = ''

@description('Name of the Cognitive Services (AIServices) account.')
param aiResourceNameOverride string = ''

@description('Restore the AIServices account when a soft-deleted resource with the same name exists.')
param restoreAiResource bool = false

@description('Name of the Foundry project inside the AIServices account.')
param aiProjectNameOverride string = ''

@description('User-assigned identities attached to the AI Services account, keyed by full resource ID.')
param aiResourceUserAssignedIdentities object = {}

@description('Retention period for the agent Log Analytics workspace.')
param agentLogRetentionInDays int = 30

@description('RAI policy applied to every deployment managed by this layer.')
param modelRaiPolicyName string = 'Microsoft.DefaultV2'

@description('SKU used by the gpt-4.1 deployment.')
param gpt41SkuName string = 'GlobalStandard'

@description('Capacity used by the gpt-4.1 deployment.')
param gpt41Capacity int = 1

@description('Capacity used by the gpt-5.6-sol deployment.')
param gpt56SolCapacity int = 500

@description('Capacity used by the gpt-5.1 deployment.')
param gpt51Capacity int = 1

@description('Capacity used by the gpt-5-mini deployment.')
param gpt5MiniCapacity int = 1

@description('Capacity used by the text-embedding-3-small deployment.')
param textEmbedding3SmallCapacity int = 1

@description('Version upgrade policy used by text-embedding-3-small.')
param textEmbedding3SmallVersionUpgradeOption string = 'NoAutoUpgrade'

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var agentLogWorkspaceName = !empty(agentLogWorkspaceNameOverride) ? agentLogWorkspaceNameOverride : 'qabot-agent-log-${suffix}'
var agentAppInsightsName = !empty(agentAppInsightsNameOverride) ? agentAppInsightsNameOverride : 'qabot-agent-${suffix}'
var aiResourceName = !empty(aiResourceNameOverride) ? aiResourceNameOverride : 'qabot-ai-resource-${suffix}'
var aiProjectName = !empty(aiProjectNameOverride) ? aiProjectNameOverride : 'qabot-ai'

// Log Analytics workspace backing the agent Application Insights. Created here so
// the agent layer is self-contained and does not depend on the (now removed)
// shared qabot-log workspace.
resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: agentLogWorkspaceName
  location: agentLogWorkspaceLocation
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: agentLogRetentionInDays
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: agentAppInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2026-05-01' = {
  name: aiResourceName
  properties: union({
    apiProperties: {}
    customSubDomainName: aiResourceName
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    allowProjectManagement: true
    defaultProject: aiProjectName
    associatedProjects: [
      aiProjectName
    ]
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }, restoreAiResource ? {
    restore: true
  } : {})
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: empty(aiResourceUserAssignedIdentities) ? 'SystemAssigned' : 'SystemAssigned, UserAssigned'
    userAssignedIdentities: aiResourceUserAssignedIdentities
  }
}

var modelDeploymentConfigs = [
  {
    name: 'gpt-4.1'
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-4.1'
        version: '2025-04-14'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
      raiPolicyName: modelRaiPolicyName
      currentCapacity: gpt41Capacity
      deploymentState: 'Running'
    }
    sku: {
      name: gpt41SkuName
      capacity: gpt41Capacity
    }
  }
  {
    name: 'gpt-5.6-sol'
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-5.6-sol'
        version: '2026-07-09'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
      raiPolicyName: modelRaiPolicyName
      // 500 capacity units ≈ 500,000 TPM (GlobalStandard: 1 unit ≈ 1,000 TPM).
      currentCapacity: gpt56SolCapacity
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      // 500 units ≈ 500,000 TPM. This consumes the currently available half of
      // the subscription's 1,000-unit GlobalStandard gpt-5.6-sol quota.
      capacity: gpt56SolCapacity
    }
  }
  {
    name: 'gpt-5.1'
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-5.1'
        version: '2025-11-13'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
      raiPolicyName: modelRaiPolicyName
      currentCapacity: gpt51Capacity
      serviceTier: 'Default'
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: gpt51Capacity
    }
  }
  {
    name: 'gpt-5-mini'
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-5-mini'
        version: '2025-08-07'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
      raiPolicyName: modelRaiPolicyName
      currentCapacity: gpt5MiniCapacity
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: gpt5MiniCapacity
    }
  }
  {
    name: 'text-embedding-3-small'
    properties: {
      model: {
        format: 'OpenAI'
        name: 'text-embedding-3-small'
        version: '1'
      }
      versionUpgradeOption: textEmbedding3SmallVersionUpgradeOption
      raiPolicyName: modelRaiPolicyName
      currentCapacity: textEmbedding3SmallCapacity
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: textEmbedding3SmallCapacity
    }
  }
]

// Cognitive Services rejects concurrent writes to deployment children of the
// same account with RequestConflict. A batch size of one deploys each model in
// modelDeploymentConfigs order.
@batchSize(1)
resource modelDeployments 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = [for deployment in modelDeploymentConfigs: {
  name: deployment.name
  parent: account
  properties: deployment.properties
  sku: deployment.sku
}]

resource accountStorageConnection 'Microsoft.CognitiveServices/accounts/connections@2026-05-01' = {
  name: storageAccountName
  parent: account
  dependsOn: [modelDeployments]
  properties: {
    authType: 'AAD'
    category: 'AzureStorageAccount'
    target: storageBlobEndpoint
    useWorkspaceManagedIdentity: false
    isSharedToAll: false
    sharedUserList: []
    peRequirement: 'NotRequired'
    peStatus: 'NotApplicable'
    metadata: {
      ApiType: 'Azure'
      ResourceId: resourceId('Microsoft.Storage/storageAccounts', storageAccountName)
    }
  }
}

// NOTE: Basic Agent Setup — no capabilityHost is required. The hosted agent is
// deployed via the agent.yaml flow (azd deploy agent), which uses platform-
// managed storage/runtime rather than a Standard Setup capabilityHost.

resource project 'Microsoft.CognitiveServices/accounts/projects@2026-05-01' = {
  name: aiProjectName
  parent: account
  dependsOn: [modelDeployments]
  properties: {}
  location: location
  identity: {
    type: 'SystemAssigned'
  }
}

resource projectStorageConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2026-05-01' = {
  name: '${storageAccountName}-project'
  parent: project
  properties: {
    authType: 'AAD'
    category: 'AzureStorageAccount'
    target: storageBlobEndpoint
    useWorkspaceManagedIdentity: false
    isSharedToAll: false
    sharedUserList: []
    peRequirement: 'NotRequired'
    peStatus: 'NotApplicable'
    metadata: {
      ApiType: 'Azure'
      ResourceId: resourceId('Microsoft.Storage/storageAccounts', storageAccountName)
    }
  }
}

// Application Insights connection so the Foundry project emits agent traces and
// telemetry to the qabot-agent component created above.
resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2026-05-01' = {
  name: component.name
  parent: project
  properties: {
    authType: 'ApiKey'
    category: 'AppInsights'
    target: component.id
    credentials: {
      key: component.properties.ConnectionString
    }
    isSharedToAll: true
    metadata: {
      ApiType: 'Azure'
      ResourceId: component.id
    }
  }
}

// Runtime and deployment roles are reconciled by the layer postprovision hook.

// Output
@description('Azure AI Services (Cognitive Services) account name.')
output AI_RESOURCE_NAME string = account.name

@description('Azure AI project name.')
output AI_PROJECT_NAME string = project.name

@description('Full ARM resource ID of the Azure AI Foundry project required by the azd agent extension.')
output AZURE_AI_PROJECT_ID string = project.id

@description('Principal ID of the Foundry project identity that pulls hosted-agent images.')
output FOUNDRY_PROJECT_PRINCIPAL_ID string = project.identity.principalId

@description('Azure AI Foundry project REST API endpoint.')
output FOUNDRY_PROJECT_ENDPOINT string = 'https://${account.name}.services.ai.azure.com/api/projects/${project.name}'

@description('Full ARM resource ID of the Application Insights component containing chat-agent traces.')
output AGENT_APPLICATIONINSIGHTS_RESOURCE_ID string = component.id
