// Azure Table
@secure()
param azureStorageUrl string
@secure()
param azureTableNameForConversation string

// RAG
@secure()
param ragApiKey string
@secure()
param ragEndpoint string
@secure()
param ragTanentId string

// Channels
// - python
@secure()
param channelIdForPython string
@secure()
param channelIdForPythonDevInternal string
@secure()
param ragTanentIdForPython string
// - sdk onboarding
@secure()
param channelIdForSdkOnboardingDevInternal string
@secure()
param ragTanentIdForSdkOnboarding string


// Resources
@maxLength(20)
@minLength(4)
@description('Used to generate names for all resources in this file')
param resourceBaseName string

param serverfarmsName string = resourceBaseName
param webAppName string = resourceBaseName
param identityName string = resourceBaseName
param logAnalyticsName string = resourceBaseName
param location string = resourceGroup().location
param webAppSKU string

// Bot
@maxLength(42)
param botDisplayName string

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  location: location
  name: identityName
}

// Compute resources for your Web App
resource serverfarm 'Microsoft.Web/serverfarms@2021-02-01' = {
  kind: 'linux'
  location: location
  name: serverfarmsName
  sku: {
    name: webAppSKU
  }
  properties: {
    reserved: true
  }
}

// Web App that hosts your bot
resource webApp 'Microsoft.Web/sites@2021-02-01' = {
  kind: 'app,linux'
  location: location
  name: webAppName
  properties: {
    serverFarmId: serverfarm.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion: 'NODE|20-lts'
      appSettings: [
        // Web app
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1' // Run Azure App Service from a package file
        }
        {
          name: 'RUNNING_ON_AZURE'
          value: '1'
        }
        // Bot
        {
          name: 'BOT_ID'
          value: identity.properties.clientId
        }
        {
          name: 'BOT_TENANT_ID'
          value: identity.properties.tenantId
        }
        {
          name: 'BOT_TYPE'
          value: 'UserAssignedMsi'
        }
        // RAG
        {
          name: 'RAG_API_KEY'
          value: ragApiKey
        }
        {
          name: 'RAG_ENDPOINT'
          value: ragEndpoint
        }
        {
          name: 'RAG_TANENT_ID'
          value: ragTanentId
        }
        // Channels
        // - python
        {
          name: 'CHANNEL_ID_FOR_PYTHON'
          value: channelIdForPython
        }
        {
          name: 'CHANNEL_ID_FOR_PYTHON_DEV_INTERNAL'
          value: channelIdForPythonDevInternal
        }
        {
          name: 'RAG_TANENT_ID_FOR_PYTHON'
          value: ragTanentIdForPython
        }
        // - sdk onboarding
        {
          name: 'CHANNEL_ID_FOR_SDK_ONBOARDING_DEV_INTERNAL'
          value: channelIdForSdkOnboardingDevInternal
        }
        {
          name: 'RAG_TANENT_ID_FOR_SDK_ONBOARDING'
          value: ragTanentIdForSdkOnboarding
        }
        // Azure Table
        {
          name: 'AZURE_STORAGE_URL'
          value: azureStorageUrl
        }
        {
          name: 'AZURE_TABLE_NAME_FOR_CONVERSATION'
          value: azureTableNameForConversation
        }
      ]
      ftpsState: 'FtpsOnly'
      httpLoggingEnabled: true
      detailedErrorLoggingEnabled: true
      requestTracingEnabled: true
      logsDirectorySizeLimit: 35
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
}

// Create Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    retentionInDays: 30
  }
}

// Diagnostic settings for App Service
resource webAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${webApp.name}-diagnostic'
  scope: webApp
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}

// Register your web service as a bot with the Bot Framework
module azureBotRegistration './botRegistration/azurebot.bicep' = {
  name: 'Azure-Bot-registration'
  params: {
    resourceBaseName: resourceBaseName
    identityClientId: identity.properties.clientId
    identityResourceId: identity.id
    identityTenantId: identity.properties.tenantId
    botAppDomain: webApp.properties.defaultHostName
    botDisplayName: botDisplayName
  }
}

// The output will be persisted in .env.{envName}. Visit https://aka.ms/teamsfx-actions/arm-deploy for more details.
output BOT_AZURE_APP_SERVICE_RESOURCE_ID string = webApp.id
output BOT_DOMAIN string = webApp.properties.defaultHostName
output BOT_ID string = identity.properties.clientId
output BOT_TENANT_ID string = identity.properties.tenantId
