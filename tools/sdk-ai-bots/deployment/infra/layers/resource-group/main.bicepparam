using './main.bicep'

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param resourceGroupName = readEnvironmentVariable('AZURE_RESOURCE_GROUP', 'rg-azuresdkqabot-${env}')
