targetScope='subscription'

param location string

param appResourceGroupName string
param appServicePlanName string
param webAppName string
param networkSecurityGroupName string
param vnetName string
param vnetPrefix string
param subnetPrefix string
param useVnet bool
param cosmosAccountName string
param appStorageAccountName string
param aspEnvironment string
param keyVaultName string
param devOpsEventHubNamespaceName string
param gitHubEventHubNamespaceName string

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
    vnetPrefix: vnetPrefix
    subnetPrefix: subnetPrefix
    webAppName: webAppName
    keyVaultName: keyVaultName
    cosmosAccountName: cosmosAccountName
    appStorageAccountName: appStorageAccountName
    aspEnvironment: aspEnvironment
    networkSecurityGroupName: networkSecurityGroupName
    vnetName: vnetName
    useVnet: useVnet
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
    devOpsEventHubNamespaceName: devOpsEventHubNamespaceName
    gitHubEventHubNamespaceName: gitHubEventHubNamespaceName
    appIdentityPrincipalId: pipelineWitness.outputs.appIdentityPrincipalId
    subnetId: pipelineWitness.outputs.subnetId
    useVnet: useVnet
  }
}
