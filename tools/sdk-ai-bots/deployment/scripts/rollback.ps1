#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local-fallback rollback wrapper. Reads LastKnownGoodTag from App Config
    and re-points the requested component's App Service to that image.

.NOTES
    The pipeline-driven path (pipelines/templates/rollback.yml) is preferred.
    This script exists for emergency manual rollback only — DRI must call.

    Resource names are resolved from infra/environments/environment-suite.yaml
    via yq, so this stays in sync with the CD pipelines and Bicep templates.
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('frontend', 'backend', 'function-app', 'agent-server')]
    [string]$Component,

    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$Slot = 'default',

    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml"
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if ($Environment -eq 'prod' -and -not $env:TF_BUILD -and -not $env:GITHUB_ACTIONS) {
    Write-Error "Manual prod rollback from a developer machine is blocked. Use the prod CD pipeline."
    exit 1
}

if (-not (Test-Path $SuitePath)) {
    Write-Error "environment-suite.yaml not found at $SuitePath"
    exit 1
}
if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    Write-Error "yq is required. Install with: winget install MikeFarah.yq"
    exit 1
}

function Read-EnvSuite([string]$Path) {
    $value = (& yq -r $Path $SuitePath).Trim()
    if (-not $value -or $value -eq 'null') {
        Write-Error "environment-suite.yaml is missing '$Path'"
        exit 1
    }
    return $value
}

$rg        = Read-EnvSuite ".environments.$Environment.resourceGroupPrefix"
$appConfig = Read-EnvSuite ".environments.$Environment.appConfigName"
$acr       = Read-EnvSuite ".environments.$Environment.containerRegistryName"

# Site name and image name per component. Frontend / backend / function-app
# have their names in env-suite; agent-server shares the backend site (it runs
# in the `agent` deployment slot of the backend web app).
$imageNames = @{
    'frontend'     = 'azure-sdk-qa-bot'
    'backend'      = 'azure-sdk-qa-bot-backend'
    'function-app' = 'azure-sdk-qa-bot-function'
    'agent-server' = 'azure-sdk-qa-bot-agent-server'
}
switch ($Component) {
    'frontend'     { $appName = Read-EnvSuite ".environments.$Environment.frontendSiteName" }
    'backend'      { $appName = Read-EnvSuite ".environments.$Environment.backendSiteName" }
    'function-app' { $appName = Read-EnvSuite ".environments.$Environment.functionAppName" }
    'agent-server' { $appName = Read-EnvSuite ".environments.$Environment.backendSiteName" }
}
$imageName = $imageNames[$Component]
$key       = "Deployment:${Component}:LastKnownGoodTag"

$tag = az appconfig kv show --name $appConfig --key $key --query value -o tsv 2>$null
if (-not $tag) { Write-Error "No LastKnownGoodTag in App Config: $key"; exit 1 }

Write-Host "Rolling back $Component on $appName ($Slot) → ${imageName}:$tag"
if (-not $PSCmdlet.ShouldProcess("$appName/$Slot", "rollback to $tag")) { return }

$slotArg = if ($Slot -ne 'default') { @('--slot', $Slot) } else { @() }
$loginServer = az acr show --name $acr --query loginServer -o tsv

az webapp config container set `
    --name $appName --resource-group $rg @slotArg `
    --container-image-name "$loginServer/${imageName}:$tag" `
    --container-registry-url "https://$loginServer"

az webapp restart --name $appName --resource-group $rg @slotArg

Write-Host "Rollback complete." -ForegroundColor Green
