targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string

@description('Full container image reference for the backend service (registry/repository:tag).')
param ragBasedBackendImage string

@description('Full container image reference for the agent server slot (registry/repository:tag).')
param agentBasedBackendImage string

@description('Client ID of the qabot-identity managed identity; used by DefaultAzureCredential to select the identity.')
param managedIdentityClientId string

@description('Client ID (audience) of the Entra app registration that fronts the backend, used by App Service authentication (Easy Auth).')
param serverAudience string

@description('Name of the shared user-assigned managed identity attached to the site and slot.')
param sharedIdentityName string

@description('Name of the frontend/bot user-assigned managed identity also attached to the site and slot.')
param frontendIdentityName string

@description('Azure AI Services account name the backend talks to.')
param aiResourceName string

@description('Azure AI project name.')
param aiProjectName string

@description('Azure AI Search service name.')
param searchServiceName string

@description('Cosmos DB account name.')
param cosmosDbAccountName string

@description('Storage account name.')
param storageAccountName string

@description('Key Vault name.')
param keyVaultName string

@description('App Configuration store name.')
param appConfigName string

@description('Name of the shared action group notified by the metric alerts.')
param actionGroupName string

// User-assigned identities attached to both the site and the agent slot.
var siteUserAssignedIdentities = {
  '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', sharedIdentityName)}': {}
  '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', frontendIdentityName)}': {}
}

resource serverfarm 'Microsoft.Web/serverfarms@2025-05-01' = {
  name: 'azuresdkqabot-appserviceplan-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    reserved: true
  }
  sku: {
    name: 'P0v3'
    tier: 'Premium0V3'
    size: 'P0v3'
    family: 'Pv3'
    capacity: 1
  }
  kind: 'linux'
}

// Log Analytics workspace backing the backend Application Insights components.
resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: 'azuresdkqabot-log-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource serverAppInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'azuresdkqabot-server-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource slotAppInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'azuresdkqabot-server202510300250-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource site 'Microsoft.Web/sites@2025-05-01' = {
  name: 'azuresdkqabot-server-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  tags: {
    'hidden-link: /app-insights-resource-id': slotAppInsights.id
  }
  location: location
  properties: {
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: serverfarm.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${ragBasedBackendImage}'
      alwaysOn: true
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
          name: 'AZURE_RESOURCE_GROUP'
          value: resourceGroup().name
        }
        {
          name: 'AI_PROJECT_NAME'
          value: aiProjectName
        }
        {
          name: 'AZURE_AI_RESOURCE_NAME'
          value: aiResourceName
        }
        {
          name: 'APP_INSIGHTS_CONNECTION_STRING'
          value: slotAppInsights.properties.ConnectionString
        }
        {
          name: 'AZURE_SEARCH_SERVICE_NAME'
          value: searchServiceName
        }
        {
          name: 'COSMOS_DB_ACCOUNT_NAME'
          value: cosmosDbAccountName
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
          name: 'APP_CONFIG_NAME'
          value: appConfigName
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: siteUserAssignedIdentities
  }
  kind: 'app,linux,container'
}

resource config 'Microsoft.Web/sites/config@2025-05-01' = {
  name: 'authsettingsV2'
  parent: site
  properties: {
    platform: {
      enabled: true
      runtimeVersion: '~1'
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: serverAudience
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'
        }
        login: {
          loginParameters: []
        }
      }
    }
    login: {
      tokenStore: {
        enabled: true
      }
    }
  }
}

resource slot 'Microsoft.Web/sites/slots@2025-05-01' = {
  name: 'agent'
  parent: site
  tags: {
    'hidden-link: /app-insights-resource-id': slotAppInsights.id
  }
  location: location
  properties: {
    serverFarmId: serverfarm.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${agentBasedBackendImage}'
      alwaysOn: true
      acrUseManagedIdentityCreds: true
      ftpsState: 'FtpsOnly'
      httpLoggingEnabled: false
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
          name: 'AZURE_RESOURCE_GROUP'
          value: resourceGroup().name
        }
        {
          name: 'AI_PROJECT_NAME'
          value: aiProjectName
        }
        {
          name: 'AZURE_AI_RESOURCE_NAME'
          value: aiResourceName
        }
        {
          name: 'APP_INSIGHTS_CONNECTION_STRING'
          value: slotAppInsights.properties.ConnectionString
        }
        {
          name: 'COSMOS_DB_ACCOUNT_NAME'
          value: cosmosDbAccountName
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccountName
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: siteUserAssignedIdentities
  }
  kind: 'app,linux,container'
}

resource serverMetricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: 'azuresdkqabot-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: 'global'
  properties: {
    severity: 3
    enabled: true
    scopes: [
      site.id
    ]
    evaluationFrequency: 'PT1M'
    autoMitigate: true
    targetResourceType: 'Microsoft.Web/sites'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: resourceId('Microsoft.Insights/actionGroups', actionGroupName)
        webHookProperties: {}
      }
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          operator: 'GreaterThan'
          threshold: 0
          name: 'Metric1'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          dimensions: []
          timeAggregation: 'Total'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
  }
}

resource slotMetricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: 'azuresdkqabot-agent-alert-${substring(uniqueString(resourceGroup().id), 0, 6)}'
  location: 'global'
  properties: {
    severity: 3
    enabled: true
    scopes: [
      slot.id
    ]
    evaluationFrequency: 'PT1M'
    autoMitigate: true
    targetResourceType: 'Microsoft.Web/sites/slots'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: resourceId('Microsoft.Insights/actionGroups', actionGroupName)
        webHookProperties: {}
      }
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          operator: 'GreaterThan'
          threshold: 0
          name: 'Metric1'
          metricNamespace: 'Microsoft.Web/sites/slots'
          metricName: 'Http5xx'
          dimensions: []
          timeAggregation: 'Total'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
  }
}

// Output
output serverBaseUrl string = 'https://${slot.properties.defaultHostName}'
