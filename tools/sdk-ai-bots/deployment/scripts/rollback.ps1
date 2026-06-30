#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local-fallback rollback wrapper. Reads LastKnownGoodTag from App Config
    and re-points the requested component's App Service to that image.

.NOTES
    The pipeline-driven path (pipelines/templates/rollback.yml) is preferred.
    This script exists for emergency manual rollback only — DRI must call.
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('frontend', 'backend', 'function-app', 'agent-server')]
    [string]$Component,

    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$Slot = 'default'
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if ($Environment -eq 'prod' -and -not $env:TF_BUILD -and -not $env:GITHUB_ACTIONS) {
    Write-Error "Manual prod rollback from a developer machine is blocked. Use the prod CD pipeline."
    exit 1
}

$rg          = "rg-azuresdkqabot-$Environment"
$appConfig   = "azsdkqabot-config-$Environment"
$acr         = "azsdkqabotacr$Environment"
$componentToApp = @{
    'frontend'     = @{ App='azsdkqabot';            Image='azure-sdk-qa-bot' }
    'backend'      = @{ App='azuresdkqabot-backend'; Image='azure-sdk-qa-bot-backend' }
    'function-app' = @{ App='azuresdkqabot-function';Image='azure-sdk-qa-bot-function' }
    'agent-server' = @{ App='azuresdkqabot-backend'; Image='azure-sdk-qa-bot-agent-server' }
}
$appName   = $componentToApp[$Component].App
$imageName = $componentToApp[$Component].Image
$key       = "Deployment:${Component}:LastKnownGoodTag"

$tag = az appconfig kv show --name $appConfig --key $key --query value -o tsv 2>$null
if (-not $tag) { Write-Error "No LastKnownGoodTag in App Config: $key"; exit 1 }

Write-Host "Rolling back $Component on $appName ($Slot) → $imageName:$tag"
if (-not $PSCmdlet.ShouldProcess("$appName/$Slot", "rollback to $tag")) { return }

$slotArg = if ($Slot -ne 'default') { @('--slot', $Slot) } else { @() }
$loginServer = az acr show --name $acr --query loginServer -o tsv

az webapp config container set `
    --name $appName --resource-group $rg @slotArg `
    --container-image-name "$loginServer/${imageName}:$tag" `
    --container-registry-url "https://$loginServer"

az webapp restart --name $appName --resource-group $rg @slotArg

Write-Host "Rollback complete." -ForegroundColor Green
