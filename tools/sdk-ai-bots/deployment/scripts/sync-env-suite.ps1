#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Syncs values from environment-suite.yaml into the matching `azd` environment
    (.azure/<env>/.env), so local `azd provision` uses the same source of truth
    as the ADO pipelines.

.DESCRIPTION
    `azd` does not read environment-suite.yaml directly — it reads
    .azure/<env>/.env. This script bridges the two: it reads the per-env block
    from environment-suite.yaml and calls `azd env set` for each value, so the
    same subscription / region / RG / resource names flow into:
        - azd provision           (via main.bicepparam readEnvironmentVariable)
        - per-service hooks       (via process env vars)

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
if (-not ($existingEnvs.Name -contains $Environment)) {
    Write-Host "azd env '$Environment' does not exist yet — creating..." -ForegroundColor Yellow
    & azd env new $Environment --no-prompt
}
& azd env select $Environment | Out-Null

# Mapping: <env-suite yq path>  →  <azd env var name>
$Mapping = @(
    @{ Path = ".environments.$Environment.subscriptionId";       Key = 'AZURE_SUBSCRIPTION_ID' }
    @{ Path = ".environments.$Environment.tenantId";             Key = 'AZURE_TENANT_ID' }
    @{ Path = ".environments.$Environment.resourceGroupPrefix";  Key = 'AZURE_RESOURCE_GROUP' }
    @{ Path = ".environments.$Environment.regions[0].name";      Key = 'AZURE_LOCATION' }
    @{ Path = ".environments.$Environment.containerRegistryName";Key = 'CONTAINER_REGISTRY_NAME' }
    @{ Path = ".environments.$Environment.keyVaultName";         Key = 'KEY_VAULT_NAME' }
    @{ Path = ".environments.$Environment.appConfigName";        Key = 'APP_CONFIG_NAME' }
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

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Sync incomplete:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "✓ azd env '$Environment' is in sync with environment-suite.yaml." -ForegroundColor Green
Write-Host "  Next: azd provision --environment $Environment --no-prompt"
