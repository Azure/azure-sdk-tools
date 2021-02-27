#!/usr/bin/env pwsh -c

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
  [boolean]$Audit=$true
)

# Check if we're in audit mode (no actual deletions)
if ($Audit)
{
    Write-Host -ForegroundColor Green "Running in audit mode. Nothing will be deleted."
}
else
{
    Write-Host -ForegroundColor Red "!!!NOT IN AUDIT MODE. RESOURCE GROUPS WILL BE DELETED!!!"
}

# Connect and select active subscription
Connect-AzAccount
Select-AzSubscription -Subscription $Subscription

#initialize lists
$deleted = @()
$skipped = @()

# Loop through the resource groups and delete any with non-compliant names
foreach ($resourceGroup in Get-AzResourceGroup | Sort ResourceGroupName)
{
    # skip exceptions
    if ($resourceGroup.Tags -and $resourceGroup.Tags["CleanupException"] -eq "true")
    {
        Write-Host " Skipping tagged exception resource group: $($resourceGroup.ResourceGroupName)"
        $skipped += $($resourceGroup.ResourceGroupName)
        continue
    }

    # check compliance (formatting first, then validate alias) and skip if compliant
    if ($resourceGroup.ResourceGroupName -match "^(rg-)?((t-|a-|v-)?[a-z,A-Z]{3,15})([-_]{1}.*)?$" -and (Get-AzAdUser -UserPrincipalName "$($Matches.2)@microsoft.com"))
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
        if (Remove-AzResourceGroup -Name $resourceGroup.ResourceGroupName -Force)
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
