targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string

@description('Full container image reference for the agent server (registry/repository:tag).')
param containerImage string

@description('Client ID of the qabot-identity managed identity; used by DefaultAzureCredential to select the identity.')
param managedIdentityClientId string

@description('Client ID of the frontend managed identity that calls the agent server.')
param frontendIdentityClientId string

@description('Client ID (audience) of the Entra app registration that fronts the agent server, used by App Service authentication (Easy Auth).')
param serverAudience string

@description('Name of the shared user-assigned managed identity attached to the site.')
param sharedIdentityName string

@description('Name of the frontend/bot user-assigned managed identity also attached to the site.')
param frontendIdentityName string

@description('Azure AI Services account name the agent server talks to.')
param aiResourceName string

@description('Azure AI project name.')
param aiProjectName string

@description('Cosmos DB account name.')
param cosmosDbAccountName string

@description('Storage account name.')
param storageAccountName string

@description('App Configuration store name.')
param appConfigName string

@description('Name of the shared action group notified by the metric alerts.')
param actionGroupName string

// Resource-name overrides — see the shared-resources layer for
// the rationale (prod's manually-built RG has different naming).
@description('Name of the agent-server App Service plan.')
param agentServerAppServicePlanNameOverride string = ''

@description('Name of the Log Analytics workspace backing agent-server Application Insights.')
param agentServerLogWorkspaceNameOverride string = ''

@description('Name of the agent-server web app.')
param agentServerSiteNameOverride string = ''

@description('Name of the Application Insights component paired with the agent server (prod uses a timestamped legacy name).')
param agentServerAppInsightsNameOverride string = ''

@description('Name of the metric alert on the agent-server site.')
param agentServerAlertNameOverride string = ''

var suffix = substring(uniqueString(resourceGroup().id), 0, 6)
var agentServerAppServicePlanName = !empty(agentServerAppServicePlanNameOverride) ? agentServerAppServicePlanNameOverride : 'azuresdkqabot-appserviceplan-${suffix}'
var agentServerLogWorkspaceName = !empty(agentServerLogWorkspaceNameOverride) ? agentServerLogWorkspaceNameOverride : 'azuresdkqabot-log-${suffix}'
var agentServerSiteName = !empty(agentServerSiteNameOverride) ? agentServerSiteNameOverride : 'azuresdkqabot-server-${suffix}'
var agentServerAppInsightsName = !empty(agentServerAppInsightsNameOverride) ? agentServerAppInsightsNameOverride : 'azuresdkqabot-server202510300250-${suffix}'
var agentServerAlertName = !empty(agentServerAlertNameOverride) ? agentServerAlertNameOverride : 'azuresdkqabot-alert-${suffix}'
var azureCliClientId = '04b07795-8ddb-461a-bbee-02f9e1bf7b46'

// User-assigned identities attached to the agent-server site.
var siteUserAssignedIdentities = {
  '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', sharedIdentityName)}': {}
  '${resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', frontendIdentityName)}': {}
}

resource serverfarm 'Microsoft.Web/serverfarms@2025-05-01' = {
  name: agentServerAppServicePlanName
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

// Log Analytics workspace backing the agent-server Application Insights component.
resource workspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' = {
  name: agentServerLogWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: agentServerAppInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    RetentionInDays: 90
    WorkspaceResourceId: workspace.id
  }
}

resource site 'Microsoft.Web/sites@2025-05-01' = {
  name: agentServerSiteName
  tags: {
    'hidden-link: /app-insights-resource-id': component.id
    'azd-service-name': 'agent-server'
  }
  location: location
  properties: {
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: serverfarm.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: true
      acrUseManagedIdentityCreds: true
      // Which user-assigned identity to use for the ACR pull. Required because
      // the site has multiple user-assigned identities — without this the
      // platform falls back to the (nonexistent) system-assigned identity and
      // the container image pull fails, leaving the site returning 503.
      acrUserManagedIdentityID: managedIdentityClientId
      ftpsState: 'FtpsOnly'
      httpLoggingEnabled: false
      minTlsVersion: '1.2'
      appSettings: [
        {
          // The agent-server container listens on :8089. App Service must be
          // told this port or it probes the default 8080 and returns 503.
          name: 'WEBSITES_PORT'
          value: '8089'
        }
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
          value: component.properties.ConnectionString
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
          // The Python agent server loads all other settings from App
          // Configuration at this endpoint.
          name: 'AZURE_APPCONFIG_ENDPOINT'
          value: 'https://${appConfigName}.azconfig.io'
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
        validation: {
          defaultAuthorizationPolicy: {
            allowedApplications: [
              managedIdentityClientId
              frontendIdentityClientId
              azureCliClientId
            ]
          }
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

resource serverMetricAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = {
  name: agentServerAlertName
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

// Outputs consumed by azd services and hooks.
output SERVER_BASE_URL string = 'https://${site.properties.defaultHostName}'
output AGENT_SERVER_SITE_NAME string = site.name
output AGENT_SERVER_IMAGE string = containerImage
