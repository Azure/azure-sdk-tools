using './main.bicep'

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param containerImage = '${readEnvironmentVariable('AZURE_CONTAINER_REGISTRY_ENDPOINT', '')}/${readEnvironmentVariable('FUNCTION_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-function:${env}')}'
param managedIdentityClientId = readEnvironmentVariable('MANAGED_IDENTITY_CLIENT_ID', '')
param botId = readEnvironmentVariable('BOT_ID', '')
param managedIdentityResourceId = readEnvironmentVariable('MANAGED_IDENTITY_RESOURCE_ID', '')
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME', '')
param keyVaultName = readEnvironmentVariable('KEY_VAULT_NAME', '')
param functionAppServicePlanNameOverride = readEnvironmentVariable('FUNCTION_APP_SERVICE_PLAN_NAME', '')
param functionLogWorkspaceNameOverride = readEnvironmentVariable('FUNCTION_LOG_WORKSPACE_NAME', '')
param functionAppNameOverride = readEnvironmentVariable('FUNCTION_APP_NAME_OVERRIDE', '')
