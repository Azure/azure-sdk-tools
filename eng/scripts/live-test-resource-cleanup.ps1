#!/usr/bin/env pwsh

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# This script implements the resource management guidelines documented at https://github.com/Azure/azure-sdk-tools/blob/main/doc/engsys_resource_management.md

#Requires -Version 7.0
#Requires -PSEdition Core
#Requires -Modules @{ModuleName='Az.Resources'; ModuleVersion='5.3.1'}
#Requires -Modules @{ModuleName='Az.Accounts'; ModuleVersion='2.7.2'}

[CmdletBinding(DefaultParameterSetName = 'Interactive', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param (
    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $ProvisionerApplicationId,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ProvisionerApplicationSecret,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [Parameter(ParameterSetName = 'Interactive')]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $TenantId,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [Parameter(ParameterSetName = 'Interactive')]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $SubscriptionId,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Environment = "AzureCloud",

    [Parameter(ParameterSetName = 'Interactive')]
    [switch] $Login,

    [Parameter()]
    [int] $DeleteAfterHours = 24,

    [Parameter()]
    [switch] $Force,

    [Parameter(ValueFromRemainingArguments = $true)]
    $IgnoreUnusedArguments
)

Set-StrictMode -Version 3

&"$PSScriptRoot/../common/scripts/Import-AzModules.ps1"

# Import resource management helpers and override its Log function.
. "$PSScriptRoot/../common/scripts/Helpers/Resource-Helpers.ps1"

$OwnerAliasCache = @{}

function Log($Message) {
  Write-Host $Message
}

function IsValidAlias
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Alias
    )

    if ($OwnerAliasCache.ContainsKey($Alias)) {
      return $OwnerAliasCache[$Alias]
    }

    $domains = @("microsoft.com", "ntdev.microsoft.com")

    foreach ($domain in $domains) {
        if (Get-AzAdUser -UserPrincipalName "$Alias@$domain") {
            $OwnerAliasCache[$Alias] = $true
            return $true;
        }
    }

    $OwnerAliasCache[$Alias] = $false

    return $false;
}

function GetTag([object]$ResourceGroup, [string]$Key) {
  if (!$ResourceGroup.Tags) {
    return $null
  }

  foreach ($tagKey in $ResourceGroup.Tags.Keys) {
    # Compare case-insensitive
    if ($tagKey -like $Key) {
      return $ResourceGroup.Tags[$tagKey]
    }
  }

  return $null
}

function HasValidOwnerTag([object]$ResourceGroup) {
  $ownerTag = GetTag $ResourceGroup "Owners"
  if (!$ownerTag) {
    return $false
  }
  $owners = $ownerTag -split "[;, ]"
  $hasValidOwner = $false
  $invalidOwners = @()
  foreach ($owner in $owners) {
      if (IsValidAlias -Alias $owner) {
          $hasValidOwner = $true
      } else {
        $invalidOwners += $owner
      }
  }
  if ($invalidOwners) {
    Write-Warning " Resource group '$($ResourceGroup.ResourceGroupName)' has invalid owner tags: $($invalidOwners -join ',')"
  }
  if ($hasValidOwner) {
    Write-Host " Skipping tagged resource group '$($ResourceGroup.ResourceGroupName)' with owners '$owners'"
    return $true
  }
  return $false
}

function HasValidAliasInName([object]$ResourceGroup) {
    # check compliance (formatting first, then validate alias) and skip if compliant
    if ($ResourceGroup.ResourceGroupName `
      -match "^(rg-)?((t-|a-|v-)?[a-z,A-Z]{3,15})([-_]{1}.*)?$" `
      -and (IsValidAlias -Alias $matches[2]))
    {
      Write-Host " Skipping resource group '$($resourceGroup.ResourceGroupName)' starting with valid alias '$($matches[2])'"
      return $true
    }
    return $false
}

function HasExpiredDeleteAfterTag([object]$ResourceGroup) {
  $deleteAfter = GetTag $ResourceGroup "DeleteAfter"
  if ($deleteAfter) {
    $deleteDate = $deleteAfter -as [DateTime]
    return $deleteDate -and [datetime]::UtcNow -gt $deleteDate
  }
  return $false
}

function FindOrCreateDeleteAfterTag {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param(
    [object]$ResourceGroup
  )

  $deleteAfter = GetTag $ResourceGroup "DeleteAfter"
  if (!$deleteAfter -or !($deleteAfter -as [datetime])) {
    $deleteAfter = [datetime]::UtcNow.AddHours($DeleteAfterHours)
    Write-Host "Adding DeleteAfer tag with value '$deleteAfter' to group '$($ResourceGroup.ResourceGroupName)'"
    if ($Force -or $PSCmdlet.ShouldProcess("$($ResourceGroup.ResourceGroupName) [DeleteAfter (UTC): $deleteAfter]", "Update Group DeleteAfter tag")) {
      $ResourceGroup | Update-AzTag -Operation Merge -Tag @{ DeleteAfter = $deleteAfter }
    }
  }
}

