targetScope = 'resourceGroup'

@description('Principal (object) ID of the qabot-identity managed identity to grant OpenAI access.')
param managedIdentityPrincipalId string

@description('Name of the shared storage account (created by the shared resources module) connected to the Foundry project.')
param storageAccountName string

@description('Primary blob service endpoint of the shared storage account (from the shared resources module output).')
param storageBlobEndpoint string

// Log Analytics workspace backing the agent Application Insights. Created here so
// the agent layer is self-contained and does not depend on the (now removed)
// shared qabot-log workspace.
resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'qabot-agent-log-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: 'eastus'
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: 'qabot-agent-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: 'eastus'
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
  name: 'qabot-ai-resource-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  properties: {
    apiProperties: {}
    customSubDomainName: 'qabot-ai-resource-${substring(uniqueString(resourceGroup().id), 0, 6)}'
    networkAcls: {
      defaultAction: 'Allow'
      virtualNetworkRules: []
      ipRules: []
    }
    allowProjectManagement: true
    defaultProject: 'qabot-ai'
    associatedProjects: [
      'qabot-ai'
    ]
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
  location: 'eastus'
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource gpt41Deployment 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  name: 'gpt-4.1'
  parent: account
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1'
      version: '2025-04-14'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 1
    deploymentState: 'Running'
  }
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
}

resource gpt54Deployment 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  name: 'gpt-5.4'
  parent: account
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 1
    serviceTier: 'Default'
    deploymentState: 'Running'
  }
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
}

resource gpt51Deployment 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  name: 'gpt-5.1'
  parent: account
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.1'
      version: '2025-11-13'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 1
    serviceTier: 'Default'
    deploymentState: 'Running'
  }
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
}

resource gpt5MiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  name: 'gpt-5-mini'
  parent: account
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5-mini'
      version: '2025-08-07'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: 1
    deploymentState: 'Running'
  }
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
}

resource adaEmbeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2026-05-01' = {
  name: 'text-embedding-ada-002'
  parent: account
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
    versionUpgradeOption: 'NoAutoUpgrade'
    currentCapacity: 1
    deploymentState: 'Running'
  }
  sku: {
    name: 'GlobalStandard'
    capacity: 1
  }
}

resource accountStorageConnection 'Microsoft.CognitiveServices/accounts/connections@2026-05-01' = {
  name: 'qabotstorage${substring(uniqueString(resourceGroup().id), 0, 6)}'
  parent: account
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
  name: 'qabot-ai'
  parent: account
  properties: {}
  location: 'eastus'
  identity: {
    type: 'SystemAssigned'
  }
}

resource projectStorageConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2026-05-01' = {
  name: 'qabotstorage${substring(uniqueString(resourceGroup().id), 0, 6)}'
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
  name: 'qabot-agent-appinsights'
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

// Cognitive Services OpenAI User grant for qabot-identity, scoped to the AI
// account created above so the backend can call the model deployments via MSI.
resource openAiUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(account.id, managedIdentityPrincipalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// AzureSDKChatBot_Developer Entra group object ID.
var developerGroupObjectId = '2efb50ed-0ca9-4cf1-b43b-9b31a87e08f5'

// Cognitive Services OpenAI User grant for the developer group so devs can call
// the model deployments interactively.
resource developerOpenAiUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(account.id, developerGroupObjectId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: developerGroupObjectId
    principalType: 'Group'
  }
}

// Output
@description('Azure AI Services (Cognitive Services) account name.')
output aiResourceName string = account.name

@description('Azure AI project name.')
output aiProjectName string = project.name
