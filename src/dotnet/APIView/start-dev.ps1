<#
.SYNOPSIS
    Starts APIView Web and SPA client for local development.

.DESCRIPTION
    This script:
    1. Starts the APIViewWeb .NET project
    2. Waits for it to be ready
    3. Runs npm install in the ClientSPA folder
    4. Starts the Angular SPA with SSL
    5. Opens the browser to the SPA URL
#>

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiViewWebDir = Join-Path $scriptDir "APIViewWeb"
$clientSpaDir = Join-Path $scriptDir "ClientSPA"
$spaUrl = "https://localhost:4200"
$apiViewUrl = "http://localhost:5000"

Write-Host "Starting APIView development environment..." -ForegroundColor Cyan

# Start the APIViewWeb project in a new PowerShell window
Write-Host "Starting APIViewWeb..." -ForegroundColor Yellow
$dotnetProcess = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$apiViewWebDir'; dotnet run" -PassThru

# Wait for the APIViewWeb to be ready
Write-Host "Waiting for APIViewWeb to start on $apiViewUrl..." -ForegroundColor Yellow
$maxAttempts = 60
$attempt = 0
$ready = $false

while (-not $ready -and $attempt -lt $maxAttempts) {
    Start-Sleep -Seconds 2
    $attempt++
    try {
        $response = Invoke-WebRequest -Uri $apiViewUrl -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 302) {
            $ready = $true
            Write-Host "APIViewWeb is ready!" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Attempt $attempt/$maxAttempts - Waiting for APIViewWeb..." -ForegroundColor Gray
    }
}

if (-not $ready) {
    Write-Host "Warning: Could not confirm APIViewWeb is ready after $maxAttempts attempts. Continuing anyway..." -ForegroundColor Yellow
}

# Install npm dependencies in ClientSPA
Write-Host "Running npm install in ClientSPA..." -ForegroundColor Yellow
Push-Location $clientSpaDir
try {
    npm install
    if ($LASTEXITCODE -ne 0) {
        throw "npm install failed"
    }
    Write-Host "npm install completed successfully!" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Start the Angular SPA with SSL in a new PowerShell window
Write-Host "Starting Angular SPA with SSL..." -ForegroundColor Yellow
$spaProcess = Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$clientSpaDir'; npx ng serve --ssl" -PassThru

# Wait for Angular to compile and be ready
Write-Host "Waiting for Angular SPA to start on $spaUrl..." -ForegroundColor Yellow
$attempt = 0
$ready = $false

while (-not $ready -and $attempt -lt $maxAttempts) {
    Start-Sleep -Seconds 3
    $attempt++
    try {
        # Ignore SSL certificate errors for localhost self-signed cert
        $null = [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        $response = Invoke-WebRequest -Uri $spaUrl -UseBasicParsing -TimeoutSec 5 -SkipCertificateCheck -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $ready = $true
            Write-Host "Angular SPA is ready!" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Attempt $attempt/$maxAttempts - Waiting for Angular SPA to compile..." -ForegroundColor Gray
    }
}

if (-not $ready) {
    Write-Host "Warning: Could not confirm Angular SPA is ready after $maxAttempts attempts. Opening browser anyway..." -ForegroundColor Yellow
}

# Open browser to the SPA URL
Write-Host "Opening browser to $spaUrl..." -ForegroundColor Cyan
Start-Process $spaUrl

Write-Host "" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Development environment started!" -ForegroundColor Green
Write-Host "APIViewWeb: $apiViewUrl" -ForegroundColor Green
Write-Host "Angular SPA: $spaUrl" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop this script (servers will continue running in their windows)" -ForegroundColor Gray
