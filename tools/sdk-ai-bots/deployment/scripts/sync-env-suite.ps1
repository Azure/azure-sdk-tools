#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Syncs values from environment-suite.yaml into the matching `azd` environment
    (.azure/<env>/.env), so local `azd provision` uses the same source of truth
    as the ADO pipelines.

.DESCRIPTION
    `azd` does not read environment-suite.yaml directly — it reads
    .azure/<env>/.env. This script synchronizes the suite-owned values and
    bicepOverrides into that environment so the same subscription / region /
    RG / image-repository / resource-name values flow into:
        - azd provision                 (via .azure/<env>/.env + layer parameter adapters)
        - azd provision <layer> --preview (the same layer adapter path)
        - per-service hooks       (via process env vars)

    Teams IDs and any fixed environment-specific Bicep values are copied from
    the suite. When SERVER_AUDIENCE is not pinned, the preprovision hook creates
    or discovers the environment's Entra app registration.

    Run this once after `azd env new <env>`, and again whenever the suite is
    updated. Pipelines do not need it — they read the suite directly via
    pipelines/templates/load-environment-suite.yml.

.PARAMETER Environment
    The environment name (dev | preview | prod) — must exist in
    environment-suite.yaml.

.PARAMETER SuitePath
    Optional override for the env-suite location.

.EXAMPLE
    pwsh ./scripts/sync-env-suite.ps1 -Environment dev
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml"
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SuitePath)) {
    Write-Error "environment-suite.yaml not found at $SuitePath"
    exit 1
}
if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    Write-Error "yq is required. Install with: winget install MikeFarah.yq"
    exit 1
}
if (-not (Get-Command azd -ErrorAction SilentlyContinue)) {
    Write-Error "azd is required. Install from https://aka.ms/install-azd"
    exit 1
}

# Confirm the env exists in the suite.
$declared = & yq -r '.environments | keys | .[]' $SuitePath
if ($declared -notcontains $Environment) {
    Write-Error "Environment '$Environment' is not declared in $SuitePath. Found: $($declared -join ', ')"
    exit 1
}

# Confirm the azd env exists locally; if not, create it.
$existingEnvs = & azd env list --output json | ConvertFrom-Json
$existingNames = @($existingEnvs | ForEach-Object { $_.Name })
if (-not ($existingNames -contains $Environment)) {
    Write-Host "azd env '$Environment' does not exist yet — creating..." -ForegroundColor Yellow
    & azd env new $Environment --no-prompt
}
& azd env select $Environment | Out-Null

