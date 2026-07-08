targetScope = 'resourceGroup'

@description('Name of the shared storage account (created by the shared resources module) that the frontend identity needs data-plane access to.')
param storageAccountName string

@description('Name of the shared container registry (created by the shared resources module) that hosts the frontend image.')
param containerRegistryName string

@description('Frontend container image repository and tag (e.g. `azure-sdk-qa-bot:dev`), pushed to the shared registry by CI / `frontend-predeploy.ts`.')
param frontendImageRepository string

// Resource-name overrides — see qaBotSharedResources/sharedResources.bicep. In
// prod the frontend identity, Log Analytics workspace, App Service plan, web
// app, and bot service share the same short name (`azsdkqabot`).
@description('Base name shared by the frontend managed identity, Log Analytics workspace, App Service plan, web app, and bot service.')
param frontendBaseName string = 'azsdkqabot-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the frontend Application Insights component.')
param frontendAppInsightsName string = 'azsdkqabot-insights-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the email-alerts action group.')
param frontendEmailActionGroupName string = 'azsdkqabot-email-alerts-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the frontend site diagnostic setting.')
param frontendDiagnosticSettingName string = 'azsdkqabot-diagnostic-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the health-check web test.')
param frontendHealthTestName string = 'azsdkqabot-health-test-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the server-errors metric alert.')
param frontendServerErrorsAlertName string = 'azsdkqabot-server-errors-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the health-check-failure metric alert.')
param frontendHealthCheckAlertName string = 'azsdkqabot-health-check-failure-${substring(uniqueString(resourceGroup().id), 0, 6)}'

@description('Name of the delete lock guarding the frontend resource group.')
param frontendDeleteLockName string = 'azsdkqabot-delete-lock-${substring(uniqueString(resourceGroup().id), 0, 6)}'

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-05-31-preview' = {
  name: frontendBaseName
  location: 'westus2'
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: frontendBaseName
  location: 'westus2'
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: -1
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: frontendAppInsightsName
  location: 'westus2'
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: component
  name: guid(component.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2024-10-01-preview' = {
  name: frontendEmailActionGroupName
  location: 'Global'
  properties: {
    groupShortName: 'EmailAlerts'
    emailReceivers: [
      {
        name: 'email-0'
        emailAddress: 'chunyu@microsoft.com'
        useCommonAlertSchema: true
      }
      {
        name: 'email-1'
        emailAddress: 'jiaqzhang@microsoft.com'
        useCommonAlertSchema: true
      }
      {
        name: 'email-2'
        emailAddress: 'Renhe.Li@microsoft.com'
        useCommonAlertSchema: true
      }
      {
        name: 'email-3'
        emailAddress: 'jialinhuang@microsoft.com'
        useCommonAlertSchema: true
      }
    ]
  }
}

resource sharedRegistry 'Microsoft.ContainerRegistry/registries@2026-01-01-preview' existing = {
  name: containerRegistryName
}

// Grant the frontend identity AcrPull on the shared registry so the site can
// pull its container image using this user-assigned identity.
resource roleAssignment2 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sharedRegistry
  name: guid(sharedRegistry.id, userAssignedIdentity.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource serverfarm 'Microsoft.Web/serverfarms@2025-05-01' = {
  name: frontendBaseName
  location: 'westus2'
  properties: {
    reserved: true
  }
  sku: {
    name: 'B3'
    tier: 'Basic'
    size: 'B3'
    family: 'B'
    capacity: 1
  }
  kind: 'linux'
}

resource site 'Microsoft.Web/sites@2025-05-01' = {
  name: frontendBaseName
  location: 'westus2'
  tags: {
    'azd-service-name': 'frontend'
  }
  properties: {
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: serverfarm.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${sharedRegistry.properties.loginServer}/${frontendImageRepository}'
      alwaysOn: true
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: userAssignedIdentity.properties.clientId
      ftpsState: 'FtpsOnly'
      httpLoggingEnabled: true
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '3978'
        }
        {
          name: 'RUNNING_ON_AZURE'
          value: '1'
        }
        {
          name: 'BOT_ID'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'BOT_TENANT_ID'
          value: userAssignedIdentity.properties.tenantId
        }
        {
          name: 'BOT_TYPE'
          value: 'UserAssignedMsi'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: userAssignedIdentity.properties.clientId
        }
        {
          name: 'RAG_SERVICE_SCOPE'
          value: ''
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccountName
        }
        {
          name: 'AZURE_TABLE_NAME_FOR_CONVERSATION'
          value: ''
        }
        {
          name: 'BLOB_CONTAINER_NAME'
          value: ''
        }
        {
          name: 'CHANNEL_CONFIG_BLOB_NAME'
          value: ''
        }
        {
          name: 'TENANT_CONFIG_BLOB_NAME'
          value: ''
        }
        {
          name: 'GITHUB_APP_ID'
          value: ''
        }
        {
          name: 'GITHUB_APP_KEY_VAULT_NAME'
          value: ''
        }
        {
          name: 'GITHUB_APP_KEY_NAME'
          value: ''
        }
        {
          name: 'GITHUB_APP_INSTALL_OWNER'
          value: ''
        }
        {
          name: 'TEAMS_BOT_FULL_DISPLAY_NAME'
          value: 'Azure SDK Q&A Bot'
        }
      ]
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  kind: 'app,linux,container'
}

