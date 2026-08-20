// `azd` parameter file consumed by `azd provision`.
//
// Env-specific values are derived from environment variables that azd
// automatically sets (AZURE_ENV_NAME, AZURE_LOCATION) plus values that azd
// loads from .azure/<env>/.env when an environment is selected.
//
// Pipelines and local development both use this file. environment-suite.yaml
// is exported as pipeline variables or synchronized into the local azd
// environment, so both paths compile the same template with the same values.

using './main.bicep'

// ── azd-managed values (set automatically when an env is selected) ───────────
param location   = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
// AZURE_AI_DEPLOYMENTS_LOCATION is the var read by the azure.ai.agents extension
// for its model-catalog pre-check; AZURE_AI_LOCATION is the fallback.
param aiLocation = readEnvironmentVariable('AZURE_AI_DEPLOYMENTS_LOCATION', readEnvironmentVariable('AZURE_AI_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2')))

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')
var deployedAgentServerImage = readEnvironmentVariable('SERVICE_AGENT_SERVER_IMAGE_NAME', '')

param envName = env

// ── Sourced from environment-suite.yaml via scripts/sync-env-suite.ps1 ───────
// The sync script writes these into .azure/<env>/.env with `azd env set`.
// Fallbacks are used only when a value is missing (e.g., first-run before sync).
param resourceGroupName              = readEnvironmentVariable('AZURE_RESOURCE_GROUP',              'rg-azuresdkqabot-${env}')
param functionImageRepository        = readEnvironmentVariable('FUNCTION_IMAGE_REPOSITORY',        'azure-sdk-qa-bot-function:${env}')
// Preserve the last immutable image recorded by azd deploy. This prevents a
// later provision from replacing a working image with a mutable environment
// tag that may not exist in ACR.
param agentServerImageRepository     = !empty(deployedAgentServerImage) ? last(split(deployedAgentServerImage, '/')) : readEnvironmentVariable('AGENT_SERVER_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-agent-server:${env}')
param frontendImageRepository        = readEnvironmentVariable('FRONTEND_IMAGE_REPOSITORY',        'azure-sdk-qa-bot:${env}')

// Resource names for the primary web apps. Kept in sync with env-suite so the
// CD pipelines (which read the same values as FRONTEND_SITE_NAME / etc.) and
// the bicep templates agree on the site names.
param frontendBaseName               = readEnvironmentVariable('FRONTEND_SITE_NAME',               '')
param agentServerSiteName            = readEnvironmentVariable('AGENT_SERVER_SITE_NAME',           '')
param functionAppName                = readEnvironmentVariable('FUNCTION_APP_NAME',                '')

// Optional resource-name overrides. Empty values retain main.bicep's derived
// names; existing environments (especially prod) pin their deployed names via
// environment-suite.yaml's bicepOverrides map.
param managedIdentityName            = readEnvironmentVariable('MANAGED_IDENTITY_NAME',            '')
param actionGroupName                = readEnvironmentVariable('ACTION_GROUP_NAME',                '')
param keyVaultName                   = readEnvironmentVariable('KEY_VAULT_NAME',                   '')
param appConfigName                  = readEnvironmentVariable('APP_CONFIG_NAME',                  '')
param searchServiceName              = readEnvironmentVariable('SEARCH_SERVICE_NAME',              '')
param containerRegistryName          = readEnvironmentVariable('CONTAINER_REGISTRY_NAME', readEnvironmentVariable('ACR_NAME', ''))
param storageAccountName             = readEnvironmentVariable('STORAGE_ACCOUNT_NAME',             '')
param cosmosDbAccountName            = readEnvironmentVariable('COSMOS_DB_ACCOUNT_NAME',           '')

param aiResourceName                 = readEnvironmentVariable('AI_RESOURCE_NAME',                 '')
param aiProjectName                  = readEnvironmentVariable('AI_PROJECT_NAME',                  '')
param agentLogWorkspaceName          = readEnvironmentVariable('AGENT_LOG_WORKSPACE_NAME',         '')
param agentAppInsightsName           = readEnvironmentVariable('AGENT_APP_INSIGHTS_NAME',          '')

param frontendAppInsightsName        = readEnvironmentVariable('FRONTEND_APP_INSIGHTS_NAME',       '')
param frontendEmailActionGroupName   = readEnvironmentVariable('FRONTEND_EMAIL_ACTION_GROUP_NAME', '')
param frontendDiagnosticSettingName  = readEnvironmentVariable('FRONTEND_DIAGNOSTIC_SETTING_NAME', '')
param frontendHealthTestName         = readEnvironmentVariable('FRONTEND_HEALTH_TEST_NAME',        '')
param frontendServerErrorsAlertName  = readEnvironmentVariable('FRONTEND_SERVER_ERRORS_ALERT_NAME','')
param frontendHealthCheckAlertName   = readEnvironmentVariable('FRONTEND_HEALTH_CHECK_ALERT_NAME', '')
param frontendDeleteLockName         = readEnvironmentVariable('FRONTEND_DELETE_LOCK_NAME',        '')

