# Helper script for removing storage accounts with WORM that sometimes get leaked from live tests not set up to clean
# up their resource policies

[CmdletBinding(SupportsShouldProcess=$True)]
param(
    [string]$GroupPrefix
)

$ErrorActionPreference = 'Stop'

# Be a little defensive so we don't delete non-live test groups via naming convention
if (!$groupPrefix -or !$GroupPrefix.StartsWith('rg-')) {
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
            try {
                # Sometimes the retrieval here fails in preview/dogfood regions but we should still try to delete the storage account below
                # so just handle the exception and attempt the delete below.
                $immutableBlobs = $ctx `
                    | Get-AzStorageContainer `
                    | Where-Object { $_.BlobContainerProperties.HasImmutableStorageWithVersioning } `
                    | Get-AzStorageBlob
                                
                foreach ($blob in $immutableBlobs) {
                    Write-Host "Removing legal hold - blob: $($blob.Name), account: $($account.StorageAccountName), group: $($group.ResourceGroupName)"
                    $blob | Set-AzStorageBlobLegalHold -DisableLegalHold | Out-Null
                }
            } catch {
                Write-Warning "User must have 'Storage Blob Data Owner' RBAC permission on subscription or resource group"
                Write-Error $_
                throw
            }
            # Sometimes we get a 404 blob not found but can still delete containers,
            # and sometimes we must delete the blob if there's a legal hold.
            # Try to remove the blob, but keep running regardless.
            try {
                Write-Host "Removing immutability policies and blobs - account: $($ctx.StorageAccountName), group: $($group.ResourceGroupName)"
                $null = $ctx | Get-AzStorageContainer | Get-AzStorageBlob | Remove-AzStorageBlobImmutabilityPolicy
                $ctx | Get-AzStorageContainer | Get-AzStorageBlob | Remove-AzStorageBlob -Force
            } catch {}
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
