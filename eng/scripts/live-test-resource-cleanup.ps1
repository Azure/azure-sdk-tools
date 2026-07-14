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

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $false)]
  [string] $ProvisionerApplicationSecret,

  [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
  [ValidateNotNullOrEmpty()]
  [string] $OpensourceApiApplicationToken,

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

  [string] $GroupFilter = '*',

  [Parameter()]
  [switch] $Force,

  [Parameter(ParameterSetName = 'Interactive')]
  [switch] $Login,

  [switch] $UseExistingAzContext,

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
      $null = $Exceptions.Add($line.Trim())
    }
  }
}

function Log($Message) {
  Write-Host $Message
}

function IsValidAlias([string]$Alias)
{
  if (!$Alias) { 
    return $false 
  }
  
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
    $users = GetAllGithubUsers -Token $OpensourceApiApplicationToken
  }
  if (!$users) {
    Write-Error "Failed to retrieve github -> microsoft alias mappings from opensource api."
    exit 1
  }
  Write-Host "Found $($users.Count) users"
  foreach ($user in $users) {
    if ($user -and $user.aad.alias) {
      $OwnerAliasCache[$user.aad.alias] = $true
    }
    if ($user -and $user.aad.userPrincipalName) {
      $OwnerAliasCache[$user.aad.userPrincipalName] = $true
    }
    if ($user -and $user.github.login) {
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
      -match '^(SSS3PT_)?(rg-)?(?<alias>(t-|a-|v-)?[a-z,A-Z]+)([-_].*)?$' `
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
      # A ReadOnly lock (at the group, subscription, or management group scope) blocks tag writes on the group.
      # Skip the update in that case to avoid a terminating error under $ErrorActionPreference = 'Stop'.
      if (HasDeleteLock $ResourceGroup) {
        return
      }
      Write-Host "Adding DeleteAfter tag with value '$deleteAfter' to group '$($ResourceGroup.ResourceGroupName)'"
      try {
        $result = $ResourceGroup | Update-AzTag -Operation Merge -Tag @{ DeleteAfter = $deleteAfter } -ErrorAction Stop
        $result
      } catch {
        $msg = $_.Exception.Message
        # Handle race conditions where the group starts deleting after we get its info, in order to avoid pipeline warning/failure emails
        # "The resource group '<group name>' is in deprovisioning state and cannot perform this operation"
        if ($msg -like '*is in deprovisioning state*') {
          Write-Host "Skipping '$($ResourceGroup.ResourceGroupName)' as it is in a deprovisioning state"
        } elseif ($msg -like '*scope(s) are locked*') {
          # A ReadOnly lock was applied between our HasDeleteLock check and the tag update, or the lock exists at an ancestor scope.
          Write-Warning "Skipping tag update for '$($ResourceGroup.ResourceGroupName)' due to a ReadOnly lock: $msg"
        } else {
          Write-Error $msg
        }
      }
    }
  }
}

function HasDoNotDeleteTag([object]$ResourceGroup) {
  $doNotDelete = GetTag $ResourceGroup "DoNotDelete"
  if ($null -ne $doNotDelete) {
    Write-Host " Skipping resource group '$($ResourceGroup.ResourceGroupName)' because it has a 'DoNotDelete' tag"
  }
  return $null -ne $doNotDelete
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
  if (!$DeleteArmDeployments -or !$ResourceGroup) {
    return
  }
  $toDelete = @()
  try {
    $toDelete = @(Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroup.ResourceGroupName `
                | Where-Object { $_ -and ($_.Outputs?.Count -or $_.Parameters?.ContainsKey('testApplicationSecret')) })
  } catch {}
  if (!$toDelete -or !$toDelete.Count) {
    return
  }
  Write-Host "Deleting $($toDelete.Count) ARM deployments for group $($ResourceGroup.ResourceGroupName) as they may contain output secrets. Deployed resources will not be affected."
  $null = $toDelete | Remove-AzResourceGroupDeployment
}