resource diagnosticSetting 'microsoft.insights/diagnosticSettings@2021-05-01-preview' = {
  scope: site
  name: frontendDiagnosticSettingName
  properties: {
    workspaceId: workspace.id
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        timeGrain: 'PT1M'
        enabled: true
      }
    ]
  }
}

resource botService 'Microsoft.BotService/botServices@2023-09-15-preview' = {
  name: frontendBaseName
  properties: {
    displayName: 'Azure SDK Q&A Bot'
    endpoint: 'https://${site.properties.defaultHostName}/api/messages'
    msaAppId: userAssignedIdentity.properties.clientId
    msaAppTenantId: userAssignedIdentity.properties.tenantId
    msaAppType: 'UserAssignedMSI'
    msaAppMSIResourceId: userAssignedIdentity.id
  }
  location: 'global'
  sku: {
    name: 'F0'
  }
  kind: 'azurebot'
}

resource channel 'Microsoft.BotService/botServices/channels@2023-09-15-preview' = {
  name: 'MsTeamsChannel'
  parent: botService
  properties: {
    channelName: 'MsTeamsChannel'
    location: 'global'
  }
  location: 'global'
}

resource webtest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: frontendHealthTestName
  location: 'westus2'
  tags: {
    'hidden-link:${component.id}': 'Resource'
  }
  kind: 'ping'
  properties: {
    SyntheticMonitorId: 'azsdkqabot-health-test-${substring(uniqueString(resourceGroup().id), 0, 6)}'
    Name: 'azsdkqabot Health Check Test'
    Description: 'Health check test to monitor server availability'
    Enabled: true
    Frequency: 300
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
      {
        Id: 'us-va-ash-azr'
      }
      {
        Id: 'emea-nl-ams-azr'
      }
    ]
    Configuration: {
      WebTest: '<WebTest Name="azsdkqabot Health Check Test" Id="ABD48585-0831-40CB-9069-682EA6BB3584" Enabled="True" CssProjectStructure="" CssIteration="" Timeout="30" WorkItemIds="" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" Description="" CredentialUserName="" CredentialPassword="" PreAuthenticate="True" Proxy="default" StopOnError="False" RecordedResultFile="" ResultsLocale=""><Items><Request Method="GET" Guid="a5f10126-e4cd-570d-961c-cea43999a201" Version="1.1" Url="https://${site.properties.defaultHostName}/health" ThinkTime="0" Timeout="30" ParseDependentRequests="False" FollowRedirects="True" RecordResult="True" Cache="False" ResponseTimeGoal="0" Encoding="utf-8" ExpectedHttpStatusCode="200" ExpectedResponseUrl="" ReportingName="" IgnoreHttpStatusCode="False" /></Items></WebTest>'
    }
  }
}

resource metricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: frontendServerErrorsAlertName
  location: 'Global'
  properties: {
    description: 'Alert when server returns 5xx HTTP errors'
    severity: 3
    enabled: true
    scopes: [
      site.id
    ]
    evaluationFrequency: 'PT1M'
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          operator: 'GreaterThan'
          threshold: 0
          name: 'Http5xxCriteria'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          dimensions: []
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
  }
}

resource metricAlert2 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: frontendHealthCheckAlertName
  location: 'Global'
  properties: {
    description: 'Alert when health check fails (server is down or not responding)'
    severity: 3
    enabled: true
    scopes: [
      site.id
    ]
    evaluationFrequency: 'PT1M'
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          name: 'HealthCheckFailedCriteria'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'HealthCheckStatus'
          dimensions: []
          timeAggregation: 'Average'
          operator: 'LessThan'
          threshold: 100
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
  }
}

resource lock 'Microsoft.Authorization/locks@2020-05-01' = {
  name: frontendDeleteLockName
  properties: {
    level: 'CanNotDelete'
    notes: 'This resource group is protected from deletion. Contact the Azure SDK team if you need to remove it.'
  }
}

module azureSdkQaBotModule './azureSdkQaBotModule.bicep' = {
  name: 'azureSdkQaBotModule'
  params: {
    storageAccountName: storageAccountName
    userAssignedIdentityPropertiesPrincipalId: userAssignedIdentity.properties.principalId
  }
}

// Outputs
output botIdentityName string = userAssignedIdentity.name
output botBaseUrl string = 'https://${site.properties.defaultHostName}'
output botAudience string = userAssignedIdentity.properties.clientId
