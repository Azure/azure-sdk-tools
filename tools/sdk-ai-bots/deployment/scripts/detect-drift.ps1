#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Detects drift between deployed Azure resources and the Bicep source.

.DESCRIPTION
    Runs each layer's preprovision hook followed by `azd provision <layer>
    --preview` against the requested environment. Fails with exit 1 if any
    layer reports a Modify or Delete operation.
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

$layers = @('resource-group', 'shared-resources', 'agent', 'frontend', 'agent-server', 'function-app', 'logic-app')
$previewOutputFiles = @()
$riskyOperations = @()
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

    $env:AZD_PROVISION_PREVIEW = 'true'
    foreach ($layer in $layers) {
        Write-Host "Previewing provisioning layer '$layer'..."
        & azd env refresh $Environment --layer $layer --no-prompt
        if ($LASTEXITCODE -ne 0) {
            throw "No deployed state is available for layer '$layer'. Layered preview requires existing state so upstream outputs can be hydrated."
        }

        & azd hooks run preprovision --layer $layer --environment $Environment --no-prompt
        if ($LASTEXITCODE -ne 0) {
            throw "preprovision preparation failed for layer '$layer'."
        }

        $previewOutputFile = New-TemporaryFile
        $previewOutputFiles += $previewOutputFile
        & azd provision $layer --preview --environment $Environment --no-prompt --output json > $previewOutputFile
        if ($LASTEXITCODE -ne 0) {
            throw "azd preview failed for layer '$layer'."
        }

        $previewOutput = Get-Content -Raw -Path $previewOutputFile
        Write-Host $previewOutput
        $layerOperations = @(& node "$PSScriptRoot/list-risky-preview-operations.mjs" $previewOutputFile)
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to inspect azd preview output for layer '$layer'."
        }
        $riskyOperations += $layerOperations | ForEach-Object { "${layer}: $_" }
    }
} catch {
    throw
} finally {
    $previewOutputFiles | ForEach-Object {
        Remove-Item -Path $_ -Force -ErrorAction SilentlyContinue
    }
    $env:AZD_PROVISION_PREVIEW = $previousPreviewMode
    Set-Location $previousLocation
}

if ($riskyOperations.Count -gt 0) {
    Write-Host ""
    Write-Host "DRIFT DETECTED - azd preview reports Modify or Delete operations:" -ForegroundColor Red
    $riskyOperations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "No drift detected." -ForegroundColor Green
exit 0