param agentServerAppServicePlanName  = readEnvironmentVariable('AGENT_SERVER_APP_SERVICE_PLAN_NAME', '')
param agentServerLogWorkspaceName    = readEnvironmentVariable('AGENT_SERVER_LOG_WORKSPACE_NAME',   '')
param agentServerAppInsightsName     = readEnvironmentVariable('AGENT_SERVER_APP_INSIGHTS_NAME',    '')
param agentServerAlertName           = readEnvironmentVariable('AGENT_SERVER_ALERT_NAME',           '')

param functionAppServicePlanName     = readEnvironmentVariable('FUNCTION_APP_SERVICE_PLAN_NAME',   '')
param functionLogWorkspaceName       = readEnvironmentVariable('FUNCTION_LOG_WORKSPACE_NAME',      '')

param integrationAccountName        = readEnvironmentVariable('INTEGRATION_ACCOUNT_NAME',         '')
param teamsConnectionName            = readEnvironmentVariable('TEAMS_CONNECTION_NAME',            '')
param azureBlobConnectionName        = readEnvironmentVariable('AZURE_BLOB_CONNECTION_NAME',       '')
param documentDbConnectionName       = readEnvironmentVariable('DOCUMENT_DB_CONNECTION_NAME',      '')
param logicAppWorkflowName           = readEnvironmentVariable('LOGIC_APP_WORKFLOW_NAME',           '')
param logicAppAlertName              = readEnvironmentVariable('LOGIC_APP_ALERT_NAME',              '')

// ── Per-env values (read from .azure/<env>/.env) ─────────────────────────────
// SERVER_AUDIENCE is auto-populated by the preprovision hook
// (hooks/preprovision.ts → ensureServerAudience), which creates or looks up
// an Entra ID app registration named `azuresdkqabot-server-<env>` via
// `az ad app` and persists its clientId with `azd env set`. Only set this
// manually to pin a specific external app registration.
//
// TEAMS_GROUP_ID / TEAMS_CHANNEL_IDS are set per-env with:
//   azd env set TEAMS_GROUP_ID <guid>
//   azd env set TEAMS_CHANNEL_IDS '19:foo@thread.skype,19:bar@thread.skype'
param serverAudience  = readEnvironmentVariable('SERVER_AUDIENCE', '')

// ── Developer access (auto-detected by preprovision hook) ─────────────────
// The preprovision hook detects the deployer's object ID and type and persists
// them. Override by setting DEVELOPER_PRINCIPAL_ID / DEVELOPER_PRINCIPAL_TYPE
// in the azd env (or via environment-suite bicepOverrides for prod groups).
param developerGroupObjectId  = readEnvironmentVariable('DEVELOPER_PRINCIPAL_ID', '')
param developerPrincipalType  = readEnvironmentVariable('DEVELOPER_PRINCIPAL_TYPE', 'User')
param cosmosDbLocation        = readEnvironmentVariable('COSMOS_DB_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2'))
param teamsGroupId    = readEnvironmentVariable('TEAMS_GROUP_ID', '3e17dcb0-4257-4a30-b843-77f47f1d4121')
param teamsChannelIds = split(readEnvironmentVariable('TEAMS_CHANNEL_IDS', '19:de3fce22c2994be18cac50502c55f717@thread.skype'), ',')

// When the Teams managed API connection is already authorized (OAuth
// "Connected"), hooks/preprovision.ts sets CREATE_TEAMS_CONNECTION=false so
// this provision leaves the token binding untouched. Default true so the
// first provision creates the connection shell.
param createTeamsConnection = readEnvironmentVariable('CREATE_TEAMS_CONNECTION', 'true') == 'true'

// Preserve the complete workflow on subsequent provisions. The preprovision
// hook sets this to false only when no Logic App workflow exists yet.
param includeLogicAppWorkflowDefinition = readEnvironmentVariable('INCLUDE_LOGIC_APP_WORKFLOW_DEFINITION', 'false') == 'true'

param azureTableNameForConversation = readEnvironmentVariable('AZURE_TABLE_NAME_FOR_CONVERSATION', '')
param ragServiceScope                = readEnvironmentVariable('RAG_SERVICE_SCOPE',                '')
param teamsBotFullDisplayName        = readEnvironmentVariable('TEAMS_BOT_FULL_DISPLAY_NAME',      '')
