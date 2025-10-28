#!/usr/bin/env pwsh

Set-Location $PSScriptRoot/../../../chaos/examples

$exampleDirs = Get-ChildItem .

foreach ($dir in $exampleDirs) {
    Push-Location $dir
    helm repo add --force-update stress-test-charts https://azuresdkartifacts.z5.web.core.windows.net/stress/
    helm dependency update
    if ($LASTEXITCODE) {
        Write-Error "helm dependency update for $dir exited with code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Pop-Location
}
