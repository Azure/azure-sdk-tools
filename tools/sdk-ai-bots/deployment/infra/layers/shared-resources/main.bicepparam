using './main.bicep'

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param cosmosDbLocation = readEnvironmentVariable('COSMOS_DB_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2'))
param developerGroupObjectId = readEnvironmentVariable('DEVELOPER_PRINCIPAL_ID', '')
param developerPrincipalType = readEnvironmentVariable('DEVELOPER_PRINCIPAL_TYPE', 'User')
param managedIdentityNameOverride = readEnvironmentVariable('MANAGED_IDENTITY_NAME_OVERRIDE', '')
param actionGroupNameOverride = readEnvironmentVariable('ACTION_GROUP_NAME_OVERRIDE', '')
param keyVaultNameOverride = readEnvironmentVariable('KEY_VAULT_NAME_OVERRIDE', '')
param appConfigNameOverride = readEnvironmentVariable('APP_CONFIG_NAME_OVERRIDE', '')
param searchServiceNameOverride = readEnvironmentVariable('SEARCH_SERVICE_NAME_OVERRIDE', '')
param containerRegistryNameOverride = readEnvironmentVariable('CONTAINER_REGISTRY_NAME_OVERRIDE', '')
param storageAccountNameOverride = readEnvironmentVariable('STORAGE_ACCOUNT_NAME_OVERRIDE', '')
param cosmosDbAccountNameOverride = readEnvironmentVariable('COSMOS_DB_ACCOUNT_NAME_OVERRIDE', '')