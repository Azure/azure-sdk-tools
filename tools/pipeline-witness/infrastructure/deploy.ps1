param(
    [Parameter(Mandatory)]
    [validateSet('production', 'staging', 'test')]
    [string]$target
)

$resourceGroupName = "pipelinelogs-$target"
$parametersFile = "./bicep/pipelinelogs.$target.json"

$subscription = az account show --query name -o tsv

$parsed = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json)
$location = $parsed.parameters.location.value
$resourceGroupName = $parsed.parameters.resourceGroupName.value

Write-Host @"
Deploying resources to:
    Subscription: $subscription
    Resource Group: $resourceGroupName
    Location: $location

"@

$randomString 

Write-Host "> az deployment sub create --template-file ./bicep/resourceGroup.bicep --parameters $parametersFile --location $location --name "pipelinelogs-$target" --verbose"
az deployment sub create --template-file ./bicep/resourceGroup.bicep --parameters $parametersFile --location $location --name "pipelinelogs-$target" --verbose