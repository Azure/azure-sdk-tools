#!/usr/bin/env pwsh
param(
    [switch]$Build = $false,
    [switch]$Bash = $false
)

# Function to parse .env file and return environment variables
function Parse-EnvFile {
    param([string]$FilePath)
    
    $envVars = @()
    
    if (Test-Path $FilePath) {
        Write-Host "Loading environment variables from: $FilePath" -ForegroundColor Green
        
        Get-Content $FilePath | ForEach-Object {
            $line = $_.Trim()
            
            # Skip empty lines and comments
            if ($line -and !$line.StartsWith('#')) {
                # Parse KEY=VALUE or KEY='VALUE' or KEY="VALUE"
                if ($line -match '^([^=]+)=(.*)$') {
                    $key = $matches[1].Trim()
                    $value = $matches[2].Trim()
                    
                    # Remove quotes if present
                    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or 
                        ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                        $value = $value.Substring(1, $value.Length - 2)
                    }
                    
                    # Mask secret values in logs
                    $displayValue = if ($key.StartsWith('SECRET_')) { '***' } else { $value }
                    Write-Host "Parsing env:  $key=$displayValue" -ForegroundColor Cyan
                    
                    $envVars += "-e", "$key=$value"

                }
            }
        }
    } else {
        Write-Host "Environment file not found: $FilePath" -ForegroundColor Yellow
    }
    
    return $envVars
}

# Parse environment variables from .env.testtool.user
$envFile = "env/.env.testtool"
$envFileForUser = "env/.env.testtool.user"
$envArgs = Parse-EnvFile -FilePath $envFile
$envArgsForUser = Parse-EnvFile -FilePath $envFileForUser

Write-Host "`nBuilding Docker image..." -ForegroundColor Green

if ($Build) {
    npm run build
    docker build . -t azure-sdk-qa-bot:local
}

if (1 -eq 1) {
    Write-Host "`nRunning Docker container with environment variables..." -ForegroundColor Green

    # Construct the docker run command with environment variables
    $dockerArgs = @('run', '-it', '--rm', '-v', "${HOME}/.azure:/root/.azure", '-p', '3978:3978', '--network=host') + $envArgs + $envArgsForUser + @('azure-sdk-qa-bot:local')
    
    if ($Bash) {
        $dockerArgs += 'bash'
    }
    
    Write-Host "docker $($dockerArgs -join ' ')" -ForegroundColor Yellow
    & docker @dockerArgs
} else {
    Write-Host "❌ Docker build failed!" -ForegroundColor Red
    exit 1
}