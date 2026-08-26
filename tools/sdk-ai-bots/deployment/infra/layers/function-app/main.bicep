targetScope = 'resourceGroup'

@description('Azure region for the Function App, plan, and Application Insights.')
param location string

@description('Container image (registry/repository:tag) the Function App runs.')
param containerImage string

@description('Client ID of the user-assigned managed identity the Function App runs as.')
param managedIdentityClientId string

@description('Client ID of the frontend bot identity used as the Bot Framework activity recipient.')
param botId string

@description('Storage account name used by the Functions runtime (identity-based AzureWebJobsStorage).')
param storageAccountName string

@description('Resource ID of the user-assigned managed identity the Function App runs as.')
param managedIdentityResourceId string

@description('Name of the Key Vault the AdoTokenRefresh function writes the ado-token secret to.')
param keyVaultName string

// Resource-name overrides — see the shared-resources layer.
@description('Name of the Function App service plan.')
param functionAppServicePlanNameOverride string = ''

@description('Name of the Log Analytics workspace backing Function App insights.')
param functionLogWorkspaceNameOverride string = ''

@description('Name of the Function App (also used for its Application Insights component).')
param functionAppNameOverride string = ''

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var functionAppServicePlanName = !empty(functionAppServicePlanNameOverride) ? functionAppServicePlanNameOverride : 'azuresdkqabot-functionserviceplan-${suffix}'
var functionLogWorkspaceName = !empty(functionLogWorkspaceNameOverride) ? functionLogWorkspaceNameOverride : 'azuresdkqabot-function-log-${suffix}'
var functionAppName = !empty(functionAppNameOverride) ? functionAppNameOverride : 'azuresdkqabot-function-${suffix}'

resource serverfarm 'Microsoft.Web/serverfarms@2025-05-01' = {
  name: functionAppServicePlanName
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
  name: functionLogWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: functionAppName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource site 'Microsoft.Web/sites@2025-05-01' = {
  name: functionAppName
  tags: {
    'hidden-link: /app-insights-resource-id': component.id
    'azd-service-name': 'function-app'
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
      // Which user-assigned identity to use for the ACR pull. Required because
      // this app has only a user-assigned identity — without this the platform
      // falls back to the (nonexistent) system-assigned identity and the
      // container image pull fails, leaving the Functions host unavailable (503).
      acrUserManagedIdentityID: managedIdentityClientId
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
          name: 'BOT_ID'
          value: botId
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccountName
        }
        {
          name: 'KEY_VAULT_NAME'
          value: keyVaultName
        }
        {
          // Full Key Vault URL read by the AdoTokenRefresh function
          // (src/functions/AdoTokenRefresh.ts → process.env.KEY_VAULT_URL) to
          // write the 'ado-token' secret. Without it the function throws on every
          // run and the secret is never created.
          name: 'KEY_VAULT_URL'
          value: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}'
        }
        {
          // Azure DevOps AAD resource scope the function mints a token for
          // (well-known ADO app id 499b84ac-.../.default). Must match the
          // ADO_RESOURCE_SCOPE seeded into App Configuration
          // (deployment/hooks/lib/seed-app-config.ts). Read by
          // AdoTokenRefresh.ts → credential.getToken(process.env.ADO_RESOURCE_SCOPE).
          name: 'ADO_RESOURCE_SCOPE'
          value: '499b84ac-1321-427f-aa17-267ca6975798/.default'
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
output FUNCTION_APP_NAME string = site.name
output FUNCTION_CONTAINER_IMAGE string = containerImage
