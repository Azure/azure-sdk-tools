param(
    [Parameter(Mandatory)]
    [validateSet('production', 'staging', 'test')]
    [string]$target
)

$root = $PSScriptRoot
$deploymentName = "pipelinelogs-$target-$(Get-Date -Format 'yyyyMMddHHmm')"
$parametersFile = "./bicep/pipelinelogs.$target.json"

$subscription = az account show --query name -o tsv

$parsed = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json)
$location = $parsed.parameters.location.value
$resourceGroupName = $parsed.parameters.resourceGroupName.value

. $root/kusto/Merge-KustoScripts.ps1 -OutputPath "$root/artifacts/merged.kql"
if($?) {
    Write-Host "Merged KQL files"
} else {
    Write-Error "Failed to merge KQL files"
    exit 1
}

Write-Host @"
Deploying resources to:
    Subscription: $subscription
    Resource Group: $resourceGroupName
    Location: $location

"@

Write-Host "> az deployment sub create --template-file ./bicep/resourceGroup.bicep --parameters $parametersFile --location $location --name $deploymentName --verbose"
az deployment sub create --template-file ./bicep/resourceGroup.bicep --parameters $parametersFile --location $location --name $deploymentName --verbose
