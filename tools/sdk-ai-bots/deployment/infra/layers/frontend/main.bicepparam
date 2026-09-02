using './main.bicep'

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')
var envSuffixTitleCase = '${toUpper(substring(env, 0, 1))}${substring(env, 1)}'
var serverAudience = readEnvironmentVariable('SERVER_AUDIENCE', '')
var tableNameOverride = readEnvironmentVariable('AZURE_TABLE_NAME_FOR_CONVERSATION', '')
var ragServiceScopeOverride = readEnvironmentVariable('RAG_SERVICE_SCOPE', '')
var displayNameOverride = readEnvironmentVariable('TEAMS_BOT_FULL_DISPLAY_NAME', '')

param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME', '')
param containerRegistryName = readEnvironmentVariable('CONTAINER_REGISTRY_NAME', '')
param frontendImageRepository = readEnvironmentVariable('FRONTEND_IMAGE_REPOSITORY', 'azure-sdk-qa-bot:${env}')
param frontendBaseNameOverride = readEnvironmentVariable('FRONTEND_SITE_NAME', '')
param botServiceNameOverride = readEnvironmentVariable('BOT_SERVICE_NAME', '')
param frontendAppInsightsNameOverride = readEnvironmentVariable('FRONTEND_APP_INSIGHTS_NAME', '')
param frontendEmailActionGroupNameOverride = readEnvironmentVariable('FRONTEND_EMAIL_ACTION_GROUP_NAME', '')
param frontendDiagnosticSettingNameOverride = readEnvironmentVariable('FRONTEND_DIAGNOSTIC_SETTING_NAME', '')
param frontendHealthTestNameOverride = readEnvironmentVariable('FRONTEND_HEALTH_TEST_NAME', '')
param frontendServerErrorsAlertNameOverride = readEnvironmentVariable('FRONTEND_SERVER_ERRORS_ALERT_NAME', '')
param frontendHealthCheckAlertNameOverride = readEnvironmentVariable('FRONTEND_HEALTH_CHECK_ALERT_NAME', '')
param frontendDeleteLockNameOverride = readEnvironmentVariable('FRONTEND_DELETE_LOCK_NAME', '')
param azureTableNameForConversation = !empty(tableNameOverride) ? tableNameOverride : 'TeamsChannelConversations${envSuffixTitleCase}'
param ragServiceScope = !empty(ragServiceScopeOverride) ? ragServiceScopeOverride : 'api://${serverAudience}/.default'
param teamsBotFullDisplayName = !empty(displayNameOverride) ? displayNameOverride : (env == 'prod' ? 'Azure SDK Q&A Bot' : 'Azure SDK Q&A Bot ${env}')