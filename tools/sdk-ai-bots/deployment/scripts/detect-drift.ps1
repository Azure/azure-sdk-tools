#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Detects drift between deployed Azure resources and the Bicep source.

.DESCRIPTION
    Runs `az deployment sub what-if` against the requested environment and
    surfaces any Modify or Delete actions. Fails with exit 1 if any drift is
    detected.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$BicepFile = "$PSScriptRoot/../infra/main.bicep",
    [string]$ParametersFile = "$PSScriptRoot/../infra/environments/$Environment.parameters.json",
    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml",
    [string]$Location = ""
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

Write-Host "Running drift detection for '$Environment'..."

if (-not (Get-Command yq -ErrorAction SilentlyContinue)) {
    Write-Error "yq is required to read Teams routing from environment-suite.yaml."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Location)) {
    $Location = (& yq -r ".environments.$Environment.regions[0].name" $SuitePath).Trim()
}
$teamsGroupId = (& yq -r ".environments.$Environment.teamsGroupId" $SuitePath).Trim()
$teamsChannelIds = @(& yq -r ".environments.$Environment.teamsChannelIds[]" $SuitePath)
if ([string]::IsNullOrWhiteSpace($teamsGroupId) -or $teamsChannelIds.Count -eq 0) {
    Write-Error "Teams routing is missing for '$Environment' in $SuitePath."
    exit 1
}

$teamsParametersFile = New-TemporaryFile
try {
    @{
        parameters = @{
            teamsGroupId = @{ value = $teamsGroupId }
            teamsChannelIds = @{ value = $teamsChannelIds }
        }
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $teamsParametersFile -Encoding utf8

    $whatIf = az deployment sub what-if `
        --location $Location `
        --template-file $BicepFile `
        --parameters "@$ParametersFile" "@$teamsParametersFile" `
        --no-pretty-print 2>&1 | Out-String
} finally {
    Remove-Item -Path $teamsParametersFile -Force
}

Write-Host $whatIf

if ($whatIf -match '^\s*(Delete|Modify)' ) {
    Write-Host ""
    Write-Host "DRIFT DETECTED — Bicep source does not match deployed resources." -ForegroundColor Red
    exit 1
}

Write-Host "No drift detected." -ForegroundColor Green
exit 0
