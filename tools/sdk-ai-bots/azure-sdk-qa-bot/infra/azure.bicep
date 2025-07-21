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
      healthCheckPath: '/health'
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

// Create Application Insights for better monitoring
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${resourceBaseName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Grant the managed identity Monitoring Metrics Publisher role on Application Insights
resource appInsightsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appInsights.id, identity.id, 'MonitoringMetricsPublisher')
  scope: appInsights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb') // Monitoring Metrics Publisher
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Create Action Group for email notifications
resource emailActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${resourceBaseName}-email-alerts'
  location: 'Global'
  properties: {
    groupShortName: 'EmailAlerts'
    enabled: true
    emailReceivers: [
      {
        name: 'wanl-email'
        emailAddress: 'wanl@microsoft.com'
        useCommonAlertSchema: true
      }
    ]
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

// Alert rule for HTTP 5xx errors (server errors)
resource serverErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${resourceBaseName}-server-errors'
  location: 'Global'
  properties: {
    description: 'Alert when server returns 5xx HTTP errors'
    severity: 1
    enabled: true
    scopes: [
      webApp.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xxCriteria'
          metricName: 'Http5xx'
          metricNamespace: 'Microsoft.Web/sites'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: emailActionGroup.id
      }
    ]
  }
}

// Web test for health check endpoint monitoring
resource healthCheckWebTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: '${resourceBaseName}-health-test'
  location: location
  kind: 'ping'
  properties: {
    SyntheticMonitorId: '${resourceBaseName}-health-test'
    Name: '${resourceBaseName} Health Check Test'
    Description: 'Health check test to monitor server availability'
    Enabled: true
    Frequency: 300 // 5 minutes
    Timeout: 30
    Kind: 'ping'
    RetryEnabled: true
    Locations: [
      {
        Id: 'us-tx-sn1-azr'
      }
      {
        Id: 'us-il-ch1-azr'
      }
      {
        Id: 'us-ca-sjc-azr'
      }
    ]
    Configuration: {
      WebTest: '<WebTest Name="${resourceBaseName} Health Check Test" Id="ABD48585-0831-40CB-9069-682EA6BB3584" Enabled="True" CssProjectStructure="" CssIteration="" Timeout="30" WorkItemIds="" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" Description="" CredentialUserName="" CredentialPassword="" PreAuthenticate="True" Proxy="default" StopOnError="False" RecordedResultFile="" ResultsLocale=""><Items><Request Method="GET" Guid="a5f10126-e4cd-570d-961c-cea43999a201" Version="1.1" Url="https://${webApp.properties.defaultHostName}/health" ThinkTime="0" Timeout="30" ParseDependentRequests="False" FollowRedirects="True" RecordResult="True" Cache="False" ResponseTimeGoal="0" Encoding="utf-8" ExpectedHttpStatusCode="200" ExpectedResponseUrl="" ReportingName="" IgnoreHttpStatusCode="False" /></Items></WebTest>'
    }
  }
  tags: {
    'hidden-link:${appInsights.id}': 'Resource'
  }
}

// Alert rule for health check web test failures
resource healthCheckAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${resourceBaseName}-health-check-failure'
  location: 'Global'
  properties: {
    description: 'Alert when health check fails (server is down or not responding)'
    severity: 0
    enabled: true
    scopes: [
      healthCheckWebTest.id
      appInsights.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: healthCheckWebTest.id
      componentId: appInsights.id
      failedLocationCount: 2
    }
    actions: [
      {
        actionGroupId: emailActionGroup.id
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
