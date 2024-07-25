<#
.SYNOPSIS
    Builds and deploys the dotnet app.
#>
param(
  [Parameter(Mandatory)]
  [validateSet('staging', 'test')]
  [string]$Target
)

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
. "$repoRoot/eng/common/scripts/Helpers/CommandInvocation-Helpers.ps1"

Push-Location $PSScriptRoot
try {
    $subscriptionName = $Target -eq 'test' ? 'Azure SDK Developer Playground' : 'Azure SDK Engineering System'
    $parametersFile = "../infrastructure/bicep/parameters.$Target.json"
      
    $parameters = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json).parameters
    $resourceGroupName = $parameters.appResourceGroupName.value
    $resourceName = $parameters.webAppName.value
  
    Write-Host "Deploying web app to:`n" + `
    "  Subscription: $subscriptionName`n" + `
    "  Resource Group: $resourceGroupName`n" + `
    "  Resource: $resourceName`n"

    $artifactsPath = "$repoRoot/artifacts"
    $publishPath = "$artifactsPath/app"

    Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue

    Invoke-LoggedCommand "dotnet publish --configuration Release --output '$publishPath'"

    Compress-Archive -Path "$publishPath/*" -DestinationPath "$artifactsPath/pipeline-witness.zip" -Force
    if($?) {
        Write-Host "pipeline-witness.zip created"
    } else {
        Write-Error "Failed to create pipeline-witness.zip"
        exit 1
    }

    Invoke-LoggedCommand "az webapp deploy --src-path '$artifactsPath/pipeline-witness.zip' --clean true --restart true --type zip --subscription '$subscriptionName' --resource-group '$resourceGroupName' --name '$resourceName'"
}
finally {
    Pop-Location
}
