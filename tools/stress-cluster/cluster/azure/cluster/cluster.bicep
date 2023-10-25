// cluster parameters
param tags object
param groupSuffix string
param dnsPrefix string = 's1'
param clusterName string
param location string = resourceGroup().location
param defaultAgentPoolMinNodes int = 6
param defaultAgentPoolMaxNodes int = 20
param maintenanceWindowDay string = 'Monday'
// AKS does not allow agentPool updates via existing managed cluster resources
param updateNodes bool = false

// monitoring parameters
param workspaceId string

var kubernetesVersion = '1.26.6'
var nodeResourceGroup = 'rg-nodes-${dnsPrefix}-${clusterName}-${groupSuffix}'

var systemAgentPool = {
  name: 'systemal'
  count: 1
  minCount: 1
  maxCount: 4
  mode: 'System'
  vmSize: 'Standard_D4ds_v4'
  type: 'VirtualMachineScaleSets'
  osType: 'AzureLinux'
  enableAutoScaling: true
  enableEncryptionAtHost: true
  nodeLabels: {
    sku: 'system'
  }
}

var defaultAgentPool = {
  name: 'defaultal'
  count: defaultAgentPoolMinNodes
  minCount: defaultAgentPoolMinNodes
  maxCount: defaultAgentPoolMaxNodes
  mode: 'User'
  vmSize: 'Standard_D8a_v4'
  type: 'VirtualMachineScaleSets'
  osType: 'AzureLinux'
  osDiskType: 'Ephemeral'
  enableAutoScaling: true
  enableEncryptionAtHost: true
  nodeLabels: {
    sku: 'default'
  }
}

var agentPools = [
    systemAgentPool
    defaultAgentPool
]

resource newCluster 'Microsoft.ContainerService/managedClusters@2023-02-02-preview' = if (!updateNodes) {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
      }
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: workspaceId
        }
      }
      azurepolicy: {
        enabled: true
      }
    }
    autoUpgradeProfile: {
      nodeOSUpgradeChannel: 'SecurityPatch'
      upgradeChannel: null
    }
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

resource maintenanceConfig 'Microsoft.ContainerService/managedClusters/maintenanceConfigurations@2023-05-02-preview' = if (!updateNodes) {
  name: 'aksManagedNodeOSUpgradeSchedule'
  parent: newCluster
  properties: {
    maintenanceWindow: {
      durationHours: 4
      utcOffset: '-08:00'
      startTime: '02:00'
      schedule: {
        weekly: {
          dayOfWeek: maintenanceWindowDay
          intervalWeeks: 1
        }
      }
    }
  }
}


resource existingCluster 'Microsoft.ContainerService/managedClusters@2023-02-02-preview' existing = if (updateNodes) {
  name: clusterName
}

// Workaround for duplicate variable names when conditionals are in use
// See https://github.com/Azure/bicep/issues/1410
var cluster = updateNodes ? existingCluster : newCluster

resource pools 'Microsoft.ContainerService/managedClusters/agentPools@2022-09-02-preview' = [for pool in agentPools: if (updateNodes) {
  parent: existingCluster
  name: pool.name
  properties: {
    count: pool.count
    minCount: pool.minCount
    maxCount: pool.maxCount
    mode: pool.mode
    vmSize: pool.vmSize
    type: pool.type
    osType: pool.osType
    enableAutoScaling: pool.enableAutoScaling
    // enableEncryptionAtHost: pool.enableEncryptionAtHost
    nodeLabels: pool.nodeLabels
  }
}]

// Add Monitoring Metrics Publisher role to omsagent identity. Required to publish metrics data to
// cluster resource container insights.
// https://docs.microsoft.com/azure/azure-monitor/containers/container-insights-update-metrics
resource metricsPublisher 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (!updateNodes) {
  name: guid('monitoringMetricsPublisherRole', resourceGroup().id)
  scope: newCluster
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
    // NOTE: using objectId over clientId seems to handle cross-region propagation delays better for newly created identities
    principalId: cluster.properties.addonProfiles.omsagent.identity.objectId
  }
}

output secretProviderObjectId string = cluster.properties.addonProfiles.azureKeyvaultSecretsProvider.identity.objectId
output secretProviderClientId string = cluster.properties.addonProfiles.azureKeyvaultSecretsProvider.identity.clientId
output kubeletIdentityObjectId string = cluster.properties.identityProfile.kubeletidentity.objectId
output clusterName string = clusterName
