#!/usr/bin/env pwsh

# TODO: parse the .env file and set environment variables
param(
    [switch]$Push = $false,
    [string]$Tag, # image tag, e.g. dev-0.0.1
    [string]$AcrName, # azure container registry name
    [string]$ImageName
)

# prepare node server
Write-Host "Running npm install..."
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Error "npm install failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Running npm run build..."
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "npm run build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Copy azsdk-cli source using the dedicated script
Write-Host "Copying azsdk-cli source..."
& "./scripts/copy-azsdk-source.ps1"

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