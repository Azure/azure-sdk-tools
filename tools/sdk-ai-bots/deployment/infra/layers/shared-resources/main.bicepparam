using './main.bicep'

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param cosmosDbLocation = readEnvironmentVariable('COSMOS_DB_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2'))
param developerGroupObjectId = readEnvironmentVariable('DEVELOPER_PRINCIPAL_ID', '')
param developerPrincipalType = readEnvironmentVariable('DEVELOPER_PRINCIPAL_TYPE', 'User')
param deploymentPrincipalObjectId = readEnvironmentVariable('DEPLOYMENT_PRINCIPAL_ID', '')
param deploymentPrincipalType = readEnvironmentVariable('DEPLOYMENT_PRINCIPAL_TYPE', 'ServicePrincipal')
param manageAuthorizationResources = readEnvironmentVariable('MANAGE_AUTHORIZATION_RESOURCES', 'true') == 'true'
param managedIdentityNameOverride = readEnvironmentVariable('MANAGED_IDENTITY_NAME_OVERRIDE', '')
param actionGroupNameOverride = readEnvironmentVariable('ACTION_GROUP_NAME_OVERRIDE', '')
param keyVaultNameOverride = readEnvironmentVariable('KEY_VAULT_NAME_OVERRIDE', '')
param appConfigNameOverride = readEnvironmentVariable('APP_CONFIG_NAME_OVERRIDE', '')
param searchServiceNameOverride = readEnvironmentVariable('SEARCH_SERVICE_NAME_OVERRIDE', '')
param containerRegistryNameOverride = readEnvironmentVariable('CONTAINER_REGISTRY_NAME_OVERRIDE', '')
param storageAccountNameOverride = readEnvironmentVariable('STORAGE_ACCOUNT_NAME_OVERRIDE', '')
param cosmosDbAccountNameOverride = readEnvironmentVariable('COSMOS_DB_ACCOUNT_NAME_OVERRIDE', '')
param keyVaultAccessPolicies = json(readEnvironmentVariable('KEY_VAULT_ACCESS_POLICIES', '[]'))
param searchUserAssignedIdentities = json(readEnvironmentVariable('SEARCH_USER_ASSIGNED_IDENTITIES', '{}'))
param containerRegistryUserAssignedIdentities = json(readEnvironmentVariable('CONTAINER_REGISTRY_USER_ASSIGNED_IDENTITIES', '{}'))
param cosmosCapabilities = json(readEnvironmentVariable('COSMOS_CAPABILITIES', '[]'))
param searchKnowledgeRetrieval = readEnvironmentVariable('SEARCH_KNOWLEDGE_RETRIEVAL', 'standard')
param enableEpisodeVectorIndex = readEnvironmentVariable('ENABLE_EPISODE_VECTOR_INDEX', 'false') == 'true'
