[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)]
  [string] $UserPrincipalName,
  [Parameter(Mandatory=$True)]
  [string] $UserPrincipalId,
  [string] $SubscriptionId = "a18897a6-7e44-457d-9260-f2854c0aca42",
  [string] $ResourceGroupName = "apiviewstagingrg",
  [string] $KeyVaultName = "apiviewstagingkv",
  [string] $StorageAccountName = "apiviewstagingstorage",
  [string] $AppConfigurationName = "apiviewstagnkvconfig",
  [string] $CosmosDbAccountName = "apiviewstaging"
)

function Test-RoleAssignmentExists {
    param(
        [string]$ObjectId,
        [string]$RoleDefinitionName,
        [string]$Scope
    )
    
    try {
        $existingAssignment = Get-AzRoleAssignment -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope -ErrorAction SilentlyContinue
        return $null -ne $existingAssignment
    }
    catch {
        return $false
    }
}

# Helper function to assign role with existence check
function Set-RoleAssignmentIfNotExists {
    param(
        [string]$ObjectId,
        [string]$RoleDefinitionName,
        [string]$Scope,
        [string]$ResourceName,
        [string]$ResourceType
    )
    
    Write-Host "Checking if '$RoleDefinitionName' role exists for $UserPrincipalName on $ResourceType $ResourceName" -ForegroundColor Yellow
    if (Test-RoleAssignmentExists -ObjectId $ObjectId -RoleDefinitionName $RoleDefinitionName -Scope $Scope) {
        Write-Host "ℹ️  Role assignment already exists - skipping" -ForegroundColor Blue
        return $null
    }
    
    try {
        Write-Host "Assigning '$RoleDefinitionName' role to $UserPrincipalName for $ResourceType $ResourceName" -ForegroundColor Yellow
        $roleAssignment = New-AzRoleAssignment `
            -ObjectId $ObjectId `
            -RoleDefinitionName $RoleDefinitionName `
            -Scope $Scope `
            -ErrorAction Stop
        
        Write-Host "✓ Successfully assigned $RoleDefinitionName role to $UserPrincipalName" -ForegroundColor Green
        Write-Host "  Role Assignment ID: $($roleAssignment.RoleAssignmentId)" -ForegroundColor Green
        return $roleAssignment
    }
    catch {
        if ($_.Exception.Message -like "*Conflict*" -or $_.Exception.Message -like "*already exists*") {
            Write-Host "ℹ️  Role assignment already exists (detected during creation) - continuing" -ForegroundColor Blue
            return $null
        }
        else {
            throw
        }
    }
}
try {
    if ($SubscriptionId) {
        Set-AzContext -SubscriptionId $SubscriptionId -ErrorAction Stop
    }
     
    $keyVault = Get-AzKeyVault -VaultName $KeyVaultName -ResourceGroupName $ResourceGroupName -ErrorAction Stop
    $keyVaultResourceId = $keyVault.ResourceId
    Set-RoleAssignmentIfNotExists -ObjectId $UserPrincipalId -RoleDefinitionName "Key Vault Secrets User" -Scope $keyVaultResourceId -ResourceName $KeyVaultName -ResourceType "Key Vault"

    Write-Host "Assigning Get, List and Set access policy to $UserPrincipalName for Key Vault $KeyVaultName" -ForegroundColor Yellow
    try {
      Set-AzKeyVaultAccessPolicy `
      -VaultName $KeyVaultName `
      -ResourceGroupName $ResourceGroupName `
      -ObjectId $UserPrincipalId `
      -PermissionsToSecrets @("Get", "List", "Set")
      Write-Host "✓ Successfully assigned Get, List and Set access policy to $UserPrincipalName for Key Vault $KeyVaultName" -ForegroundColor Green
    }
    catch {
      Write-Host "⚠️  Key Vault access policy may already be set or RBAC is enabled - continuing" -ForegroundColor Yellow
    }

    $storageAccount = Get-AzStorageAccount -ResourceGroupName $ResourceGroupName -Name $StorageAccountName -ErrorAction Stop
    $storageAccountResourceId = $storageAccount.Id
    Set-RoleAssignmentIfNotExists -ObjectId $UserPrincipalId -RoleDefinitionName "Storage Blob Data Contributor" -Scope $storageAccountResourceId -ResourceName $StorageAccountName -ResourceType "Storage Account"

    $appConfig = Get-AzAppConfigurationStore -ResourceGroupName $ResourceGroupName -Name $AppConfigurationName -ErrorAction Stop
    $appConfigResourceId = $appConfig.Id
    Set-RoleAssignmentIfNotExists -ObjectId $UserPrincipalId -RoleDefinitionName "App Configuration Data Reader" -Scope $appConfigResourceId -ResourceName $AppConfigurationName -ResourceType "App Configuration"


    $cosmosDbAccount = Get-AzCosmosDBAccount -ResourceGroupName $ResourceGroupName -Name $CosmosDbAccountName -ErrorAction Stop
    $cosmosDbResourceId = $cosmosDbAccount.Id
    Set-RoleAssignmentIfNotExists -ObjectId $UserPrincipalId -RoleDefinitionName "DocumentDB Account Contributor" -Scope $cosmosDbResourceId -ResourceName $CosmosDbAccountName -ResourceType "Cosmos DB"

    Write-Host "Checking if Cosmos DB SQL role exists for $UserPrincipalName" -ForegroundColor Yellow
    try {
        $existingSqlRoles = Get-AzCosmosDBSqlRoleAssignment -AccountName $CosmosDbAccountName -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue
        $existingAssignment = $existingSqlRoles | Where-Object { $_.PrincipalId -eq $UserPrincipalId }

        if ($existingAssignment) {
            Write-Host "ℹ️  Cosmos DB SQL role assignment already exists - skipping" -ForegroundColor Blue
        }
        else {
            Write-Host "Assigning Cosmos DB SQL role to $UserPrincipalName" -ForegroundColor Yellow
            New-AzCosmosDBSqlRoleAssignment `
                -AccountName $CosmosDbAccountName `
                -ResourceGroupName $ResourceGroupName `
                -RoleDefinitionId "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosDbAccountName/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002" `
                -Scope "/" `
                -PrincipalId $UserPrincipalId `
                -ErrorAction Stop
            Write-Host "✓ Successfully assigned Cosmos DB SQL role" -ForegroundColor Green
        }
    }
    catch {
        if ($_.Exception.Message -like "*already exists*" -or $_.Exception.Message -like "*Conflict*") {
            Write-Host "ℹ️  Cosmos DB SQL role assignment already exists - continuing" -ForegroundColor Blue
        }
        else {
            Write-Warning "Failed to assign Cosmos DB SQL role: $($_.Exception.Message)"
        }
    }
} catch {
    Write-Error "❌ Failed to assign permissions: $($_.Exception.Message)"
    Write-Host "Stack Trace: $($_.Exception.StackTrace)" -ForegroundColor Red
    exit 1
}