# Mapping: <env-suite yq path>  →  <azd env var name>
$Mapping = @(
    @{ Path = ".environments.$Environment.subscriptionId";       Key = 'AZURE_SUBSCRIPTION_ID' }
    @{ Path = ".environments.$Environment.tenantId";             Key = 'AZURE_TENANT_ID' }
    @{ Path = ".environments.$Environment.serviceManagementReference"; Key = 'SERVICE_MANAGEMENT_REFERENCE' }
    @{ Path = ".environments.$Environment.resourceGroupPrefix";  Key = 'AZURE_RESOURCE_GROUP' }
    @{ Path = ".environments.$Environment.regions[0].name";      Key = 'AZURE_LOCATION' }
    @{ Path = ".environments.$Environment.aiLocation";           Key = 'AZURE_AI_LOCATION' }
    @{ Path = ".environments.$Environment.aiLocation";           Key = 'AZURE_AI_DEPLOYMENTS_LOCATION' }
    @{ Path = ".environments.$Environment.cosmosDbLocation";     Key = 'COSMOS_DB_LOCATION' }
    @{ Path = ".environments.$Environment.chatbotEvolutionAgentEnabled"; Key = 'CHATBOT_EVOLUTION_AGENT_ENABLED' }
    @{ Path = ".environments.$Environment.frontendSiteName";     Key = 'FRONTEND_SITE_NAME' }
    @{ Path = ".environments.$Environment.agentServerSiteName";  Key = 'AGENT_SERVER_SITE_NAME' }
    @{ Path = ".environments.$Environment.agentServerSiteName";  Key = 'AGENT_SERVER_SITE_NAME_OVERRIDE' }
    @{ Path = ".environments.$Environment.functionAppName";      Key = 'FUNCTION_APP_NAME' }
    @{ Path = ".environments.$Environment.functionAppName";      Key = 'FUNCTION_APP_NAME_OVERRIDE' }
    @{ Path = ".environments.$Environment.containerRegistryName"; Key = 'ACR_NAME' }
    @{ Path = ".environments.$Environment.containerRegistryName"; Key = 'CONTAINER_REGISTRY_NAME' }
    @{ Path = ".environments.$Environment.containerRegistryName"; Key = 'CONTAINER_REGISTRY_NAME_OVERRIDE' }
    @{ Path = ".environments.$Environment.keyVaultName";         Key = 'KEY_VAULT_NAME' }
    @{ Path = ".environments.$Environment.keyVaultName";         Key = 'KEY_VAULT_NAME_OVERRIDE' }
    @{ Path = ".environments.$Environment.appConfigName";        Key = 'APP_CONFIG_NAME' }
    @{ Path = ".environments.$Environment.appConfigName";        Key = 'APP_CONFIG_NAME_OVERRIDE' }
    # Image repositories are <componentImageName>:<env>. bicepOverrides can
    # replace these derived values for an existing environment.
    @{ Path = ".components.`"function-app`".imageName + `":$Environment`"";  Key = 'FUNCTION_IMAGE_REPOSITORY' }
    @{ Path = ".components.`"agent-server`".imageName + `":$Environment`"";  Key = 'AGENT_SERVER_IMAGE_REPOSITORY' }
    @{ Path = ".components.frontend.imageName + `":$Environment`"";          Key = 'FRONTEND_IMAGE_REPOSITORY' }
)

Write-Host "Syncing environment-suite.yaml → azd env '$Environment'..." -ForegroundColor Cyan

$failed = @()
foreach ($entry in $Mapping) {
    $value = (& yq -r $entry.Path $SuitePath).Trim()
    if ([string]::IsNullOrEmpty($value) -or $value -eq 'null') {
        Write-Warning "  $($entry.Key): empty in suite (path=$($entry.Path))"
        continue
    }
    if ($value -match '^REPLACE_WITH_') {
        $failed += "$($entry.Key) is still a placeholder ('$value'). Edit environment-suite.yaml."
        continue
    }
    & azd env set $entry.Key $value | Out-Null
    Write-Host "  $($entry.Key) = $value"
}

# Teams routing is suite-owned and flows into the local azd environment.
$teamsGroupId = (& yq -r ".environments.$Environment.teamsGroupId" $SuitePath).Trim()
$teamsChannelIds = @(& yq -r ".environments.$Environment.teamsChannelIds[]" $SuitePath | ForEach-Object { ([string]$_).Trim() })

if ([string]::IsNullOrWhiteSpace($teamsGroupId) -or $teamsGroupId -match '^REPLACE_WITH_') {
    $failed += "teamsGroupId is missing or still a placeholder in environment-suite.yaml."
} else {
    & azd env set TEAMS_GROUP_ID $teamsGroupId | Out-Null
    Write-Host "  TEAMS_GROUP_ID = $teamsGroupId"
}

if ($teamsChannelIds.Count -eq 0 -or @($teamsChannelIds | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -match '^REPLACE_WITH_' }).Count -gt 0) {
    $failed += "teamsChannelIds is empty or contains a placeholder in environment-suite.yaml."
} else {
    $teamsChannelIdsValue = $teamsChannelIds -join ','
    & azd env set TEAMS_CHANNEL_IDS $teamsChannelIdsValue | Out-Null
    Write-Host "  TEAMS_CHANNEL_IDS = $teamsChannelIdsValue"
}

# Apply environment-specific Bicep overrides after common mappings so an
# override can intentionally replace a derived value (for example prod's
# FRONTEND_IMAGE_REPOSITORY).
$overridesJson = (& yq -o=json ".environments.$Environment.bicepOverrides // {}" $SuitePath | Out-String).Trim()
$overrides = $overridesJson | ConvertFrom-Json
$overrideInputKeys = @(
    'MANAGED_IDENTITY_NAME', 'ACTION_GROUP_NAME', 'KEY_VAULT_NAME',
    'APP_CONFIG_NAME', 'SEARCH_SERVICE_NAME', 'CONTAINER_REGISTRY_NAME',
    'STORAGE_ACCOUNT_NAME', 'COSMOS_DB_ACCOUNT_NAME', 'AI_RESOURCE_NAME',
    'AI_PROJECT_NAME', 'AGENT_SERVER_SITE_NAME', 'FUNCTION_APP_NAME',
    'INTEGRATION_ACCOUNT_NAME', 'TEAMS_CONNECTION_NAME',
    'AZURE_BLOB_CONNECTION_NAME', 'DOCUMENT_DB_CONNECTION_NAME',
    'LOGIC_APP_WORKFLOW_NAME', 'LOGIC_APP_ALERT_NAME'
)
foreach ($property in $overrides.PSObject.Properties) {
    $key = $property.Name
    $value = [string]$property.Value
    if ([string]::IsNullOrWhiteSpace($value)) {
        $failed += "$key is empty in bicepOverrides."
        continue
    }
    if ($value -match '^REPLACE_WITH_') {
        $failed += "$key is still a placeholder ('$value') in bicepOverrides."
        continue
    }
    & azd env set $key $value | Out-Null
    Write-Host "  $key = $value"
    if ($key -in $overrideInputKeys) {
        $overrideKey = "${key}_OVERRIDE"
        & azd env set $overrideKey $value | Out-Null
        Write-Host "  $overrideKey = $value"
    }
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Sync incomplete:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "✓ azd env '$Environment' is in sync with environment-suite.yaml." -ForegroundColor Green
Write-Host "  Next: azd provision --environment $Environment --no-prompt"
