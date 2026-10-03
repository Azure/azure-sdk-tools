using './main.bicep'

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')
var deployedImage = readEnvironmentVariable('SERVICE_AGENT_SERVER_IMAGE_NAME', '')
var registryEndpoint = readEnvironmentVariable('AZURE_CONTAINER_REGISTRY_ENDPOINT', '${readEnvironmentVariable('CONTAINER_REGISTRY_NAME', '')}.azurecr.io')

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param containerImage = !empty(deployedImage) && startsWith(deployedImage, '${registryEndpoint}/') ? deployedImage : '${registryEndpoint}/${readEnvironmentVariable('AGENT_SERVER_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-agent-server:${env}')}'
param managedIdentityClientId = readEnvironmentVariable('MANAGED_IDENTITY_CLIENT_ID', '')
param frontendIdentityClientId = readEnvironmentVariable('BOT_ID', '')
param serverApplicationClientId = readEnvironmentVariable('SERVER_APPLICATION_CLIENT_ID', '')
param sharedIdentityName = readEnvironmentVariable('MANAGED_IDENTITY_NAME', '')
param frontendIdentityName = readEnvironmentVariable('FRONTEND_SITE_NAME', '')
param appConfigName = readEnvironmentVariable('APP_CONFIG_NAME', '')
param actionGroupName = readEnvironmentVariable('ACTION_GROUP_NAME', '')
param agentServerAppServicePlanNameOverride = readEnvironmentVariable('AGENT_SERVER_APP_SERVICE_PLAN_NAME', '')
param agentServerLogWorkspaceNameOverride = readEnvironmentVariable('AGENT_SERVER_LOG_WORKSPACE_NAME', '')
param agentServerLogWorkspaceResourceId = readEnvironmentVariable('AGENT_SERVER_LOG_WORKSPACE_RESOURCE_ID', '')
param agentServerSiteNameOverride = readEnvironmentVariable('AGENT_SERVER_SITE_NAME_OVERRIDE', '')
param agentServerAppInsightsNameOverride = readEnvironmentVariable('AGENT_SERVER_APP_INSIGHTS_NAME', '')
param agentServerAlertNameOverride = readEnvironmentVariable('AGENT_SERVER_ALERT_NAME', '')
param agentServerAppInsightsFlowType = readEnvironmentVariable('AGENT_SERVER_APP_INSIGHTS_FLOW_TYPE', 'Bluefield')
param agentServerAppInsightsRequestSource = readEnvironmentVariable('AGENT_SERVER_APP_INSIGHTS_REQUEST_SOURCE', 'rest')
