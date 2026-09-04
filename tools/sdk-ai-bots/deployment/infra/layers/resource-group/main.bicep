targetScope = 'subscription'

@description('Azure region for the resource group.')
param location string = 'westus2'

@description('Name of the resource group to create.')
param resourceGroupName string

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}