function DeleteSubscriptionDeployments() {
  $subDeployments = @(Get-AzSubscriptionDeployment)
  if (!$subDeployments) {
    return
  }
  Write-Host "Removing $($subDeployments.Count) subscription scoped deployments async"
  $subDeployments | Remove-AzSubscriptionDeployment -AsJob | Out-Null
  for ($i = 0; $i -lt 20; $i++) {
      $notStarted = Get-Job | Where-Object { $_.State -eq 'NotStarted' }
      if (!$notStarted) {
          break
      }
      Write-Host "Waiting for async jobs to start..."
      Start-Sleep 5
  }
}

function Wait-DeleteJob() {
  param(
    [Parameter(Mandatory = $true)]
    $Job,

    [Parameter(Mandatory = $true)]
    [string]$DisplayName,

    [Parameter()]
    [scriptblock]$VerifyDeleted,

    [Parameter()]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TimeoutSeconds = 900
  )

  $null = Wait-Job -Job $Job -Timeout $TimeoutSeconds

  if ($Job.State -notin @('Completed', 'Failed', 'Stopped')) {
    if ($VerifyDeleted -and (& $VerifyDeleted)) {
      return $null
    }

    return "$DisplayName delete job did not complete within $TimeoutSeconds seconds."
  }

  $jobErrors = @()
  $jobFailureMessages = @(
    $Job.ChildJobs |
      Where-Object { $_.JobStateInfo.State -eq 'Failed' -and $_.JobStateInfo.Reason } |
      ForEach-Object { $_.JobStateInfo.Reason.Message }
  )
  $null = Receive-Job -Job $Job -Keep -ErrorVariable +jobErrors -ErrorAction SilentlyContinue

  if ($Job.State -ne 'Completed') {
    $allMessages = @($jobFailureMessages + ($jobErrors | ForEach-Object { $_.ToString() })) | Where-Object { $_ }
    if ($allMessages) {
      return "$DisplayName delete job ended in state '$($Job.State)': $($allMessages -join ' | ')"
    }

    return "$DisplayName delete job ended in state '$($Job.State)'."
  }

  if ($jobErrors) {
    return "$DisplayName delete job reported errors: $((@($jobErrors | ForEach-Object { $_.ToString() }) | Where-Object { $_ }) -join ' | ')"
  }

  if ($VerifyDeleted -and -not (& $VerifyDeleted)) {
    return "$DisplayName delete job completed but the resource still exists."
  }

  return $null
}

function Remove-DependencyResources() {
  param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceType,

    [Parameter(Mandatory = $true)]
    [string]$Description
  )

  $errors = @()
  $resources = @(
    Get-AzResource -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue |
      Where-Object { $_.ResourceType -ieq $ResourceType } |
      Sort-Object ResourceId -Descending
  )

  foreach ($resource in $resources) {
    Write-Host "Deleting $Description '$($resource.Name)' in resource group '$ResourceGroupName'"
    $verifyDeleted = {
      $null -eq (Get-AzResource -ResourceId $resource.ResourceId -ErrorAction SilentlyContinue)
    }.GetNewClosure()

    $job = Remove-AzResource -ResourceId $resource.ResourceId -Force -AsJob
    $deleteError = Wait-DeleteJob -Job $job -DisplayName "$Description '$($resource.Name)'" -VerifyDeleted $verifyDeleted
    if ($deleteError) {
      $errors += $deleteError
    }
  }

  return $errors
}

