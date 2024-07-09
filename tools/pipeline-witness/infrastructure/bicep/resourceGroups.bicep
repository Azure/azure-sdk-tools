targetScope='subscription'

param location string

param appResourceGroupName string
param appServicePlanName string
param webAppName string
param networkSecurityGroupName string
param vnetName string
param cosmosAccountName string
param appStorageAccountName string
param aspEnvironment string

param logsResourceGroupName string
param logsStorageAccountName string
param kustoClusterName string
param kustoDatabaseName string
param deploymentName string = deployment().name

resource appResourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: appResourceGroupName
  location: location
}

module pipelineWitness 'appResourceGroup.bicep' = {
  name: deploymentName
  scope: appResourceGroup
  params: {
    location: location
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    cosmosAccountName: cosmosAccountName
    appStorageAccountName: appStorageAccountName
    aspEnvironment: aspEnvironment
    networkSecurityGroupName: networkSecurityGroupName
    vnetName: vnetName
  }
}

resource logsResourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: logsResourceGroupName
  location: location
}

module pipelineLogs 'logsResourceGroup.bicep' = {
  name: deploymentName
  scope: logsResourceGroup
  dependsOn: [
    pipelineWitness
  ]
  params: {
    location: location
    logsStorageAccountName: logsStorageAccountName
    kustoClusterName: kustoClusterName
    kustoDatabaseName: kustoDatabaseName
    webAppName: webAppName
    appIdentityPrincipalId: pipelineWitness.outputs.appIdentityPrincipalId
    subnetId: pipelineWitness.outputs.subnetId
  }
}
