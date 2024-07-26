<#
.SYNOPSIS
    Assign appropriate storage permissions to the Azure SDK Engineering System Team for local debugging.
#>
param(
  [Parameter(Mandatory)]
  [validateSet('staging', 'test')]
  [string]$target
)

function Invoke([string]$command) {
  Write-Host "> $command"
  Invoke-Expression $command
}

Push-Location $PSScriptRoot
try {
  $subscriptionName = $target -eq 'test' ? 'Azure SDK Developer Playground' : 'Azure SDK Engineering System'
  Write-Host "Setting subscription to '$subscriptionName'"
  Invoke "az account set --subscription '$subscriptionName' --output none"
  $subscriptionId = az account show --query id -o tsv

  $parametersFile = "./bicep/parameters.$target.json"
  Write-Host "Reading parameters from $parametersFile"
  $parameters = (Get-Content -Path $parametersFile -Raw | ConvertFrom-Json).parameters
  $appResourceGroupName = $parameters.appResourceGroupName.value
  $keyVaultName = $parameters.keyVaultName.value
  $appStorageAccountName = $parameters.appStorageAccountName.value
  $cosmosAccountName = $parameters.cosmosAccountName.value
  $logsResourceGroupName = $parameters.logsResourceGroupName.value
  $logsStorageAccountName = $parameters.logsStorageAccountName.value

  Write-Host "Adding Azure SDK Engineering System Team RBAC access to storage resources:`n" + `
    "  Vault: $appResourceGroupName/$keyVaultName`n" + `
    "  Blob: $logsResourceGroupName/$logsStorageAccountName`n" + `
    "  Queue: $appResourceGroupName/$appStorageAccountName`n" + `
    "  Cosmos: $appResourceGroupName/$cosmosAccountName`n"

  Write-Host "Getting group id for Azure SDK Engineering System Team"
  $azAdGroupId = Invoke "az ad group show --group 'Azure SDK Engineering System Team' --query id --output tsv"

  Write-Host "Granting 'read/write' access to $appResourceGroupName/$cosmosAccountName"
  Invoke "az cosmosdb sql role assignment create --resource-group '$appResourceGroupName' --account-name '$cosmosAccountName' --scope '/' --role-definition-id '00000000-0000-0000-0000-000000000002' --principal-id '$azAdGroupId' --output none"
    
  if ($LASTEXITCODE -ne 0) {
    Write-Output $output
    Write-Error "Failed to grant access"
    exit 1
  }

  Write-Host "Granting 'Key Vault Administrator' access to $appStorageAccountName/$keyVaultName"
  $scope = "/subscriptions/$subscriptionId/resourceGroups/$appStorageAccountName/providers/Microsoft.KeyVault/vaults/$keyVaultName"
  $output = Invoke "az role assignment create --assignee '$azAdGroupId' --role 'Key Vault Administrator' --scope '$scope' --output none"
    
  if ($LASTEXITCODE -ne 0) {
    Write-Output $output
    Write-Error "Failed to grant access"
    exit 1
  }
    
  Write-Host "Granting 'Storage Blob Data Contributor' access to $logsResourceGroupName/$logsStorageAccountName"
  $scope = "/subscriptions/$subscriptionId/resourceGroups/$logsResourceGroupName/providers/Microsoft.Storage/storageAccounts/$logsStorageAccountName"
  $output = Invoke "az role assignment create --assignee '$azAdGroupId' --role 'Storage Blob Data Contributor' --scope '$scope' --output none"
    
  if ($LASTEXITCODE -ne 0) {
    Write-Output $output
    Write-Error "Failed to grant access"
    exit 1
  }
    
  Write-Host "Granting 'Storage Queue Data Contributor' access to $appResourceGroupName/$appStorageAccountName"
  $scope = "/subscriptions/$subscriptionId/resourceGroups/$appResourceGroupName/providers/Microsoft.Storage/storageAccounts/$appStorageAccountName"
  $output = Invoke "az role assignment create --assignee '$azAdGroupId' --role 'Storage Queue Data Contributor' --scope '$scope' --output none"
    
  if ($LASTEXITCODE -ne 0) {
    Write-Output $output
    Write-Error "Failed to grant access"
    exit 1
  }
}
finally {
  Pop-Location
}