function Invoke-PreDeleteResourceCleanup() {
  param(
    [Parameter(Mandatory = $true)]
    [object]$ResourceGroup
  )

  $errors = @()
  $resourceGroupName = $ResourceGroup.ResourceGroupName

  if ($ResourceGroup.ManagedBy) {
    Write-Host "Resource group '$resourceGroupName' is managed by '$($ResourceGroup.ManagedBy)'. Attempting to delete the managing resource before deleting the group."
    try {
      $managedByResourceId = $ResourceGroup.ManagedBy
      $verifyManagedResourceDeleted = {
        $null -eq (Get-AzResource -ResourceId $managedByResourceId -ErrorAction SilentlyContinue)
      }.GetNewClosure()

      $managedResourceDeleteJob = Remove-AzResource -ResourceId $managedByResourceId -Force -AsJob
      $managedResourceDeleteError = Wait-DeleteJob -Job $managedResourceDeleteJob -DisplayName "Managing resource '$managedByResourceId'" -VerifyDeleted $verifyManagedResourceDeleted
      if ($managedResourceDeleteError) {
        $errors += "Failed deleting managing resource '$managedByResourceId' for group '$resourceGroupName': $managedResourceDeleteError"
      }
    } catch {
      $errors += "Failed deleting managing resource '$($ResourceGroup.ManagedBy)' for group '$resourceGroupName': $($_.Exception.Message)"
    }
  }

  $groupLocks = @(Get-AzResourceLock -ResourceGroupName $resourceGroupName -AtScope -ErrorAction SilentlyContinue)
  foreach ($groupLock in $groupLocks) {
    Write-Host "Removing resource group lock '$($groupLock.Name)' from '$resourceGroupName'"
    try {
      Remove-AzResourceLock -LockId $groupLock.LockId -Force -ErrorAction Stop
    } catch {
      $errors += "Failed removing lock '$($groupLock.Name)' from resource group '$resourceGroupName': $($_.Exception.Message)"
    }
  }

  $resources = @(Get-AzResource -ResourceGroupName $resourceGroupName -ErrorAction SilentlyContinue)

  if (!$resources) {
    return $errors
  }

  foreach ($resource in $resources) {
    $locks = @(Get-AzResourceLock -Scope $resource.ResourceId -AtScope -ErrorAction SilentlyContinue)
    foreach ($lock in $locks) {
      Write-Host "Removing resource lock '$($lock.Name)' from '$($resource.ResourceId)'"
      try {
        Remove-AzResourceLock -LockId $lock.LockId -Force -ErrorAction Stop
      } catch {
        $errors += "Failed removing lock '$($lock.Name)' from '$($resource.ResourceId)': $($_.Exception.Message)"
      }
    }
  }

  if ($errors.Count -ne 0) {
    return $errors
  }

  $errors += @(Remove-DependencyResources -ResourceGroupName $resourceGroupName -ResourceType 'Microsoft.Search/searchServices/sharedPrivateLinkResources' -Description 'Azure AI Search shared private link resource')
  $errors += @(Remove-DependencyResources -ResourceGroupName $resourceGroupName -ResourceType 'Microsoft.Cache/Redis/linkedServers' -Description 'Azure Cache for Redis linked server')
  $errors += @(Remove-DependencyResources -ResourceGroupName $resourceGroupName -ResourceType 'Microsoft.DevCenter/projects' -Description 'DevCenter project')

  $knownBlockers = @(
    @{
      ResourceType = 'Microsoft.EventHub/namespaces/disasterRecoveryConfigs'
      Message = 'contains Event Hubs GeoDR configuration resources that must be broken or failed over before the resource group can be deleted'
    },
    @{
      ResourceType = 'Microsoft.Migrate/moveCollections'
      Message = 'contains Azure Resource Mover collections that must be cleaned up before the resource group can be deleted'
    },
    @{
      ResourceType = 'Microsoft.RecoveryServices/vaults'
      Message = 'contains Recovery Services vaults that require provider-specific teardown before the resource group can be deleted'
    },
    @{
      ResourceType = 'microsoft.visualstudio/account'
      Message = 'contains Azure DevOps organization resources that must have billing removed before the resource group can be deleted'
    },
    @{
      ResourceType = 'Microsoft.VirtualMachineImages/imageTemplates'
      Message = 'contains VM image templates that may require active runs or dependent artifacts to be removed before the resource group can be deleted'
    }
  )

  foreach ($blocker in $knownBlockers) {
    $matchedResources = @($resources | Where-Object { $_.ResourceType -ieq $blocker.ResourceType })
    if ($matchedResources) {
      $resourceIds = $matchedResources | ForEach-Object { $_.ResourceId }
      $errors += "Skipping group '$resourceGroupName' because it $($blocker.Message). Resources: $($resourceIds -join ', ')"
    }
  }

  return $errors
}

