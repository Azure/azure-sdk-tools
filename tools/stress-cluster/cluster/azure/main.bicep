targetScope = 'subscription'

param groupSuffix string
param clusterName string
param clusterLocation string = 'westus2'
param staticTestSecretsKeyvaultName string
param staticTestSecretsKeyvaultGroup string
param monitoringLocation string = 'centralus'
param tags object
param enableMonitoring bool = false

// Azure Developer Platform Team Group
// https://ms.portal.azure.com/#blade/Microsoft_AAD_IAM/GroupDetailsMenuBlade/Overview/groupId/56709ad9-8962-418a-ad0d-4b25fa962bae
param accessGroups array = [
    '56709ad9-8962-418a-ad0d-4b25fa962bae'
]

resource group 'Microsoft.Resources/resourceGroups@2020-10-01' = {
    name: 'rg-stress-test-cluster-${groupSuffix}'
    location: clusterLocation
    tags: tags
}

// Add unique suffix to monitoring resource names to simplify cross-resource queries.
// https://docs.microsoft.com/en-us/azure/azure-monitor/logs/cross-workspace-query#identifying-an-application
var resourceSuffix = uniqueString(group.id)

module logWorkspace 'monitoring/log-analytics-workspace.bicep' = if (enableMonitoring) {
    name: 'logs'
    scope: group
    params: {
        workspaceName: '${clusterName}-logs-${resourceSuffix}'
        location: monitoringLocation
    }
}

module appInsights 'monitoring/app-insights.bicep' = if (enableMonitoring) {
    name: 'appInsights'
    scope: group
    params: {
        name: '${clusterName}-ai-${resourceSuffix}'
        location: monitoringLocation
        workspaceId: logWorkspace.outputs.id
    }
}

module cluster 'cluster/cluster.bicep' = {
    name: 'cluster'
    scope: group
    params: {
        clusterName: clusterName
        tags: tags
        groupSuffix: groupSuffix
        enableMonitoring: enableMonitoring
        workspaceId: enableMonitoring ? logWorkspace.outputs.id : ''
    }
}

module containerRegistry 'cluster/acr.bicep' = {
    name: 'containerRegistry'
    scope: group
    params: {
        registryName: '${replace(clusterName, '-', '')}registry'
        location: clusterLocation
        objectIds: concat(accessGroups, array(cluster.outputs.kubeletIdentityObjectId))
    }
}

var appInsightsInstrumentationKeySecretName = 'appInsightsInstrumentationKey-${resourceSuffix}'
var appInsightsInstrumentationKeySecretValue = 'APPINSIGHTS_INSTRUMENTATIONKEY=${appInsights.outputs.instrumentationKey}\n'

module keyvault 'cluster/keyvault.bicep' = if (enableMonitoring) {
    name: 'keyvault'
    scope: group
    params: {
        keyvaultName: 'stress-kv-${resourceSuffix}'  // 24 character max length
        location: clusterLocation
        tags: tags
        objectIds: concat(accessGroups, array(cluster.outputs.secretProviderObjectId))
        secretsObject: {
            secrets: [
                {
                    secretName: appInsightsInstrumentationKeySecretName
                    secretValue: appInsightsInstrumentationKeySecretValue
                }
            ]
        }
    }
}

module accessPolicy 'cluster/static-vault-access-policy.bicep' = {
    name: 'accessPolicy'
    scope: resourceGroup(staticTestSecretsKeyvaultGroup)
    params: {
        vaultName: staticTestSecretsKeyvaultName
        tenantId: subscription().tenantId
        objectId: cluster.outputs.secretProviderObjectId
    }
}

output STATIC_TEST_SECRETS_KEYVAULT string = staticTestSecretsKeyvaultName
output CLUSTER_KEYVAULT string = keyvault.outputs.keyvaultName
output SECRET_PROVIDER_CLIENT_ID string = cluster.outputs.secretProviderClientId
output CLUSTER_NAME string = cluster.outputs.clusterName
output CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.containerRegistryName
output APPINSIGHTS_KEY_SECRET_NAME string = appInsightsInstrumentationKeySecretName
output RESOURCE_GROUP string = group.name
output TENANT_ID string = subscription().tenantId
