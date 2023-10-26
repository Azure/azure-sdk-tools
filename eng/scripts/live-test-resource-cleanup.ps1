#!/usr/bin/env pwsh

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# This script implements the resource management guidelines documented at https://github.com/Azure/azure-sdk-tools/blob/main/doc/engsys_resource_management.md

#Requires -Version 6.0
#Requires -PSEdition Core
#Requires -Modules @{ModuleName='Az.Accounts'; ModuleVersion='1.6.4'}
#Requires -Modules @{ModuleName='Az.Resources'; ModuleVersion='1.8.0'}

[CmdletBinding(DefaultParameterSetName = 'Interactive', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param (
  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
  [string] $ProvisionerApplicationId,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string] $ProvisionerApplicationSecret,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
  [string] $OpensourceApiApplicationId,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
  [string] $OpensourceApiApplicationTenantId,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string] $OpensourceApiApplicationSecret,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [Parameter(ParameterSetName = 'Interactive')]
  [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
  [string] $TenantId,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [Parameter(ParameterSetName = 'Interactive')]
  [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
  [string] $SubscriptionId,

  [Parameter(ParameterSetName = 'Provisioner')]
  [string] $GithubAliasCachePath,

  [Parameter()]
  [ValidateNotNullOrEmpty()]
  [string] $Environment = "AzureCloud",

  [Parameter()]
  [switch] $DeleteNonCompliantGroups,

  [Parameter()]
  [switch] $DeleteArmDeployments,

  [Parameter()]
  [Double] $DeleteAfterHours = 24,

  [Parameter()]
  [Double] $MaxLifespanDeleteAfterHours,

  [Parameter()]
  [string] $AllowListPath = "$PSScriptRoot/cleanup-allowlist.txt",

  [Parameter()]
  [switch] $Force,

  [Parameter(ParameterSetName = 'Interactive')]
  [switch] $Login,

  [Parameter(ValueFromRemainingArguments = $true)]
  $IgnoreUnusedArguments
)

Set-StrictMode -Version 3

# Import resource management helpers and override its Log function.
. (Join-Path $PSScriptRoot .. common scripts Helpers Resource-Helpers.ps1)
# Import helpers for querying repos.opensource.microsoft.com API
. (Join-Path $PSScriptRoot .. common scripts Helpers Metadata-Helpers.ps1)

$OwnerAliasCache = @{}
$IsProvisionerApp = $PSCmdlet.ParameterSetName -eq "Provisioner"
$Exceptions = [System.Collections.Generic.HashSet[String]]@()

function Retry([scriptblock] $Action, [int] $Attempts = 5) {
    $attempt = 0
    $sleep = 5

    while ($attempt -lt $Attempts) {
        try {
            $attempt++
            return $Action.Invoke()
        } catch {
            if ($attempt -lt $Attempts) {
                Write-Warning "Attempt $attempt failed: $_. Trying again in $sleep seconds..."
                Start-Sleep -Seconds $sleep
            } else {
                Write-Error -ErrorRecord $_
            }
        }
    }
}

function LoadAllowList() {
  if (!(Test-Path $AllowListPath)) {
    return
  }
  $lines = Get-Content $AllowListPath
  foreach ($line in $lines) {
    if ($line -and !$line.StartsWith("#")) {
      $_ = $Exceptions.Add($line.Trim())
    }
  }
}

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

  # AAD apps require a higher level of permission requiring admin consent to query the MS Graph list API
  # https://docs.microsoft.com/en-us/graph/api/user-list?view=graph-rest-1.0&tabs=http#permissions
  # The Get-AzAdUser call uses the list API under the hood (`/users/$filter=<alias>`)
  # and for some reason the Get API (`/user/<id or user principal name>`) also returns 401
  # with User.Read and User.ReadBasic.All permissions when called with an AAD app.
  # For this reason, skip trying to query MS Graph directly in provisioner mode.
  # The owner alias cache should already be pre-populated with all user records from the
  # github -> ms alias mapping retrieved via the repos.opensource.microsoft.com API, however
  # this will not include any security groups, in the case an owner tag does not contain
  # individual user aliases.
  if ($IsProvisionerApp) {
    Write-Host "Skipping MS Graph alias lookup for '$Alias' due to permissions. Owner aliases not registered with github will be treated as invalid."
    $OwnerAliasCache[$Alias] = $false
    return $false
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

function AddGithubUsersToAliasCache() {
  if ($GithubAliasCachePath -and (Test-Path $GithubAliasCachePath)) {
    Write-Host "Loading github -> microsoft alias mappings from filesystem cache '$GithubAliasCachePath'."
    $users = Get-Content $GithubAliasCachePath | ConvertFrom-Json -AsHashtable
  } else {
    Write-Host "Retrieving github -> microsoft alias mappings from opensource API."
    $users = GetAllGithubUsers $OpensourceApiApplicationTenantId $OpensourceApiApplicationId $OpensourceApiApplicationSecret
  }
  if (!$users) {
    Write-Error "Failed to retrieve github -> microsoft alias mappings from opensource api."
    exit 1
  }
  foreach ($user in $users) {
    if ($user.aad.alias) {
      $OwnerAliasCache[$user.aad.alias] = $true
    }
    if ($user.aad.userPrincipalName) {
      $OwnerAliasCache[$user.aad.userPrincipalName] = $true
    }
    if ($user.github.login) {
      $OwnerAliasCache[$user.github.login] = $true
    }
  }
  Write-Host "Found $($OwnerAliasCache.Count) valid github or microsoft aliases."
  if ($GithubAliasCachePath -and !(Test-Path $GithubAliasCachePath)) {
      $cacheDir = Split-Path $GithubAliasCachePath
      if ($cacheDir -and $cacheDir -ne '.') {
          New-Item -Type Directory -Force $cacheDir -WhatIf:$false
      }
      Write-Host "Caching github -> microsoft alias mappings to '$GithubAliasCachePath'"
      $users | ConvertTo-Json -Depth 4 | Out-File $GithubAliasCachePath -WhatIf:$false
  }
}

function GetTag([object]$ResourceGroup, [string]$Key) {
  if (!$ResourceGroup.Tags) {
    return $null
  }

  foreach ($tagKey in $ResourceGroup.Tags.Keys) {
    # Compare case-insensitive
    if ($tagKey -ieq $Key) {
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
    Write-Host " Found tagged resource group '$($ResourceGroup.ResourceGroupName)' with owners '$owners'"
    return $true
  }
  return $false
}

function HasValidAliasInName([object]$ResourceGroup) {
    # check compliance (formatting first, then validate alias) and skip if compliant
    if ($ResourceGroup.ResourceGroupName `
      -match '^(rg-)?(?<alias>(t-|a-|v-)?[a-z,A-Z]+)([-_].*)?$' `
      -and (IsValidAlias -Alias $matches['alias']))
    {
      Write-Host " Found resource group '$($ResourceGroup.ResourceGroupName)' starting with valid alias '$($matches['alias'])'"
      return $true
    }
    return $false
}

function GetDeleteAfterTag([object]$ResourceGroup) {
  return GetTag $ResourceGroup "DeleteAfter"
}

function HasExpiredDeleteAfterTag([string]$DeleteAfter) {
  if ($DeleteAfter) {
    $deleteDate = $deleteAfter -as [DateTime]
    return $deleteDate -and [datetime]::UtcNow -gt $deleteDate
  }
  return $false
}

function HasException([object]$ResourceGroup) {
  foreach ($ex in $Exceptions) {
    if ($ResourceGroup.ResourceGroupName -like $ex) {
      Write-Host " Skipping allowed resource group '$($ResourceGroup.ResourceGroupName)' because it matches pattern '$ex' in the allow list '$AllowListPath'"
      return $true
    }
  }
  return $false
}

function FindOrCreateDeleteAfterTag {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param(
    [object]$ResourceGroup,
    [Double]$HoursToDelete
  )

  if (!$DeleteNonCompliantGroups -or !$ResourceGroup) {
      return
  }

  # Possible states are Canceled, Deleting, Failed, InProgress, Succeeded
  # https://learn.microsoft.com/dotnet/api/microsoft.azure.management.websites.models.provisioningstate
  if ($ResourceGroup.ProvisioningState -in @('Deleting', 'InProgress')) {
      Write-Host "Skipping tag query/update for group '$($ResourceGroup.ResourceGroupName)' as it is in '$($ResourceGroup.ProvisioningState)' state"
      return
  }

  $deleteAfter = GetTag $ResourceGroup "DeleteAfter"
  if (!$deleteAfter -or !($deleteAfter -as [datetime])) {
    $deleteAfter = [datetime]::UtcNow.AddHours($HoursToDelete)
    if ($Force -or $PSCmdlet.ShouldProcess("$($ResourceGroup.ResourceGroupName) [DeleteAfter (UTC): $deleteAfter]", "Adding DeleteAfter Tag to Group")) {
      Write-Host "Adding DeleteAfter tag with value '$deleteAfter' to group '$($ResourceGroup.ResourceGroupName)'"
      $result = ($ResourceGroup | Update-AzTag -Operation Merge -Tag @{ DeleteAfter = $deleteAfter }) 2>&1
      if ("Exception" -in $result.PSObject.Properties.Name) {
          # Handle race conditions where the group starts deleting after we get its info, in order to avoid pipeline warning/failure emails
          # "The resource group '<group name>' is in deprovisioning state and cannot perform this operation"
          if ($result.Exception.Message -notlike '*is in deprovisioning state*') {
              Write-Error $result.Exception.Message
          } else {
              Write-Host "Skipping '$($ResourceGroup.ResourceGroupName)' as it is in a deprovisioning state"
          }
      } else {
          $result
      }
    }
  }
}

function HasDoNotDeleteTag([object]$ResourceGroup) {
  $doNotDelete = GetTag $ResourceGroup "DoNotDelete"
  if ($doNotDelete -ne $null) {
    Write-Host " Skipping resource group '$($ResourceGroup.ResourceGroupName)' because it has a 'DoNotDelete' tag"
  }
  return $doNotDelete -ne $null
}

function IsChildResource([object]$ResourceGroup) {
  if ($ResourceGroup.ManagedBy) {
    Write-Host " Skipping resource group '$($ResourceGroup.ResourceGroupName)' because it is managed by '$($ResourceGroup.ManagedBy)'"
    return $true
  }
  return $false
}

function HasDeleteLock([object]$ResourceGroup) {
  $lock = Get-AzResourceLock -ResourceGroupName $ResourceGroup.ResourceGroupName
  if ($lock) {
    Write-Host " Skipping locked resource group '$($ResourceGroup.ResourceGroupName)'"
    return $true
  }
  return $false
}

function DeleteArmDeployments([object]$ResourceGroup) {
  if (!$DeleteArmDeployments) {
    return
  }
  $toDelete = @(Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroup.ResourceGroupName `
              | Where-Object { $_ -and ($_.Outputs?.Count -or $_.Parameters?.ContainsKey('testApplicationSecret')) })
  if (!$toDelete -or !$toDelete.Count) {
    return
  }
  Write-Host "Deleting $($toDelete.Count) ARM deployments for group $($ResourceGroup.ResourceGroupName) as they may contain output secrets. Deployed resources will not be affected."
  $null = $toDelete | Remove-AzResourceGroupDeployment
}

function DeleteOrUpdateResourceGroups() {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param()

  if ($IsProvisionerApp) {
    AddGithubUsersToAliasCache
  }

  Write-Verbose "Fetching groups"
  [Array]$allGroups = Retry { Get-AzResourceGroup }
  $toDelete = @()
  $toClean = @()
  $toDeleteSoon = @()
  $toDeleteLater = @()
  Write-Host "Total Resource Groups: $($allGroups.Count)"

  foreach ($rg in $allGroups) {
    if (HasException $rg) {
      continue
    }
    $deleteAfter = GetDeleteAfterTag $rg
    if ($deleteAfter) {
      if (HasExpiredDeleteAfterTag $deleteAfter) {
        $toDelete += $rg
      } else {
        $toClean += $rg
      }
      continue
    }
    if ((IsChildResource $rg) -or (HasDeleteLock $rg)) {
      continue
    }
    if (HasDoNotDeleteTag $rg) {
      $toClean += $rg
      continue
    }
    if ((HasValidAliasInName $rg) -or (HasValidOwnerTag $rg)) {
      $toClean += $rg
      $toDeleteLater += $rg
      continue
    }

    $toDeleteSoon += $rg
  }


  foreach ($rg in $toDeleteSoon) {
    FindOrCreateDeleteAfterTag -ResourceGroup $rg -HoursToDelete $DeleteAfterHours
  }

  if ($MaxLifeSpanDeleteAfterHours) {
    foreach ($rg in $toDeleteLater) {
      FindOrCreateDeleteAfterTag -ResourceGroup $rg -HoursToDelete $MaxLifespanDeleteAfterHours
    }
  }

  DeleteAndPurgeGroups $toDelete

  foreach ($rg in $toClean) {
    DeleteArmDeployments $rg
  }
}

function DeleteAndPurgeGroups([array]$toDelete) {
  # Get purgeable resources already in a deleted state.
  $purgeableResources = @(Get-PurgeableResources)

  if ($toDelete) {
    Write-Host "Total Resource Groups To Delete: $($toDelete.Count)"
  }
  foreach ($rg in $toDelete)
  {
    $deleteAfter = GetTag $rg "DeleteAfter"
    if ($Force -or $PSCmdlet.ShouldProcess("$($rg.ResourceGroupName) [DeleteAfter (UTC): $deleteAfter]", "Delete Group")) {
      # Add purgeable resources that will be deleted with the resource group to the collection.
      $purgeableResourcesFromRG = @(Get-PurgeableGroupResources $rg.ResourceGroupName)

      if ($purgeableResourcesFromRG) {
        $purgeableResources += $purgeableResourcesFromRG
        Write-Verbose "Found $($purgeableResourcesFromRG.Count) potentially purgeable resources in resource group $($rg.ResourceGroupName)"
      }

      Write-Verbose "Deleting group: $($rg.ResourceGroupName)"
      Write-Verbose "  tags $($rg.Tags | ConvertTo-Json -Compress)"

      # For storage tests specifically, if they are aborted then blobs with immutability policies
      # can be left around which prevent deletion.
      if ($rg.Tags?.ContainsKey('ServiceDirectory') -and $rg.Tags.ServiceDirectory -like '*storage*') {
        & $PSScriptRoot/Remove-WormStorageAccounts.ps1 -GroupPrefix $rg.ResourceGroupName
      } else {
        Write-Host ($rg | Remove-AzResourceGroup -Force -AsJob).Name
      }
    }
  }

  if (!$purgeableResources.Count) {
    return
  }
  if ($Force -or $PSCmdlet.ShouldProcess("Purgable Resources", "Delete Purgeable Resources")) {
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
    Retry {
        Connect-AzAccount -Force -Tenant $TenantId -Credential $provisionerCredential -ServicePrincipal -Environment $Environment -WhatIf:$false
    }
    Select-AzSubscription -Subscription $SubscriptionId -Confirm:$false -WhatIf:$false
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

LoadAllowList
Login

$originalSubscription = (Get-AzContext).Subscription.Id
if ($SubscriptionId -and ($originalSubscription -ne $SubscriptionId)) {
  Select-AzSubscription -Subscription $SubscriptionId -Confirm:$false -WhatIf:$false
}

DeleteOrUpdateResourceGroups

if ($SubscriptionId -and ($originalSubscription -ne $SubscriptionId)) {
  Select-AzSubscription -Subscription $originalSubscription -Confirm:$false -WhatIf:$false
}
