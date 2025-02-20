#!/usr/bin/env pwsh

param(
    [switch]$Force
)

function Run()
{
    Write-Host "`n==> $args`n" -ForegroundColor Green
    $command, $arguments = $args
    & $command $arguments
    if ($LASTEXITCODE) {
        Write-Error "Command '$args' failed with code: $LASTEXITCODE" -ErrorAction 'Continue'
    }
}
function RunOrExitOnFailure()
{
    Run @args
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}
$ErrorActionPreference = 'Stop'
$subscriptionId = "a18897a6-7e44-457d-9260-f2854c0aca42"
$env:AZURE_STORAGE_ACCOUNT="azuresdkartifacts"

Remove-Item -Force $PSScriptRoot/*.tgz

RunOrExitOnFailure helm package $PSScriptRoot
RunOrExitOnFailure helm repo index --url https://azuresdkartifacts.z5.web.core.windows.net/stress/ --merge index.yaml $PSScriptRoot

# The index.yaml in git should be synced with the index.yaml already in blob storage
# az storage blob download -c helm -n index.yaml -f index.yaml

$files = (Get-Item *.tgz).Name
$confirmation = "y"
if (!$Force) {
  $confirmation = Read-Host "Do you want to update the helm repository to add ${files}? [y/n]"
}
if ($confirmation -match "[yY]") { 
    RunOrExitOnFailure az storage blob upload --subscription $subscriptionId --container-name '$web' --file index.yaml --name stress/index.yaml --auth-mode login --overwrite
    RunOrExitOnFailure az storage blob upload --subscription $subscriptionId --container-name '$web' --file $files --name stress/$files --auth-mode login

    # index.yaml must be kept up to date, otherwise when helm generates the file, it will not
    # merge it with previous entries, and those packages will become inaccessible as they are no
    # longer index.
    Write-Host "UPDATE CHANGELOG.md"
    Write-Host "COMMIT CHANGES MADE TO 'index.yaml' and 'CHANGELOG.md'"
} else {
    Write-Host "Abort uploading files $files."
}
