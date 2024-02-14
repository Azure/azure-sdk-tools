<#
.SYNOPSIS
    Deploys the kusto cluster, storage account and associated ingestion queue resources to the specified environment.
#>
param(
    [Parameter(Mandatory)]
    [validateSet('production', 'staging', 'test')]
    [string]$target
)

Push-Location $PSScriptRoot
try {
    $deploymentName = "pipelinelogs-$target-$(Get-Date -Format 'yyyyMMddHHmm')"
    $parametersFile = "./bicep/pipelinelogs.$target.json"

    $subscription = az account show --query name -o tsv

    $parameters = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json).parameters
    $location = $parameters.location.value
    $resourceGroupName = $parameters.resourceGroupName.value

    ./Merge-KustoScripts.ps1 -OutputPath "./artifacts/merged.kql"
    if($?) {
        Write-Host "Merged KQL files"
    } else {
        Write-Error "Failed to merge KQL files"
        exit 1
    }

    Write-Host "Deploying resources to:`n" + `
               "  Subscription: $subscription`n" + `
               "  Resource Group: $resourceGroupName`n" + `
               "  Location: $location`n"

    Write-Host "> az deployment sub create --template-file './bicep/resourceGroup.bicep' --parameters $parametersFile --location $location --name $deploymentName"
    az deployment sub create --template-file './bicep/resourceGroup.bicep' --parameters $parametersFile --location $location --name $deploymentName
} finally {
    Pop-Location
}