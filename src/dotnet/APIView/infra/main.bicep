targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@minLength(1)
@description('Principal ID to grant access to the resources')
param principalId string

param cosmosAccountName string = ''
param cosmosDatabaseName string = 'APIViewV2'
param resourceGroupName string = ''
param blobName string = ''

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

var pid = !empty(principalId) ? principalId : '<default principal id>'

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : 'apiview-${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// The application database
module cosmos './app/cosmos.bicep' = {
  name: 'apiview-cosmos-${environmentName}'
  scope: rg
  params: {
    accountName: !empty(cosmosAccountName) ? cosmosAccountName : '${abbrs.documentDBDatabaseAccounts}${resourceToken}'
    databaseName: cosmosDatabaseName
    location: location
    tags: tags
    principalId: pid
  }
}

// The application storage
module storage './app/storage.bicep' = {
  name: 'apiview-storage-${environmentName}'
  scope: rg
  params: {
    name: !empty(blobName) ? blobName : '${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    principalId: pid
  }
}

// The application configuration store
module appconfig './app/appconfig.bicep' = {
  name: 'apiview-appconf-${environmentName}'
  scope: rg
  params: {
    configStoreName: '${abbrs.appConfigurationConfigurationStores}${resourceToken}'
    location: location
  }
}

// Data outputs
output AZURE_COSMOS_ENDPOINT string = cosmos.outputs.endpoint
output AZURE_COSMOS_DATABASE_NAME string = cosmos.outputs.databaseName

output AZURE_STORAGE_NAME string = storage.outputs.name
output AZURE_STORAGE_BLOB_ENDPOINT string = storage.outputs.primaryEndpoints.blob

output AZURE_APPCONFIG_ENDPOINT string = appconfig.outputs.endpoint

// App outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
