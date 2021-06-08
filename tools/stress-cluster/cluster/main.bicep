targetScope = 'subscription'

param groupSuffix string
param location string
param tags object

resource clusterGroup 'Microsoft.Resources/resourceGroups@2020-10-01' = {
    name: 'rg-stress-test-cluster-${groupSuffix}'
    location: location
    tags: tags
}

module clusterMod './cluster.bicep' = {
    name: 'clusterMod'
    scope: clusterGroup
    params: {
        tags: tags
        groupSuffix: groupSuffix
    }
}
