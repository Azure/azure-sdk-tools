targetScope = 'subscription'

param subscriptionId string = ''
param groupSuffix string
param clusterName string
param clusterLocation string = 'westus3'
param staticTestKeyvaultName string
param staticTestKeyvaultGroup string
param monitoringLocation string = 'centralus'
param defaultAgentPoolMinNodes int = 6
param defaultAgentPoolMaxNodes int = 20
param maintenanceWindowDay string = 'Monday'
param tags object
// AKS does not allow agentPool updates via existing managed cluster resources
param updateNodes bool = false

// Azure Developer Platform Team Group
// https://ms.portal.azure.com/#blade/Microsoft_AAD_IAM/GroupDetailsMenuBlade/Overview/groupId/56709ad9-8962-418a-ad0d-4b25fa962bae
param accessGroups array = [
    '56709ad9-8962-418a-ad0d-4b25fa962bae'
]

param groupName string

resource group 'Microsoft.Resources/resourceGroups@2020-10-01' = {
    name: groupName
    location: clusterLocation
    tags: tags
}

// Add unique suffix to monitoring resource names to simplify cross-resource queries.
// https://docs.microsoft.com/azure/azure-monitor/logs/cross-workspace-query#identifying-an-application
var resourceSuffix = uniqueString(group.id)

module logWorkspace 'monitoring/log-analytics-workspace.bicep' = {
    name: 'logs'
    scope: group
    params: {
        workspaceName: '${clusterName}-logs-${resourceSuffix}'
        location: monitoringLocation
    }
}

module appInsights 'monitoring/app-insights.bicep' = {
    name: 'appInsights'
    scope: group
    params: {
        name: '${clusterName}-ai-${resourceSuffix}'
        location: monitoringLocation
        workspaceId: logWorkspace.outputs.id
    }
}

module test_dashboard 'monitoring/stress-test-workbook.bicep' = {
    name: 'test_dashboard'
    scope: group
    params: {
        workbookDisplayName: 'Azure SDK Stress Testing - ${groupSuffix}'
        location: clusterLocation
        logAnalyticsResource: logWorkspace.outputs.id
    }
}

module status_dashboard 'monitoring/stress-status-workbook.bicep' = {
    name: 'status_dashboard'
    scope: group
    params: {
        workbookDisplayName: 'Stress Status - ${groupSuffix}'
        location: clusterLocation
        logAnalyticsResource: logWorkspace.outputs.id
    }
}

module cluster 'cluster/cluster.bicep' = {
    name: 'cluster'
    scope: group
    params: {
        updateNodes: updateNodes
        location: clusterLocation
        clusterName: clusterName
        defaultAgentPoolMinNodes: defaultAgentPoolMinNodes
        defaultAgentPoolMaxNodes: defaultAgentPoolMaxNodes
        maintenanceWindowDay: maintenanceWindowDay 
        tags: tags
        groupSuffix: groupSuffix
        workspaceId: logWorkspace.outputs.id
    }
}

module containerRegistry 'cluster/acr.bicep' = {
    name: 'containerRegistry'
    scope: group
    params: {
        registryName: '${replace(clusterName, '-', '')}${resourceSuffix}'
        location: clusterLocation
        objectIds: concat(accessGroups, array(cluster.outputs.kubeletIdentityObjectId))
    }
}

module storage 'cluster/storage.bicep' = {
  name: 'storage'
  scope: group
  params: {
    storageName: 'stressdebug${resourceSuffix}'
    fileShareName: 'stressfiles${resourceSuffix}'
    location: clusterLocation
  }
}

var appInsightsInstrumentationKeySecretName = 'appInsightsInstrumentationKey-${resourceSuffix}'
// Value is in dotenv format as it will be appended to stress test container dotenv files
var appInsightsInstrumentationKeySecretValue = 'APPINSIGHTS_INSTRUMENTATIONKEY=${appInsights.outputs.instrumentationKey}\n'
var appInsightsConnectionStringSecretName = 'appInsightsConnectionString-${resourceSuffix}'
// Value is in dotenv format as it will be appended to stress test container dotenv files
// Include double quotes since the connection string contains semicolons, which causes problems when sourcing the file
var appInsightsConnectionStringSecretValue = 'APPLICATIONINSIGHTS_CONNECTION_STRING="${appInsights.outputs.connectionString}"\n'

// Storage account information used for kubernetes fileshare volume mounting via the azure files csi driver
// See https://docs.microsoft.com/azure/aks/azure-files-volume#create-a-kubernetes-secret
// See https://docs.microsoft.com/azure/aks/azure-files-csi
var debugStorageKeySecretName = 'debugStorageKey-${resourceSuffix}'
var debugStorageKeySecretValue = storage.outputs.key
var debugStorageAccountSecretName = 'debugStorageAccount-${resourceSuffix}'
var debugStorageAccountSecretValue = storage.outputs.name

module keyvault 'cluster/keyvault.bicep' = {
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
                {
                    secretName: appInsightsConnectionStringSecretName
                    secretValue: appInsightsConnectionStringSecretValue
                }
                {
                    secretName: debugStorageKeySecretName
                    secretValue: debugStorageKeySecretValue
                }
                {
                    secretName: debugStorageAccountSecretName
                    secretValue: debugStorageAccountSecretValue
                }
            ]
        }
    }
}

module accessPolicy 'cluster/static-vault-access-policy.bicep' = {
    name: 'accessPolicy'
    scope: resourceGroup(staticTestKeyvaultGroup)
    params: {
        vaultName: staticTestKeyvaultName
        tenantId: subscription().tenantId
        objectId: cluster.outputs.secretProviderObjectId
    }
}

output STATIC_TEST_SECRETS_KEYVAULT string = staticTestKeyvaultName
output CLUSTER_TEST_SECRETS_KEYVAULT string = keyvault.outputs.keyvaultName
output SECRET_PROVIDER_CLIENT_ID string = cluster.outputs.secretProviderClientId
output CLUSTER_NAME string = cluster.outputs.clusterName
output CONTAINER_REGISTRY_NAME string = containerRegistry.outputs.containerRegistryName
output APPINSIGHTS_KEY_SECRET_NAME string = appInsightsInstrumentationKeySecretName
output APPINSIGHTS_CONNECTION_STRING_SECRET_NAME string = appInsightsConnectionStringSecretName
output DEBUG_STORAGE_KEY_SECRET_NAME string = debugStorageKeySecretName
output DEBUG_STORAGE_ACCOUNT_SECRET_NAME string = debugStorageAccountSecretName
output DEBUG_FILESHARE_NAME string = storage.outputs.fileShareName
output TEST_DASHBOARD_RESOURCE string = test_dashboard.outputs.id
output TEST_DASHBOARD_LINK string = 'https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/${test_dashboard.outputs.id}/workbook'
output STATUS_DASHBOARD_RESOURCE string = status_dashboard.outputs.id
output STATUS_DASHBOARD_LINK string = 'https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/${status_dashboard.outputs.id}/workbook'
output RESOURCE_GROUP string = group.name
output SUBSCRIPTION_ID string = subscriptionId
output TENANT_ID string = subscription().tenantId
