using './main.bicep'

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')
var deployedImage = readEnvironmentVariable('SERVICE_AGENT_SERVER_IMAGE_NAME', '')

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param containerImage = !empty(deployedImage) ? deployedImage : '${readEnvironmentVariable('AZURE_CONTAINER_REGISTRY_ENDPOINT', '')}/${readEnvironmentVariable('AGENT_SERVER_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-agent-server:${env}')}'
param managedIdentityClientId = readEnvironmentVariable('MANAGED_IDENTITY_CLIENT_ID', '')
param frontendIdentityClientId = readEnvironmentVariable('BOT_ID', '')
param serverApplicationClientId = readEnvironmentVariable('SERVER_APPLICATION_CLIENT_ID', '')
param sharedIdentityName = readEnvironmentVariable('MANAGED_IDENTITY_NAME', '')
param frontendIdentityName = readEnvironmentVariable('FRONTEND_SITE_NAME', '')
param appConfigName = readEnvironmentVariable('APP_CONFIG_NAME', '')
param actionGroupName = readEnvironmentVariable('ACTION_GROUP_NAME', '')
param agentServerAppServicePlanNameOverride = readEnvironmentVariable('AGENT_SERVER_APP_SERVICE_PLAN_NAME', '')
param agentServerLogWorkspaceNameOverride = readEnvironmentVariable('AGENT_SERVER_LOG_WORKSPACE_NAME', '')
param agentServerSiteNameOverride = readEnvironmentVariable('AGENT_SERVER_SITE_NAME_OVERRIDE', '')
param agentServerAppInsightsNameOverride = readEnvironmentVariable('AGENT_SERVER_APP_INSIGHTS_NAME', '')
param agentServerAlertNameOverride = readEnvironmentVariable('AGENT_SERVER_ALERT_NAME', '')
