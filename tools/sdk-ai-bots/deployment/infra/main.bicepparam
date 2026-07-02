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
param location = readEnvironmentVariable('AZURE_LOCATION', 'westus2')

param envName = readEnvironmentVariable('AZURE_ENV_NAME', 'dev')


// ── Sourced from environment-suite.yaml via scripts/sync-env-suite.ps1 ───────
// The sync script writes these into .azure/<env>/.env with `azd env set`.
// Fallbacks are used only when a value is missing (e.g., first-run before sync).
param resourceGroupName              = readEnvironmentVariable('AZURE_RESOURCE_GROUP',              'rg-azuresdkqabot-${envName}')
param functionImageRepository        = readEnvironmentVariable('FUNCTION_IMAGE_REPOSITORY',        'azure-sdk-qa-bot-function:${envName}')
param ragBasedBackendImageRepository = readEnvironmentVariable('RAG_BASED_BACKEND_IMAGE_REPOSITORY', 'azure-sdk-qa-bot-backend:${envName}')
param agentBasedImageRepository      = readEnvironmentVariable('AGENT_BASED_IMAGE_REPOSITORY',      'azure-sdk-qa-bot-agent-server:${envName}')

// ── Per-env values (read from .azure/<env>/.env; pipelines override via JSON) ─
// SERVER_AUDIENCE is auto-populated by the preprovision hook
// (hooks/preprovision.ts → ensureServerAudience), which creates or looks up
// an Entra ID app registration named `azuresdkqabot-server-<envName>` via
// `az ad app` and persists its clientId with `azd env set`. Only set this
// manually to pin a specific external app registration.
//
// TEAMS_GROUP_ID / TEAMS_CHANNEL_IDS are set per-env with:
//   azd env set TEAMS_GROUP_ID <guid>
//   azd env set TEAMS_CHANNEL_IDS '19:foo@thread.skype,19:bar@thread.skype'
param serverAudience  = readEnvironmentVariable('SERVER_AUDIENCE', '')
param teamsGroupId    = readEnvironmentVariable('TEAMS_GROUP_ID', '3e17dcb0-4257-4a30-b843-77f47f1d4121')
param teamsChannelIds = split(readEnvironmentVariable('TEAMS_CHANNEL_IDS', '19:de3fce22c2994be18cac50502c55f717@thread.skype'), ',')
