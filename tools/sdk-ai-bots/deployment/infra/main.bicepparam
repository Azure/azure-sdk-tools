// `azd` parameter file consumed by `azd provision`.
//
// Env-specific values are derived from environment variables that azd
// automatically sets (AZURE_ENV_NAME, AZURE_LOCATION) plus values that azd
// loads from .azure/<env>/.env when an environment is selected.
//
// Pipelines do NOT use this file. They invoke `az deployment sub create`
// directly with `--parameters @environments/<env>.parameters.json`, which is
// the authoritative per-environment config (full Teams channel list, real
// AAD app audience, etc.).

using './main.bicep'

// ── azd-managed values (set automatically when an env is selected) ───────────
param location   = readEnvironmentVariable('AZURE_LOCATION', 'westus2')
// AZURE_AI_DEPLOYMENTS_LOCATION is the var read by the azure.ai.agents extension
// for its model-catalog pre-check; AZURE_AI_LOCATION is the fallback.
param aiLocation = readEnvironmentVariable('AZURE_AI_DEPLOYMENTS_LOCATION', readEnvironmentVariable('AZURE_AI_LOCATION', readEnvironmentVariable('AZURE_LOCATION', 'westus2')))

var env = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')

param envName = env

// ── Sourced from environment-suite.yaml via scripts/sync-env-suite.ps1 ───────
// The sync script writes these into .azure/<env>/.env with `azd env set`.
// Fallbacks are used only when a value is missing (e.g., first-run before sync).
param resourceGroupName              = readEnvironmentVariable('AZURE_RESOURCE_GROUP',              'rg-azuresdkqabot-${env}')
param functionImageRepository        = readEnvironmentVariable('FUNCTION_IMAGE_REPOSITORY',        'azure-sdk-qa-bot-function:${env}')
param ragBasedBackendImageRepository = readEnvironmentVariable('RAG_BASED_BACKEND_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-backend:${env}')
param agentBasedImageRepository      = readEnvironmentVariable('AGENT_BASED_IMAGE_REPOSITORY',      'azure-sdk-qa-bot-agent-server:${env}')
param frontendImageRepository        = readEnvironmentVariable('FRONTEND_IMAGE_REPOSITORY',        'azure-sdk-qa-bot:${env}')

// Resource names for the primary web apps. Kept in sync with env-suite so the
// CD pipelines (which read the same values as FRONTEND_SITE_NAME / etc.) and
// the bicep templates agree on the site names.
param frontendBaseName               = readEnvironmentVariable('FRONTEND_SITE_NAME',               '')
param backendSiteName                = readEnvironmentVariable('BACKEND_SITE_NAME',                '')
param functionAppName                = readEnvironmentVariable('FUNCTION_APP_NAME',                '')

// ── Per-env values (read from .azure/<env>/.env; pipelines override via JSON) ─
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
// in the azd env (or via environments/<env>.parameters.json for prod groups).
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
