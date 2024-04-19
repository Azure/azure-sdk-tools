<#
.SYNOPSIS
    Deploys the kusto cluster, storage account and associated ingestion queue resources to the specified environment.
#>
param(
  [Parameter(Mandatory)]
  [validateSet('production', 'staging', 'test')]
  [string]$target,

  [switch]$replaceRoleAssignments
)

function Invoke([string]$command) {
  Write-Host "> $command"
  Invoke-Expression $command
}

function RemoveStorageRoleAssignments($subscriptionId, $resourceGroup, $resourceName) {
  $scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.Storage/storageAccounts/$resourceName"
  
  Write-Host "Removing role assignments from $resourceGroup/$resourceName"
  $existingAssignments = az role assignment list --scope $scope --output json | ConvertFrom-Json

  if ($existingAssignments.Count -eq 0) {
    Write-Host "  No role assignments found"
    return
  }

  foreach ($assignment in $existingAssignments) {
    Write-Host "  Removing role assignment for '$($assignment.principalName)' in role '$($assignment.roleDefinitionName)'"
    Invoke "az role assignment delete --assignee '$($assignment.principalId)' --role '$($assignment.roleDefinitionId)' --scope '$scope' --yes"
  }
}

function RemoveCosmosRoleAssignments($subscriptionId, $resourceGroup, $resourceName) {
  Write-Host "Removing cosmos role assignments from $resourceGroup/$resourceName"
  $existingAssignments = az cosmosdb sql role assignment list --account-name $resourceName --resource-group $resourceGroup --output json | ConvertFrom-Json

  if ($existingAssignments.Count -eq 0) {
    Write-Host "  No role assignments found"
    return
  }

  foreach ($assignment in $existingAssignments) {
    Write-Host "  Removing cosmos role assignment $($assignment.name)"
    Invoke "az cosmosdb sql role assignment delete --account-name '$cosmosAccountName' --resource-group '$appResourceGroupName' --role-assignment-id '$($assignment.id)' --yes"
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

  Invoke "az account set --subscription '$subscriptionName'"
  $subscriptionId = az account show --query id -o tsv

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

  if ($replaceRoleAssignments) {
    RemoveStorageRoleAssignments $subscriptionId $logsResourceGroupName $logsStorageAccountName
    RemoveStorageRoleAssignments $subscriptionId $appResourceGroupName $appStorageAccountName
    RemoveCosmosRoleAssignments $subscriptionId $appResourceGroupName $cosmosAccountName
  }

  Invoke "az deployment sub create --template-file './bicep/resourceGroups.bicep' --parameters '$parametersFile' --location '$location' --name '$deploymentName' --output none"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to deploy resource groups"
    exit 1
  }

  if ($target -ne 'production') {
    $azAdGroupId = az ad group show --group "Azure SDK Engineering System Team" --query id --output tsv

    Write-Host "Granting the Azure SDK Engineering System Team read/write access to cosmos account"
    Invoke "az cosmosdb sql role assignment create --resource-group '$appResourceGroupName' --account-name '$cosmosAccountName' --scope '/' --role-definition-id '00000000-0000-0000-0000-000000000002' --principal-id '$azAdGroupId' --output none"
      
    if ($LASTEXITCODE -ne 0) {
      Write-Output $output
      Write-Error "Failed to grant access to cosmos account"
      exit 1
    }

    Write-Host "Granting the Azure SDK Engineering System Team access to storage accounts"
    $scope = "/subscriptions/$subscriptionId/resourceGroups/$logsResourceGroupName/providers/Microsoft.Storage/storageAccounts/$logsStorageAccountName"
    $output = Invoke "az role assignment create --assignee '$azAdGroupId' --role 'Storage Blob Data Contributor' --scope '$scope' --output none"
      
    if ($LASTEXITCODE -ne 0) {
      Write-Output $output
      Write-Error "Failed to grant access to logs storage account"
      exit 1
    }
      
    $scope = "/subscriptions/$subscriptionId/resourceGroups/$appResourceGroupName/providers/Microsoft.Storage/storageAccounts/$appStorageAccountName"
    $output = Invoke "az role assignment create --assignee '$azAdGroupId' --role 'Storage Queue Data Contributor' --scope '$scope' --output none"
      
    if ($LASTEXITCODE -ne 0) {
      Write-Output $output
      Write-Error "Failed to grant access to app storage account"
      exit 1
    }
  }
}
finally {
  Pop-Location
}