# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# Requires PowerShell modules Az and RemoteSigned excecution policy
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
    sub-cleanup.ps1 <subscription Id> <exception file>

Notes:
    The optional exception file is a text file listing
    resource groups to be skipped in the cleanup.
=============================================================
"@
}

if ($args.Count -lt 1)
{
    Print-Help
}

# Load exceptions file if one was provided
$exceptions = @()
if ($args.Count -eq 2)
{
    $exceptionFile = 
    Write-Host "Using exception file $($args[1])"
    $exceptions = Get-Content $args[1]
}

# Connect and select active subscription
$sub = $args[0]
Connect-AzAccount
Select-AzSubscription -Subscription $sub

# Loop through the resource groups and delete any with non-compliant names
$resourceGroups = Get-AzResourceGroup | Sort ResourceGroupName
foreach ($resourceGroup in $resourceGroups)
{
    # skip exceptions
    if ($exceptions -contains $resourceGroup.ResourceGroupName)
    {
        Write-Host " Skipping exception resource group: $($resourceGroup.ResourceGroupName)"
        continue
    }

    # check compliance (formatting then valid alias) and skip if compliant
    if ($resourceGroup.ResourceGroupName -match "^((t-|a-|v-)?[a-z,A-Z]{3,15})(-{1}.*)?$" -and (Get-AzAdUser -UserPrincipalName "$($Matches.1)@microsoft.com"))
    {
        Write-Host " Skipping compliant resource group: $($resourceGroup.ResourceGroupName)"
        continue
    }

    Write-Host "Deleting non-compliant resource group: $($resourceGroup.ResourceGroupName)"
    
    # delete
    Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName
}


