#!/usr/bin/env pwsh -c

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

#Requires -Modules @{ModuleName='Az.Accounts'; ModuleVersion='1.6.4'}
#Requires -Modules @{ModuleName='Az.Resources'; ModuleVersion='1.8.0'}

<#
.SYNOPSIS
Subscription cleanup script

.DESCRIPTION
Cleans up non-compliant resource groups in a specified Azure
subscription.  Complient resource groups are named with a valid
Microsoft alias or using the format: alias-<identifier>

.NOTES
You can add the tag CleanupException=true to the resource group
to have it skipped as an exception. This can be done with CLI
or in the portal.

Requires PowerShell module Az and RemoteSigned excecution policy.
Commands to make this happen:
  PS> Install-Module -Name Az -AllowClobber
  PS> Set-ExecutionPolicy RemoteSigned

.EXAMPLE
PS> sub-cleanup.ps1 -Subscription {guid} -Audit false

.PARAMETER Subscription
Subscription ID (guid) for the subscription to be cleaned.

.PARAMETER Audit
When specified, deletion operations are just logged, but not performed.
#>

param(
  [Parameter(Mandatory = $true)]
  [string]$Subscription,
  
  [Parameter(Mandatory = $false)]
  [boolean]$Audit=$true,

  [Parameter(Mandatory = $false)]
  [boolean]$DeleteAsync=$false,

  [Parameter(Mandatory = $false)]
  [boolean]$Login=$true
)

function IsValidAlias
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Alias
    )

    $domains = @("microsoft.com", "ntdev.microsoft.com")

    foreach ($domain in $domains)
    {
        if (Get-AzAdUser -UserPrincipalName "$Alias@$domain")
        {
            return $true;
        }
    }
    return $false;
}

# Check if we're in audit mode (no actual deletions)
if ($Audit)
{
    Write-Host -ForegroundColor Green "Running in audit mode. Nothing will be deleted."
}
else
{
    Write-Warning "!!!NOT IN AUDIT MODE. RESOURCE GROUPS WILL BE DELETED!!!"
}

# Connect and select active subscription
if ($Login)
{
    Connect-AzAccount
    Select-AzSubscription -Subscription $Subscription
}

#initialize lists
$deleted = @()
$skipped = @()

# Loop through the resource groups and delete any with non-compliant names/tags
foreach ($resourceGroup in Get-AzResourceGroup | Sort-Object ResourceGroupName)
{
    # skip exceptions
    if ($resourceGroup.Tags -and $resourceGroup.Tags["CleanupException"] -eq "true")
    {
        Write-Host " Skipping tagged exception resource group: $($resourceGroup.ResourceGroupName)"
        $skipped += $($resourceGroup.ResourceGroupName)
        continue
    }

    # Exclude groups with a valid owners tag list
    if ($resourceGroup.Tags -and $resourceGroup.Tags["owners"])
    {
        $hasValidOwner = $false
        $owners = $resourceGroup.Tags["owners"]
        foreach ($owner in $owners -Split "[;, ]") {
            if (IsValidAlias -Alias $owner) {
                $hasValidOwner = $true
                break
            }
        }
        if ($hasValidOwner)
        {
            Write-Host " Skipping tagged exception resource group '$($resourceGroup.ResourceGroupName)' with owners '$owners'"
            $skipped += $($resourceGroup.ResourceGroupName)
            continue
        }
    }

    # Exclude groups already marked for cleanup within a week
    if ($resourceGroup.Tags -and $resourceGroup.Tags["DeleteAfter"])
    {
        $now = [DateTime]::UtcNow
        $parsedTime = [DateTime]::MaxValue
        $canParse = [DateTime]::TryParse($resourceGroup.Tags["DeleteAfter"], [ref]$parsedTime)
        if ($canParse -and ($parsedTime -lt $now.AddDays(7))) {
            Write-Host " Skipping compliant resource group '$($resourceGroup.ResourceGroupName)' marked DeleteAfter '$parsedTime'"
            $skipped += $($resourceGroup.ResourceGroupName)
            continue
        }
    }

    # check compliance (formatting first, then validate alias) and skip if compliant
    if ($resourceGroup.ResourceGroupName -match "^(rg-)?((t-|a-|v-)?[a-z,A-Z]{3,15})([-_]{1}.*)?$" -and (IsValidAlias -Alias $matches[2]))
    {
        Write-Host " Skipping compliant resource group: $($resourceGroup.ResourceGroupName)"
        $skipped += $($resourceGroup.ResourceGroupName)
        continue
    }

    Write-Host -ForegroundColor Red -NoNewline "Deleting non-compliant resource group: $($resourceGroup.ResourceGroupName)"
    
    # delete
    $deleted += $($resourceGroup.ResourceGroupName)
    if ($Audit)
    {
        Write-Host " Audit:Skipped."
    }
    else
    {
        if ($DeleteAsync)
        {
            Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName -Force -AsJob
        }
        elseif (Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName -Force)
        {
            Write-Host " Succeeded."
        }
    }
}

Write-Host -ForegroundColor Cyan "##################################################"
Write-Host -ForegroundColor Cyan "Summary:"
Write-Host -ForegroundColor Cyan "  Deleted: $($deleted.Count)"
Write-Host -ForegroundColor Cyan "  Skipped: $($skipped.Count)"
Write-Host -ForegroundColor Cyan "##################################################"

Write-Host -ForegroundColor Cyan "Resource Groups Deleted:"
foreach ($rg in $deleted)
{
    Write-Host "  $rg"
}

Write-Host -ForegroundColor Cyan "##################################################"
Write-Host -ForegroundColor Cyan "Compliant Resource Groups Skipped:"
foreach ($rg in $skipped)
{
    Write-Host "  $rg"
}
