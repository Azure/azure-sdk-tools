targetScope='subscription'

param resourceGroupName string
param location string
param storageAccountName string
param kustoClusterName string
param kustoDatabaseName string
param deploymentName string = deployment().name

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: resourceGroupName
  location: location
}

module resources 'resources.bicep' = {
  name: deploymentName
  scope: resourceGroup
  params: {
    storageAccountName: storageAccountName
    kustoClusterName: kustoClusterName
    kustoDatabaseName: kustoDatabaseName
    location: location
  }
}