function HasDeleteLock() {
  return $false
}

function DeleteOrUpdateResourceGroups() {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param()

  Write-Verbose "Fetching groups"
  $allGroups = @(Get-AzResourceGroup)
  $toDelete = @()
  $toUpdate = @()
  Write-Host "Total Resource Groups: $($allGroups.Count)"

  foreach ($rg in $allGroups) {
    if (HasExpiredDeleteAfterTag $rg) {
      $toDelete += $rg
    } elseif (HasValidAliasInName $rg) {
      continue
    } elseif (HasValidOwnerTag $rg -or HasDeleteLock $rg) {
      continue
    } else {
      $toUpdate += $rg
    }
  }

  foreach ($rg in $toUpdate) {
    FindOrCreateDeleteAfterTag $rg
  }

  # Get purgeable resources already in a deleted state.
  $purgeableResources = @(Get-PurgeableResources)

  Write-Host "Total Resource Groups To Delete: $($toDelete.Count)"
  foreach ($rg in $toDelete)
  {
    $deleteAfter = GetTag $rg "DeleteAfter"
    if ($Force -or $PSCmdlet.ShouldProcess("$($rg.ResourceGroupName) [DeleteAfter (UTC): $deleteAfter]", "Delete Group")) {
      # Add purgeable resources that will be deleted with the resource group to the collection.
      $purgeableResourcesFromRG = Get-PurgeableGroupResources $rg.ResourceGroupName

      if ($purgeableResourcesFromRG) {
        $purgeableResources += $purgeableResourcesFromRG
        Write-Verbose "Found $($purgeableResourcesFromRG.Count) potentially purgeable resources in resource group $($rg.ResourceGroupName)"
      }
      Write-Verbose "Deleting group: $($rg.ResourceGroupName)"
      Write-Verbose "  tags $($rg.Tags | ConvertTo-Json -Compress)"
      # Write-Host ($rg | Remove-AzResourceGroup -Force -AsJob).Name
    }
  }

  if (!$purgeableResources.Count) {
    return
  }
  if ($Force -or $PSCmdlet.ShouldProcess("$($purgeableResources.VaultName)", "Delete Purgeable Resources")) {
    # Purge all the purgeable resources and get a list of resources (as a collection) we need to follow-up on.
    Write-Host "Attempting to purge $($purgeableResources.Count) resources."
    $failedResources = @(Remove-PurgeableResources $purgeableResources -PassThru)
    if ($failedResources) {
      Write-Warning "Timed out deleting the following $($failedResources.Count) resources. Please file an IcM ticket per resource type."
      $failedResources | Sort-Object AzsdkResourceType, AzsdkName | Format-Table -Property @{l='Type'; e={$_.AzsdkResourceType}}, @{l='Name'; e={$_.AzsdkName}}
    }
  }
}

function Login() {
  if ($PSCmdlet.ParameterSetName -eq "Provisioner") {
    Write-Verbose "Logging in with provisioner"
    $provisionerSecret = ConvertTo-SecureString -String $ProvisionerApplicationSecret -AsPlainText -Force
    $provisionerCredential = [System.Management.Automation.PSCredential]::new($ProvisionerApplicationId, $provisionerSecret)
    Connect-AzAccount -Force -Tenant $TenantId -Credential $provisionerCredential -ServicePrincipal -Environment $Environment
    Select-AzSubscription -Subscription $SubscriptionId -Confirm:$false
  } elseif ($Login) {
    Write-Verbose "Logging in with interactive user"
    $cmd = "Connect-AzAccount"
    if ($TenantId) {
        $cmd += " -TenantId $TenantId"
    }
    if ($SubscriptionId) {
        $cmd += " -SubscriptionId $SubscriptionId"
    }
    Invoke-Expression $cmd
  } elseif (Get-AzContext) {
    Write-Verbose "Using existing account"
  } else {
    $errMsg = 'User context not found. Please re-run script with "-Login" to login, ' +
              'or run "Connect-AzAccount -UseDeviceAuthentication" if interactive login is not available.'
    Write-Error $errMsg
    exit 1
  }
}

function Main() {
  Login

  $originalSubscription = (Get-AzContext).Subscription.Id
  if ($SubscriptionId -and ($originalSubscription -ne $SubscriptionId)) {
    Select-AzSubscription -Subscription $SubscriptionId -Confirm:$false
  }

  DeleteOrUpdateResourceGroups

  if ($SubscriptionId -and ($originalSubscription -ne $SubscriptionId)) {
    Select-AzSubscription -Subscription $originalSubscription -Confirm:$false
  }
}

Main
