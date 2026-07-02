targetScope = 'resourceGroup'

@description('Azure region for the Function App, plan, and Application Insights.')
param location string

@description('Container image (registry/repository:tag) the Function App runs.')
param containerImage string

@description('Client ID of the user-assigned managed identity the Function App runs as.')
param managedIdentityClientId string

@description('Storage account name used by the Functions runtime (identity-based AzureWebJobsStorage).')
param storageAccountName string

@description('Resource ID of the user-assigned managed identity the Function App runs as.')
param managedIdentityResourceId string

resource serverfarm 'Microsoft.Web/serverfarms@2025-05-01' = {
  name: 'azuresdkqabot-functionserviceplan-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    elasticScaleEnabled: true
    maximumElasticWorkerCount: 20
    reserved: true
  }
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
    size: 'EP1'
    family: 'EP'
    capacity: 1
  }
  kind: 'elastic'
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'azuresdkqabot-function-log-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: 'azuresdkqabot-function-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource site 'Microsoft.Web/sites@2025-05-01' = {
  name: 'azuresdkqabot-function-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  tags: {
    'hidden-link: /app-insights-resource-id': component.id
  }
  location: location
  properties: {
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: serverfarm.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: false
      acrUseManagedIdentityCreds: true
      ftpsState: 'FtpsOnly'
      httpLoggingEnabled: false
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        {
          name: 'AZURE_TENANT_ID'
          value: tenant().tenantId
        }
        {
          name: 'AZURE_SUBSCRIPTION_ID'
          value: subscription().subscriptionId
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccountName
        }
        {
          name: 'KEY_VAULT_NAME'
          value: 'qabot-keyvalut-${substring(uniqueString(resourceGroup().id), 0, 6)}'
        }
        {
          name: 'APP_CONFIG_NAME'
          value: 'qabot-config-${substring(uniqueString(resourceGroup().id), 0, 6)}'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: component.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: component.properties.ConnectionString
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__clientId'
          value: managedIdentityClientId
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'node'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
      ]
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
        supportCredentials: false
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  kind: 'functionapp,linux,container'
}

// Output
output functionAppName string = site.name
