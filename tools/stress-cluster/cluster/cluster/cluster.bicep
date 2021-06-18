param tags object
param groupSuffix string
param dnsPrefix string = 's1'
param clusterName string = 'stress-azuresdk'
param location string = resourceGroup().location
param agentVMSize string = 'Standard_D2_v3'
param enableMonitoring bool = false

@minValue(1)
@maxValue(50)
@description('The number of nodes for the cluster.')
param agentCount int = 3

var kubernetesVersion = '1.20.7'
var subnetRef = '${vn.id}/subnets/${subnetName}'
var addressPrefix = '20.0.0.0/16'
var subnetName = 'Subnet01'
var subnetPrefix = '20.0.0.0/24'
var virtualNetworkName = 'vnet-${dnsPrefix}-${clusterName}'
var nodeResourceGroup = 'rg-nodes-${dnsPrefix}-${clusterName}-${groupSuffix}'
var agentPoolName = 'agentpool01'

resource vn 'Microsoft.Network/virtualNetworks@2020-06-01' = {
  name: virtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetPrefix
        }
      }
    ]
  }
}

module logWorkspace '../monitoring/log-analytics-workspace.bicep' = if (enableMonitoring) {
    name: 'logsMod'
    params: {
        workspaceName: '${clusterName}-logs'
        location: location
    }
}

resource stress_cluster 'Microsoft.ContainerService/managedClusters@2020-09-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    addonProfiles: (enableMonitoring ? {
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logWorkspace.outputs.id
        }
      }
    } : null)
    kubernetesVersion: kubernetesVersion
    enableRBAC: true
    dnsPrefix: dnsPrefix
    agentPoolProfiles: [
      {
        name: agentPoolName
        count: agentCount
        mode: 'System'
        vmSize: agentVMSize
        type: 'VirtualMachineScaleSets'
        osType: 'Linux'
        enableAutoScaling: false
        vnetSubnetID: subnetRef
      }
    ]
    servicePrincipalProfile: {
      clientId: 'msi'
    }
    nodeResourceGroup: nodeResourceGroup
    networkProfile: {
      networkPlugin: 'azure'
      loadBalancerSku: 'standard'
    }
  }
}

output id string = stress_cluster.id
output apiServerAddress string = stress_cluster.properties.fqdn
