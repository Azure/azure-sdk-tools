// cluster parameters
param tags object
param groupSuffix string
param dnsPrefix string = 's1'
param clusterName string
param location string = resourceGroup().location
param enableHighMemAgentPool bool = false

// monitoring parameters
param enableMonitoring bool = false
param workspaceId string

var kubernetesVersion = '1.21.2'
var nodeResourceGroup = 'rg-nodes-${dnsPrefix}-${clusterName}-${groupSuffix}'

var defaultAgentPool = {
  name: 'default'
  count: 3
  minCount: 3
  maxCount: 9
  mode: 'System'
  vmSize: 'Standard_D2_v3'
  type: 'VirtualMachineScaleSets'
  osType: 'Linux'
  enableAutoScaling: true
  enableEncryptionAtHost: true
  nodeLabels: {
      'sku': 'default'
  }
}

var highMemAgentPool = {
  name: 'highmemory'
  count: 1
  minCount: 1
  maxCount: 3
  mode: 'System'
  vmSize: 'Standard_D4ds_v4'
  type: 'VirtualMachineScaleSets'
  osType: 'Linux'
  enableAutoScaling: true
  enableEncryptionAtHost: true
  nodeLabels: {
      'sku': 'highMem'
  }
}

var agentPools = concat([
        defaultAgentPool
    ], enableHighMemAgentPool ? [
        highMemAgentPool
    ] : [])

resource cluster 'Microsoft.ContainerService/managedClusters@2020-09-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    addonProfiles: (enableMonitoring ? {
      azureKeyvaultSecretsProvider: {
        enabled: true
      }
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: workspaceId
        }
      }
    } : null)
    kubernetesVersion: kubernetesVersion
    enableRBAC: true
    dnsPrefix: dnsPrefix
    agentPoolProfiles: agentPools
    servicePrincipalProfile: {
      clientId: 'msi'
    }
    nodeResourceGroup: nodeResourceGroup
  }
}

// Add Monitoring Metrics Publisher role to omsagent identity. Required to publish metrics data to
// cluster resource container insights.
// https://docs.microsoft.com/en-us/azure/azure-monitor/containers/container-insights-update-metrics
resource metricsPublisher 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (enableMonitoring) {
  name: '${guid('monitoringMetricsPublisherRole', resourceGroup().id)}'
  scope: cluster
  properties: {
    roleDefinitionId: '${subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')}'
    // NOTE: using objectId over clientId seems to handle cross-region propagation delays better for newly created identities
    principalId: cluster.properties.addonProfiles.omsagent.identity.objectId
  }
}

output secretProviderObjectId string = cluster.properties.addonProfiles.azureKeyvaultSecretsProvider.identity.objectId
output secretProviderClientId string = cluster.properties.addonProfiles.azureKeyvaultSecretsProvider.identity.clientId
output kubeletIdentityObjectId string = cluster.properties.identityProfile.kubeletidentity.objectId
output clusterName string = cluster.name
