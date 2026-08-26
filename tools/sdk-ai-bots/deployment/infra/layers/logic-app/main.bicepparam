using './main.bicep'

param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
param teamsGroupId = readEnvironmentVariable('TEAMS_GROUP_ID', '3e17dcb0-4257-4a30-b843-77f47f1d4121')
param teamsChannelIds = split(readEnvironmentVariable('TEAMS_CHANNEL_IDS', '19:de3fce22c2994be18cac50502c55f717@thread.skype'), ',')
param serverAudience = readEnvironmentVariable('SERVER_AUDIENCE', '')
param serverBaseUrl = readEnvironmentVariable('SERVER_BASE_URL', '')
param botBaseUrl = readEnvironmentVariable('BOT_BASE_URL', '')
param botAudience = readEnvironmentVariable('BOT_AUDIENCE', '')
param blobStorageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT_NAME', '')
param managedIdentityName = readEnvironmentVariable('MANAGED_IDENTITY_NAME', '')
param botIdentityName = readEnvironmentVariable('BOT_IDENTITY_NAME', '')
param functionAppName = readEnvironmentVariable('FUNCTION_APP_NAME', '')
param integrationAccountNameOverride = readEnvironmentVariable('INTEGRATION_ACCOUNT_NAME_OVERRIDE', '')
param teamsConnectionNameOverride = readEnvironmentVariable('TEAMS_CONNECTION_NAME_OVERRIDE', '')
param azureBlobConnectionNameOverride = readEnvironmentVariable('AZURE_BLOB_CONNECTION_NAME_OVERRIDE', '')
param documentDbConnectionNameOverride = readEnvironmentVariable('DOCUMENT_DB_CONNECTION_NAME_OVERRIDE', '')
param logicAppWorkflowNameOverride = readEnvironmentVariable('LOGIC_APP_WORKFLOW_NAME_OVERRIDE', '')
param logicAppAlertNameOverride = readEnvironmentVariable('LOGIC_APP_ALERT_NAME_OVERRIDE', '')
param actionGroupName = readEnvironmentVariable('ACTION_GROUP_NAME', '')
param includeWorkflowDefinition = readEnvironmentVariable('INCLUDE_LOGIC_APP_WORKFLOW_DEFINITION', 'false') == 'true'
param workflowEnabled = readEnvironmentVariable('LOGIC_APP_WORKFLOW_ENABLED', 'false') == 'true'
param createTeamsConnection = readEnvironmentVariable('CREATE_TEAMS_CONNECTION', 'true') == 'true'
