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
    [string]$Location = "westus2"
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

Write-Host "Running drift detection for '$Environment'..."

$whatIf = az deployment sub what-if `
    --location $Location `
    --template-file $BicepFile `
    --parameters "@$ParametersFile" `
    --no-pretty-print 2>&1 | Out-String

Write-Host $whatIf

if ($whatIf -match '^\s*(Delete|Modify)' ) {
    Write-Host ""
    Write-Host "DRIFT DETECTED — Bicep source does not match deployed resources." -ForegroundColor Red
    exit 1
}

Write-Host "No drift detected." -ForegroundColor Green
exit 0
