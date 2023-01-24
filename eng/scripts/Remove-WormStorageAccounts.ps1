# Helper script for removing storage accounts with WORM that sometimes get leaked from live tests not set up to clean
# up their resource policies

[CmdletBinding(SupportsShouldProcess=$True)]
param(
    [string]$GroupPrefix
)

# Be a little defensive so we don't delete non-live test groups via naming convention
if (!$groupPrefix -or !$GroupPrefix.StartsWith('rg-')) {
    Write-Error "The -GroupPrefix parameter must start with 'rg-'"
    exit 1
}

$groups = Get-AzResourceGroup | ? { $_.ResourceGroupName.StartsWith($GroupPrefix) } | ? { $_.ProvisioningState -ne 'Deleting' }

foreach ($group in $groups) {
    Write-Host "========================================="
    $accounts = Get-AzStorageAccount -ResourceGroupName $group.ResourceGroupName
    if ($accounts) {
        foreach ($account in $accounts) {
            if ($WhatIfPreference) {
                Write-Host "What if: Removing $($account.StorageAccountName) in $($account.ResourceGroupName)"
            } else {
                Write-Host "Removing $($account.StorageAccountName) in $($account.ResourceGroupName)"
            }
            $ctx = New-AzStorageContext -StorageAccountName $account.StorageAccountName
            $ctx | Get-AzStorageContainer | Get-AzStorageBlob | Remove-AzStorageBlob -Force
            # Use AzRm cmdlet as deletion will only work through ARM with the immutability policies defined on the blobs
            $ctx | Get-AzStorageContainer | % { Remove-AzRmStorageContainer -Name $_.Name -StorageAccountName $ctx.StorageAccountName -ResourceGroupName $group.ResourceGroupName -Force }
            Remove-AzStorageAccount -StorageAccountName $account.StorageAccountName -ResourceGroupName $account.ResourceGroupName -Force
        }
    }
    if ($WhatIfPreference) {
        Write-Host "What if: Removing resource group $($group.ResourceGroupName)"
    } else {
        Remove-AzResourceGroup -ResourceGroupName $group.ResourceGroupName -Force -AsJob
    }
}
