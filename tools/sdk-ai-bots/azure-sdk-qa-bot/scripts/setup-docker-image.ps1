#!/usr/bin/env pwsh

# TODO: parse the .env file and set environment variables
param(
    [switch]$Push = $false,
    [string]$Tag = "env-0.0.0",
    [string]$AcrName = "azsdkqabotenv",
    [string]$ImageName = "azure-sdk-qa-bot"
)

# prepare node server
npm install
npm run build

# prepare azsdk-cli
$azsdk_cli_source = "../../azsdk-cli"
$azsdk_cli_target = "./azsdk-cli"
if (Test-Path $azsdk_cli_source) {
    Write-Host "Found azsdk-cli at $azsdk_cli_source, copying to current directory..."
    
    # Remove existing azsdk-cli in current directory if it exists
    if (Test-Path $azsdk_cli_target) {
        Write-Host "Removing existing azsdk-cli in current directory..."
        Remove-Item $azsdk_cli_target -Recurse -Force
    }
    
    # Copy azsdk-cli to current directory
    Copy-Item $azsdk_cli_source $azsdk_cli_target -Recurse -Force
    Write-Host "Successfully copied azsdk-cli to current directory."
} else {
    Write-Warning "azsdk-cli not found at $azsdk_cli_source"
    Write-Host "Expected path: $(Resolve-Path $azsdk_cli_source -ErrorAction SilentlyContinue)"
}

# setup docker
docker build . -t ${ImageName}:local

if ($Push) {
    Write-Host "Push flag is enabled, logging into ACR and pushing image..."
    az acr login --name ${AcrName}
    docker tag "${ImageName}:local" "${AcrName}.azurecr.io/${ImageName}:${Tag}"
    docker push  "${AcrName}.azurecr.io/${ImageName}:${Tag}"
    Write-Host "Image successfully pushed to ${AcrName}.azurecr.io/${ImageName}:${Tag}"
} else {
    Write-Host "Push flag is disabled, skipping ACR login and push operations."
    Write-Host "Local image built: $ImageName:local"
    Write-Host "To push later, use: ./scripts/build-tag-push-docker-image.ps1 -Push"
}