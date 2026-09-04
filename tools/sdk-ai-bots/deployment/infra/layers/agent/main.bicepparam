using './main.bicep'

param location = readEnvironmentVariable('AZURE_AI_DEPLOYMENTS_LOCATION', readEnvironmentVariable('AZURE_AI_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2')))
param managedIdentityPrincipalId = readEnvironmentVariable('MANAGED_IDENTITY_PRINCIPAL_ID', '')
param searchServicePrincipalId = readEnvironmentVariable('SEARCH_SERVICE_PRINCIPAL_ID', '')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME', '')
param storageBlobEndpoint = readEnvironmentVariable('STORAGE_BLOB_ENDPOINT', '')
param containerRegistryName = readEnvironmentVariable('CONTAINER_REGISTRY_NAME', '')
param developerGroupObjectId = readEnvironmentVariable('DEVELOPER_PRINCIPAL_ID', '')
param developerPrincipalType = readEnvironmentVariable('DEVELOPER_PRINCIPAL_TYPE', 'User')
param aiResourceNameOverride = readEnvironmentVariable('AI_RESOURCE_NAME_OVERRIDE', '')
param restoreAiResource = readEnvironmentVariable('AI_RESOURCE_RESTORE', 'false') == 'true'
param aiProjectNameOverride = readEnvironmentVariable('AI_PROJECT_NAME_OVERRIDE', '')
param agentLogWorkspaceNameOverride = readEnvironmentVariable('AGENT_LOG_WORKSPACE_NAME', '')
param agentAppInsightsNameOverride = readEnvironmentVariable('AGENT_APP_INSIGHTS_NAME', '')