function DeleteOrUpdateResourceGroups() {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param()

  if ($IsProvisionerApp) {
    AddGithubUsersToAliasCache
  }

  Write-Verbose "Fetching groups"
  [Array]$allGroups = Retry { Get-AzResourceGroup } | Where-Object { $_.ResourceGroupName -like $GroupFilter }
  if (!$allGroups) {
      Write-Warning "No resource groups found"
      return
  }
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

  $errors = @(DeleteAndPurgeGroups $toDelete)

  foreach ($rg in $toClean) {
    try {
      DeleteArmDeployments $rg
    } catch {
      Write-Warning "Error deleting deployments for group '$($rg.ResourceGroupName)'"
      Write-Warning $_
    }
  }

  if ($errors.Count -ne 0) {
    Write-Host "Encountered errors removing some resource groups:"
    $errors | ForEach-Object { Write-Host "  $_" }
    exit 1
  }
}

function DeleteAndPurgeGroups {
  [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
  param(
    [array]$toDelete
  )

  $errors = @()
  # Get purgeable resources already in a deleted state.
  $purgeableResources = @(Get-PurgeableResources)

  if ($toDelete) {
    Write-Host "Total Resource Groups To Delete: $($toDelete.Count)"
  }
  foreach ($rg in $toDelete) {
    try {
      $deleteAfter = GetTag $rg "DeleteAfter"
      if ($Force -or $PSCmdlet.ShouldProcess("$($rg.ResourceGroupName) [DeleteAfter (UTC): $deleteAfter]", "Delete Group")) {
        $preDeleteErrors = @(Invoke-PreDeleteResourceCleanup -ResourceGroup $rg)
        if ($preDeleteErrors.Count -ne 0) {
          $errors += $preDeleteErrors
          continue
        }

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
        # These helpers throw in CI when the group prefix doesn't start with 'rg-' or 'SSS3PT_rg-'
        # (a safety guard against wildcard-matching non-live-test resources). We still want the
        # resource group delete to be attempted even if the helpers fail/throw for that reason,
        # so wrap each call in its own try/catch and continue.
        $ci = $null -ne $env:SYSTEM_TEAMPROJECTID
        if ($rg.Tags?.ContainsKey('ServiceDirectory') -and $rg.Tags.ServiceDirectory -like '*storage*') {
          try { SetStorageNetworkAccessRules -ResourceGroupName $rg.ResourceGroupName -SetFirewall -CI:$ci }
          catch { Write-Warning "SetStorageNetworkAccessRules failed for '$($rg.ResourceGroupName)': $($_.Exception.Message). Continuing with group delete." }

          try { Remove-WormStorageAccounts -GroupPrefix $rg.ResourceGroupName -CI:$ci }
          catch { Write-Warning "Remove-WormStorageAccounts failed for '$($rg.ResourceGroupName)': $($_.Exception.Message). Continuing with group delete." }
        }
        try { Remove-StorageSyncServices -GroupPrefix $rg.ResourceGroupName -CI:$ci }
        catch { Write-Warning "Remove-StorageSyncServices failed for '$($rg.ResourceGroupName)': $($_.Exception.Message). Continuing with group delete." }

        $verifyDeleted = {
          $null -eq (Get-AzResourceGroup -Name $rg.ResourceGroupName -ErrorAction SilentlyContinue)
        }.GetNewClosure()
        $job = $rg | Remove-AzResourceGroup -Force -AsJob
        Write-Host $job.Name

        $deleteError = Wait-DeleteJob -Job $job -DisplayName "Resource group '$($rg.ResourceGroupName)'" -VerifyDeleted $verifyDeleted
        if ($deleteError) {
          $errors += $deleteError
        }
      }
    } catch {
      $errorMsg = "ERROR: Failure deleting/purging group $($rg.ResourceGroupName): `n $($_.ToString())"
      Write-Warning $errorMsg
      $errors += $errorMsg
    }
  }

  if (!$purgeableResources.Count) {
    return $errors
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

  return $errors
}

function Login() {
  if ($UseExistingAzContext -and (Get-AzContext)) {
    Write-Verbose "Using existing account"
  } elseif ($PSCmdlet.ParameterSetName -eq "Provisioner" -and $ProvisionerApplicationSecret) {
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

try {
  DeleteOrUpdateResourceGroups
  DeleteSubscriptionDeployments
} finally {
  if ($SubscriptionId -and ($originalSubscription -ne $SubscriptionId)) {
    Select-AzSubscription -Subscription $originalSubscription -Confirm:$false -WhatIf:$false
  }
}
