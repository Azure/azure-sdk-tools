# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# Requires PowerShell module Az and RemoteSigned excecution policy
#
# Commands to make this happen:
#   Install-Module -Name Az -AllowClobber
#   Set-ExecutionPolicy RemoteSigned

function Print-Help 
{
    Write-Host @"
====================== sub-cleanup.ps1 ======================
Cleans up non-compliant resource groups in a specified Azure
subscription.  Complient resource groups are named with a
valid Microsoft alias or using the format: alias-<identifier>

Usage:
    sub-cleanup.ps1 <subscription Id>

Notes:
    You can add the tag CleanupException=true to the resource
    group to have it skipped as an exception. This can be 
    done with CLI or in the portal.
=============================================================
"@
}

if ($args.Count -ne 1)
{
    Print-Help
    exit
}

# Connect and select active subscription
$sub = $args[0]
Connect-AzAccount
Select-AzSubscription -Subscription $sub

# Loop through the resource groups and delete any with non-compliant names
foreach ($resourceGroup in Get-AzResourceGroup | Sort ResourceGroupName)
{
    # skip exceptions
    if ($resourceGroup.Tags -and $resourceGroup.Tags["CleanupException"] -eq "true")
    {
        Write-Host " Skipping tagged exception resource group: $($resourceGroup.ResourceGroupName)"
        continue
    }

    # check compliance (formatting first, then validate alias) and skip if compliant
    if ($resourceGroup.ResourceGroupName -match "^((t-|a-|v-)?[a-z,A-Z]{3,15})(-{1}.*)?$" -and (Get-AzAdUser -UserPrincipalName "$($Matches.1)@microsoft.com"))
    {
        Write-Host " Skipping compliant resource group: $($resourceGroup.ResourceGroupName)"
        continue
    }

    Write-Host -ForegroundColor Red -NoNewline "Deleting non-compliant resource group: $($resourceGroup.ResourceGroupName)"
    
    # delete
    if (Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName -Force)
    {
        Write-Host " Succeeded."
    }
}