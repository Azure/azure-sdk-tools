targetScope = 'resourceGroup'

@description('Azure region for the Cognitive Services / Foundry account, its model deployments, and backing Application Insights. May differ from the primary `location` because model availability is region-specific (prod uses swedencentral).')
param location string

@description('Principal (object) ID of the qabot-identity managed identity to grant OpenAI access.')
param managedIdentityPrincipalId string

@description('Principal (object) ID of the Search service system-assigned identity.')
param searchServicePrincipalId string

@description('Name of the shared storage account connected to the Foundry project.')
param storageAccountName string

@description('Primary blob service endpoint from the shared-resources layer.')
param storageBlobEndpoint string

@description('Name of the Log Analytics workspace backing the agent Application Insights.')
param agentLogWorkspaceNameOverride string = ''

@description('Name of the Application Insights component for the agent.')
param agentAppInsightsNameOverride string = ''

@description('Name of the Cognitive Services (AIServices) account.')
param aiResourceNameOverride string = ''

@description('Restore the AIServices account when a soft-deleted resource with the same name exists.')
param restoreAiResource bool = false

@description('Name of the Foundry project inside the AIServices account.')
param aiProjectNameOverride string = ''

@description('Name of the shared container registry the hosted agent pulls its image from. The Foundry project managed identity is granted AcrPull on it.')
param containerRegistryName string

@description('Object ID of the developer principal (user, group, or SP) to grant OpenAI access. Empty = skip.')
param developerGroupObjectId string = ''

@description('Principal type for the developer role assignment: User, Group, or ServicePrincipal.')
param developerPrincipalType string = 'User'

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
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
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
  properties: {
    restore: restoreAiResource
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
  }
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
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
      currentCapacity: 1
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: 1
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
      // 500 capacity units ≈ 500,000 TPM (GlobalStandard: 1 unit ≈ 1,000 TPM).
      currentCapacity: 500
      serviceTier: 'Default'
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      // 500 units ≈ 500,000 TPM. This consumes the currently available half of
      // the subscription's 1,000-unit GlobalStandard gpt-5.6-sol quota.
      capacity: 500
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
      currentCapacity: 1
      serviceTier: 'Default'
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: 1
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
      currentCapacity: 1
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: 1
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
      versionUpgradeOption: 'NoAutoUpgrade'
      currentCapacity: 1
      deploymentState: 'Running'
    }
    sku: {
      name: 'GlobalStandard'
      capacity: 1
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

// Search uses its system-assigned identity for integrated vectorization and
// knowledge-base model calls.
resource searchCognitiveServicesUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: account
  name: guid(account.id, searchServicePrincipalId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: searchServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

// NOTE: the "Azure AI User" (Foundry User, 53ca6127-db72-4b80-b1b0-d745d6d5456d)
// grant for qabot-identity on this AI account is NOT declared here. It is created
// idempotently (create-if-not-exists) by the agent layer's postprovision hook
// (hooks/agent-postprovision.ts).
// A native bicep roleAssignments resource uses a deterministic name and fails
// the whole deployment with RoleAssignmentExists if the same (principal, role,
// scope) already exists under a different name — e.g. one created out-of-band via
// `az role assignment create` in the lock-protected dev RG, which cannot be
// deleted. The hook tolerates that case and still creates the grant on fresh
// environments. qabot-identity needs this role because the agent server
// (server.py) reads the hosted Foundry agent via `project_client.agents.get(...)`,
// which requires the data action
// `Microsoft.CognitiveServices/accounts/AIServices/agents/read` that
// "Cognitive Services OpenAI User" (assigned above) does not cover.

// AzureSDKChatBot_Developer Entra group object ID.
// Passed as a parameter; empty means no developer role assignment.
// var developerGroupObjectId = '2efb50ed-0ca9-4cf1-b43b-9b31a87e08f5'  ← moved to param above

// Cognitive Services OpenAI User grant for the developer principal.
resource developerOpenAiUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerGroupObjectId)) {
  scope: account
  name: guid(account.id, developerGroupObjectId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: developerGroupObjectId
    principalType: developerPrincipalType
  }
}

// Foundry Project Manager grant for the developer principal on the AI account.
// This is the role that authorizes hosted-agent management: it includes the
// `Microsoft.CognitiveServices/*` action set (covering
// `accounts/AIServices/agents/write`) that `azd deploy agent` — via the
// azure.ai.agents extension — needs to create hosted agent versions through the
// Foundry REST API. It supersedes both "Azure AI Developer" (which lacks the
// AIServices/agents action) and "Cognitive Services Contributor" (whose
// CognitiveServices/* wildcard this role already includes), so neither of those
// is assigned here.
resource developerFoundryProjectManagerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(developerGroupObjectId)) {
  scope: account
  name: guid(account.id, developerGroupObjectId, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'eadc314b-1a2d-4efa-be10-5d325db5065e'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'eadc314b-1a2d-4efa-be10-5d325db5065e')
    principalId: developerGroupObjectId
    principalType: developerPrincipalType
  }
}

// The hosted agent pulls its image from the shared container registry using the
// Foundry project's system-assigned managed identity (confirmed as the image-
// pull principal by the Foundry hosted-agent docs and the azd agent extension's
// own bicep templates). Without AcrPull on the registry, `azd deploy agent`
// fails with `[ImageError] Container registry rejected the image pull (HTTP 403
// Forbidden)`.
resource registry 'Microsoft.ContainerRegistry/registries@2026-01-01-preview' existing = {
  name: containerRegistryName
}

resource projectAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, project.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: project.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Output
@description('Azure AI Services (Cognitive Services) account name.')
output AI_RESOURCE_NAME string = account.name

@description('Azure AI project name.')
output AI_PROJECT_NAME string = project.name

@description('Full ARM resource ID of the Azure AI Foundry project (CognitiveServices path).')
output AZURE_AI_PROJECT_ID string = project.id

@description('Azure AI Foundry project REST API endpoint.')
output FOUNDRY_PROJECT_ENDPOINT string = 'https://${account.name}.services.ai.azure.com/api/projects/${project.name}'

@description('Full ARM resource ID of the Application Insights component containing chat-agent traces.')
output AGENT_APPLICATIONINSIGHTS_RESOURCE_ID string = component.id
