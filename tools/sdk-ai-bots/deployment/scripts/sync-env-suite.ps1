#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Syncs values from environment-suite.yaml into the matching `azd` environment
    (.azure/<env>/.env), so local `azd provision` uses the same source of truth
    as the ADO pipelines.

.DESCRIPTION
    `azd` does not read environment-suite.yaml directly — it reads
    .azure/<env>/.env. Nor does `az deployment sub what-if` in the pipeline
    read the suite directly — it reads infra/environments/<env>.parameters.json.
    This script keeps both in sync with environment-suite.yaml so the same
    subscription / region / RG / image-repository values flow into:
        - azd provision           (via .azure/<env>/.env + main.bicepparam readEnvironmentVariable)
        - per-service hooks       (via process env vars)
        - az deployment sub what-if in pipelines (via <env>.parameters.json)

    Fields not owned by the suite (teamsGroupId, teamsChannelIds, serverAudience)
    are preserved untouched in <env>.parameters.json.

    Run this once after `azd env new <env>`, and again whenever the suite is
    updated. Pipelines do not need it — they read the suite directly via
    pipelines/templates/load-environment-suite.yml.

.PARAMETER Environment
    The environment name (dev | preview | prod) — must exist in
    environment-suite.yaml.

.PARAMETER SuitePath
    Optional override for the env-suite location.

.PARAMETER ParametersFile
    Optional override for the <env>.parameters.json path.

.EXAMPLE
    pwsh ./scripts/sync-env-suite.ps1 -Environment dev
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml",

    [string]$ParametersFile = "$PSScriptRoot/../infra/environments/$Environment.parameters.json"
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
    # Image repositories are <componentImageName>:<env>; consumed by main.bicepparam
    # so `azd provision` uses the same values as pipelines' <env>.parameters.json.
    @{ Path = ".components.`"function-app`".imageName + `":$Environment`"";  Key = 'FUNCTION_IMAGE_REPOSITORY' }
    @{ Path = ".components.backend.imageName + `":$Environment`"";           Key = 'RAG_BASED_BACKEND_IMAGE_REPOSITORY' }
    @{ Path = ".components.`"agent-server`".imageName + `":$Environment`"";  Key = 'AGENT_BASED_IMAGE_REPOSITORY' }
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

# ── Sync into <env>.parameters.json ────────────────────────────────────────────
# Fields owned by the suite are overwritten; unmanaged fields are preserved.
if (-not (Test-Path $ParametersFile)) {
    Write-Error "Parameters file not found at $ParametersFile"
    exit 1
}

Write-Host ""
Write-Host "Syncing environment-suite.yaml → $([System.IO.Path]::GetFileName($ParametersFile))..." -ForegroundColor Cyan

# Values pulled from the suite. Image repositories are <componentImageName>:<env>.
$location                       = (& yq -r ".environments.$Environment.regions[0].name" $SuitePath).Trim()
$resourceGroupName              = (& yq -r ".environments.$Environment.resourceGroupPrefix" $SuitePath).Trim()
$functionImageName              = (& yq -r '.components."function-app".imageName' $SuitePath).Trim()
$backendImageName               = (& yq -r '.components.backend.imageName' $SuitePath).Trim()
$agentImageName                 = (& yq -r '.components."agent-server".imageName' $SuitePath).Trim()

foreach ($pair in @(
    @{ Name = 'location';               Value = $location },
    @{ Name = 'resourceGroupName';      Value = $resourceGroupName },
    @{ Name = 'function-app imageName'; Value = $functionImageName },
    @{ Name = 'backend imageName';      Value = $backendImageName },
    @{ Name = 'agent-server imageName'; Value = $agentImageName }
)) {
    if ([string]::IsNullOrEmpty($pair.Value) -or $pair.Value -eq 'null') {
        Write-Error "Missing '$($pair.Name)' in $SuitePath for environment '$Environment'."
        exit 1
    }
}

$paramsJson = Get-Content -Raw -Path $ParametersFile | ConvertFrom-Json

function Set-ParamValue {
    param($Parameters, [string]$Name, $Value)
    if (-not $Parameters.PSObject.Properties.Name -contains $Name) {
        Add-Member -InputObject $Parameters -MemberType NoteProperty -Name $Name -Value ([pscustomobject]@{ value = $Value })
    } else {
        $Parameters.$Name.value = $Value
    }
}

Set-ParamValue $paramsJson.parameters 'location'                       $location
Set-ParamValue $paramsJson.parameters 'resourceGroupName'              $resourceGroupName
Set-ParamValue $paramsJson.parameters 'functionImageRepository'        "${functionImageName}:${Environment}"
Set-ParamValue $paramsJson.parameters 'ragBasedBackendImageRepository' "${backendImageName}:${Environment}"
Set-ParamValue $paramsJson.parameters 'agentBasedImageRepository'      "${agentImageName}:${Environment}"

# Preserve trailing newline; ConvertTo-Json reformats but the file is now derived.
$json = $paramsJson | ConvertTo-Json -Depth 20
Set-Content -Path $ParametersFile -Value $json -Encoding utf8 -NoNewline
Add-Content -Path $ParametersFile -Value "" -Encoding utf8

Write-Host "  location                       = $location"
Write-Host "  resourceGroupName              = $resourceGroupName"
Write-Host "  functionImageRepository        = ${functionImageName}:${Environment}"
Write-Host "  ragBasedBackendImageRepository = ${backendImageName}:${Environment}"
Write-Host "  agentBasedImageRepository      = ${agentImageName}:${Environment}"

# Warn on unmanaged placeholders that pipelines still need.
foreach ($key in @('serverAudience', 'teamsGroupId')) {
    if ($paramsJson.parameters.PSObject.Properties.Name -contains $key) {
        $v = [string]$paramsJson.parameters.$key.value
        if ($v -match '^REPLACE_WITH_') {
            Write-Warning "  $key is still a placeholder ('$v') in $([System.IO.Path]::GetFileName($ParametersFile)). Not owned by env-suite — edit the parameters file directly."
        }
    }
}

Write-Host ""
Write-Host "✓ azd env '$Environment' and $([System.IO.Path]::GetFileName($ParametersFile)) are in sync with environment-suite.yaml." -ForegroundColor Green
Write-Host "  Next: azd provision --environment $Environment --no-prompt"
