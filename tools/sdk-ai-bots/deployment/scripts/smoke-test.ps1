#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke-test an sdk-ai-bots component endpoint.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('frontend', 'backend', 'function-app', 'agent-server')]
    [string]$Component,

    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$AppName,
    [string]$Slot = 'default',
    [string]$HealthPath = '',
    [string]$EasyAuthAudience = '',
    [int]$MaxAttempts = 12,
    [int]$WaitSeconds = 10
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

# Defaults per component (mirrors environment-suite.yaml components map).
$defaults = @{
    'frontend'     = @{ AppName='azsdkqabot';            Path='/health' }
    'backend'      = @{ AppName='azuresdkqabot-backend'; Path='/ping' }
    'function-app' = @{ AppName='azuresdkqabot-function';Path='/api/health' }
    'agent-server' = @{ AppName='azuresdkqabot-backend'; Path='/ping' }
}
if (-not $AppName)    { $AppName    = $defaults[$Component].AppName }
if (-not $HealthPath) { $HealthPath = $defaults[$Component].Path }

$slotArg = if ($Slot -ne 'default') { "--slot $Slot" } else { "" }
$rg = "rg-azuresdkqabot-$Environment"

$host = az webapp show --name $AppName --resource-group $rg @($slotArg -split ' ' | Where-Object { $_ }) --query defaultHostName -o tsv
if (-not $host) { Write-Error "Could not resolve hostname for $AppName"; exit 1 }
$url = "https://$host$HealthPath"

$headers = @{}
if ($EasyAuthAudience) {
    $token = az account get-access-token --resource $EasyAuthAudience --query accessToken -o tsv
    $headers['Authorization'] = "Bearer $token"
}

Write-Host "Probing $url"
for ($i = 1; $i -le $MaxAttempts; $i++) {
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $headers -SkipHttpErrorCheck -UseBasicParsing
        if ($resp.StatusCode -eq 200) {
            Write-Host "  OK (HTTP 200) on attempt $i" -ForegroundColor Green
            exit 0
        }
        Write-Host "  attempt $i : HTTP $($resp.StatusCode)"
    }
    catch {
        Write-Host "  attempt $i : $($_.Exception.Message)"
    }
    Start-Sleep -Seconds $WaitSeconds
}

Write-Host "Smoke test FAILED for $url" -ForegroundColor Red
exit 1
