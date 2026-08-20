#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Detects drift between deployed Azure resources and the Bicep source.

.DESCRIPTION
    Runs the configured preprovision hook followed by `azd provision --preview`
    against the requested environment. This uses main.bicepparam, the same
    parameter adapter as apply. Fails with exit 1 if any Modify or Delete
    operation is detected.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'preview', 'prod')]
    [string]$Environment,

    [string]$ProjectDirectory = "$PSScriptRoot/../.."
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

Write-Host "Running drift detection for '$Environment'..."

foreach ($tool in @('azd', 'node')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        Write-Error "$tool is required for azd drift detection."
        exit 1
    }
}

if (-not (Test-Path "$ProjectDirectory/azure.yaml")) {
    Write-Error "azure.yaml not found under $ProjectDirectory."
    exit 1
}

$previewOutputFile = New-TemporaryFile
$previousLocation = Get-Location
$previousPreviewMode = $env:AZD_PROVISION_PREVIEW
try {
    Set-Location $ProjectDirectory

    $existingEnvironments = & azd env list --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to list azd environments."
    }
    $existingNames = @($existingEnvironments | ForEach-Object { $_.Name })
    if ($existingNames -notcontains $Environment) {
        throw "azd environment '$Environment' does not exist. Run deployment/scripts/sync-env-suite.ps1 -Environment $Environment first."
    }

    & azd env select $Environment --no-prompt | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to select azd environment '$Environment'."
    }

    # azd preview omits hooks, so resolve the same dynamic values as apply.
    $env:AZD_PROVISION_PREVIEW = 'true'
    & azd hooks run preprovision --environment $Environment --no-prompt
    if ($LASTEXITCODE -ne 0) {
        throw "preprovision preparation failed for '$Environment'."
    }

    & azd provision --preview --environment $Environment --no-prompt --output json > $previewOutputFile
    if ($LASTEXITCODE -ne 0) {
        throw "azd preview failed for '$Environment'."
    }
} catch {
    Remove-Item -Path $previewOutputFile -Force -ErrorAction SilentlyContinue
    throw
} finally {
    $env:AZD_PROVISION_PREVIEW = $previousPreviewMode
    Set-Location $previousLocation
}

$previewOutput = Get-Content -Raw -Path $previewOutputFile
Write-Host $previewOutput

$riskyOperations = @(& node "$PSScriptRoot/list-risky-preview-operations.mjs" $previewOutputFile)
$inspectorExitCode = $LASTEXITCODE
Remove-Item -Path $previewOutputFile -Force
if ($inspectorExitCode -ne 0) {
    Write-Error "Unable to inspect azd preview output."
    exit 1
}

if ($riskyOperations.Count -gt 0) {
    Write-Host ""
    Write-Host "DRIFT DETECTED - azd preview reports Modify or Delete operations:" -ForegroundColor Red
    $riskyOperations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "No drift detected." -ForegroundColor Green
exit 0
