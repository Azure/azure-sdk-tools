<#
.SYNOPSIS
    Sets up the evaluation fixture environment by downloading spec repo package
    files and running npm install in the Widget fixture directory.

.DESCRIPTION
    This script:
    1. Runs setup-package-files.js to clone package.json and package-lock.json
       from azure-rest-api-specs into fixtures/Microsoft.Widget/Widget/.
    2. Runs npm install in that directory.
    3. Sets FIXTURE_NODE_MODULES for node_modules symlink usage.

    Run this script from the evaluate/ directory.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$EvalDir = Split-Path -Parent $ScriptDir
$WidgetDir = Join-Path $EvalDir 'fixtures' 'Microsoft.Widget' 'Widget'

# Step 1: Download package files from azure-rest-api-specs
Write-Host '==> Downloading package files from azure-rest-api-specs...' -ForegroundColor Cyan
$setupScript = Join-Path $ScriptDir 'setup-package-files.js'
node $setupScript
if ($LASTEXITCODE -ne 0) {
    throw "setup-package-files.js failed with exit code $LASTEXITCODE"
}

# Step 2: Run npm install in the Widget directory
Write-Host "==> Running npm install in $WidgetDir ..." -ForegroundColor Cyan
Push-Location $WidgetDir
try {
    npm install
    if ($LASTEXITCODE -ne 0) {
        throw "npm install failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
}

# Step 3: Set FIXTURE_NODE_MODULES for symlink usage
$nodeModules = Join-Path $WidgetDir 'node_modules'
$env:FIXTURE_NODE_MODULES = $nodeModules
Write-Host "==> FIXTURE_NODE_MODULES set to: $nodeModules" -ForegroundColor Green
Write-Host '==> Setup complete.' -ForegroundColor Green
