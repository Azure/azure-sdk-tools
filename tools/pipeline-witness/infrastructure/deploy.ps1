<#
.SYNOPSIS
    Deploys the kusto cluster, storage account and associated ingestion queue resources to the specified environment.
#>
param(
  [Parameter(Mandatory)]
  [validateSet('production', 'staging', 'test')]
  [string]$target,

  [switch]$removeRoleAssignments
)

$repoRoot = Resolve-Path "$PSScriptRoot/../../.."
. "$repoRoot/eng/common/scripts/Helpers/CommandInvocation-Helpers.ps1"

function RemoveStorageRoleAssignments($subscriptionId, $resourceGroup, $resourceName) {
  $scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$resourceName"
  
  Write-Host "Removing role assignments from $resourceGroup/$resourceName"
  $existingAssignments = Invoke-LoggedCommand "az role assignment list --scope $scope --output json" | ConvertFrom-Json

  if ($existingAssignments.Count -eq 0) {
    Write-Host "  No role assignments found"
    return
  }

  foreach ($assignment in $existingAssignments) {
    Write-Host "  Removing role assignment for '$($assignment.principalName)' in role '$($assignment.roleDefinitionName)'"
    Invoke-LoggedCommand "az role assignment delete --assignee '$($assignment.principalId)' --role '$($assignment.roleDefinitionId)' --scope '$scope' --yes"
  }
}

function RemoveCosmosRoleAssignments($subscriptionId, $resourceGroup, $resourceName) {
  Write-Host "Removing cosmos role assignments from $resourceGroup/$resourceName"
  $existingAssignments = Invoke-LoggedCommand "az cosmosdb sql role assignment list --account-name $resourceName --resource-group $resourceGroup --output json" | ConvertFrom-Json

  if ($existingAssignments.Count -eq 0) {
    Write-Host "  No role assignments found"
    return
  }

  foreach ($assignment in $existingAssignments) {
    Write-Host "  Removing cosmos role assignment $($assignment.name)"
    Invoke-LoggedCommand "az cosmosdb sql role assignment delete --account-name '$cosmosAccountName' --resource-group '$appResourceGroupName' --role-assignment-id '$($assignment.id)' --yes"
  }
}

Push-Location $PSScriptRoot
try {
  $deploymentName = "$target-$(Get-Date -Format 'yyyyMMddHHmm')"
  $subscriptionName = $target -eq 'test' ? 'Azure SDK Developer Playground' : 'Azure SDK Engineering System'
  $parametersFile = "./bicep/parameters.$target.json"
    
  $parameters = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json).parameters
  $location = $parameters.location.value
  $appResourceGroupName = $parameters.appResourceGroupName.value
  $appStorageAccountName = $parameters.appStorageAccountName.value
  $cosmosAccountName = $parameters.cosmosAccountName.value
  $logsResourceGroupName = $parameters.logsResourceGroupName.value
  $logsStorageAccountName = $parameters.logsStorageAccountName.value

  Invoke-LoggedCommand "az account set --subscription '$subscriptionName'"
  $subscriptionId = Invoke-LoggedCommand "az account show --query id -o tsv"

  ./Merge-KustoScripts.ps1 -OutputPath "./artifacts/merged.kql"
  if ($?) {
    Write-Host "Merged KQL files"
  }
  else {
    Write-Error "Failed to merge KQL files"
    exit 1
  }

  Write-Host "Deploying resources to:`n" + `
    "  Subscription: $subscriptionName`n" + `
    "  Logs Resource Group: $logsResourceGroupName`n" + `
    "  App Resource Group: $appResourceGroupName`n" + `
    "  Location: $location`n"

  if ($removeRoleAssignments) {
    RemoveStorageRoleAssignments $subscriptionId $logsResourceGroupName $logsStorageAccountName
    RemoveStorageRoleAssignments $subscriptionId $appResourceGroupName $appStorageAccountName
    RemoveCosmosRoleAssignments $subscriptionId $appResourceGroupName $cosmosAccountName
  }

  Invoke-LoggedCommand "az deployment sub create --template-file './bicep/main.bicep' --parameters '$parametersFile' --location '$location' --name '$deploymentName' --output none"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to deploy resource groups"
    exit 1
  }
}
finally {
  Pop-Location
}
