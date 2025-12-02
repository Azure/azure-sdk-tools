# Script to migrate Java package work items in Azure DevOps
# Updates Package field from artifact name to groupId+artifactName format

param (
  [Parameter(Mandatory = $false)]
  [string]$DevOpsOrganization = "azure-sdk",
  [Parameter(Mandatory = $false)]
  [switch]$DryRun = $false
)

# Import DevOps work item helper functions
. (Join-Path $PSScriptRoot ".." "common" "scripts" "Helpers" "DevOps-WorkItem-Helpers.ps1")

<#
.SYNOPSIS
Find all Java package work items in Azure DevOps

.DESCRIPTION
Fetches all work items for Java language from Azure DevOps release tracking.
Custom implementation that doesn't filter by Package field to find all Java items.

.PARAMETER DevOpsOrganization
The Azure DevOps organization name. Default: azure-sdk

.PARAMETER IncludeClosed
Include closed work items in the results. Default: false

.OUTPUTS
Array of work items for Java packages

.EXAMPLE
$javaWorkItems = Find-JavaPackageWorkItem -DevOpsOrganization "azure-sdk" -DevOpsProject "internal"
#>
function Find-JavaPackageWorkItem {
  param (
    [Parameter(Mandatory = $false)]
    [string]$DevOpsOrganization = "azure-sdk",
    [Parameter(Mandatory = $false)]
    [bool]$IncludeClosed = $false
  )

  Write-Host "Fetching all Java package work items from DevOps..."
  
  # Build field list - same as FindPackageWorkItem
  $fields = @()
  $fields += "System.ID"
  $fields += "System.State"
  $fields += "System.AssignedTo"
  $fields += "System.Parent"
  $fields += "System.Tags"
  $fields += "Custom.Language"
  $fields += "Custom.Package"
  $fields += "Custom.PackageDisplayName"
  $fields += "System.Title"
  $fields += "Custom.PackageType"
  $fields += "Custom.PackageTypeNewLibrary"
  $fields += "Custom.PackageVersionMajorMinor"
  $fields += "Custom.PackageRepoPath"
  $fields += "Custom.ServiceName"

  $fieldList = ($fields | ForEach-Object { "[$_]"}) -join ", "
  
  # Build query - same as FindPackageWorkItem but without Package filter
  $query = "SELECT ${fieldList} FROM WorkItems WHERE [Work Item Type] = 'Package'"
  
  if (!$IncludeClosed) {
    $query += " AND [State] <> 'No Active Development' AND [PackageTypeNewLibrary] = true"
  }
  
  # Filter by Java language only (no Package filter)
  $query += " AND [Language] = 'Java'"
  
  # Exclude test items
  $query += " AND [Tags] NOT CONTAINS 'Release Planner App Test'"
  
  # Use the existing Invoke-Query function from DevOps-WorkItem-Helpers.ps1
  $workItems = Invoke-Query $fields $query $true

  Write-Host "Found $($workItems.Count) Java package work items"
  
  return $workItems
}

<#
.SYNOPSIS
Update Java package name to groupId+artifactName format

.DESCRIPTION
Updates the Package field in a work item from artifact name to the format "com.azure+{artifactName}".
Validates that the work item is for Java language before updating.

.PARAMETER WorkItem
The work item object (hashtable) from Invoke-Query.

.PARAMETER DevOpsOrganization
The Azure DevOps organization name. Default: azure-sdk

.PARAMETER DryRun
If specified, shows what would be updated without making actual changes

.OUTPUTS
Boolean indicating success or failure

.EXAMPLE
Update-JavaPackageName -WorkItem $workItem -DryRun

.EXAMPLE
Update-JavaPackageName -WorkItem $workItem -DevOpsOrganization "azure-sdk"
#>
function Update-JavaPackageName {
  param (
    [Parameter(Mandatory = $true)]
    [hashtable]$WorkItem,
    [Parameter(Mandatory = $false)]
    [string]$DevOpsOrganization = "azure-sdk",
    [Parameter(Mandatory = $false)]
    [switch]$DryRun = $false
  )

  try {
    $workItemId = $WorkItem.id
    Write-Host "Processing work item [$workItemId]..."
    
    # Verify it's a Java work item
    $language = $WorkItem.fields["Custom.Language"]
    if ($language -ne "Java") {
      Write-Host "WARNING: Work item [$workItemId] is not a Java package (Language: $language). Skipping." -ForegroundColor Yellow
      return $false
    }
    
    # Get current package name
    $currentPackageName = $WorkItem.fields["Custom.Package"]
    
    if ([string]::IsNullOrWhiteSpace($currentPackageName)) {
      Write-Host "WARNING: Work item [$workItemId] has empty Package field. Skipping." -ForegroundColor Yellow
      return $false
    }
    
    # Check if already in the new format (contains '+')
    if ($currentPackageName -like "*+*") {
      Write-Host "INFO: Work item [$workItemId] package name '$currentPackageName' already contains '+'. Skipping." -ForegroundColor Cyan
      return $true
    }
    
    # Create new package name in format "com.azure+{artifactName}"
    $newPackageName = "com.azure+$currentPackageName"
    
    Write-Host "  Current Package: $currentPackageName"
    Write-Host "  New Package:     $newPackageName"
    
    if ($DryRun) {
      Write-Host "  [DRY RUN] Would update work item [$workItemId]" -ForegroundColor Cyan
      return $true
    }
    
    # Update the work item using Azure CLI format
    $fieldsParam = "Custom.Package=$newPackageName"
    
    $updateResult = az boards work-item update --id $workItemId --fields $fieldsParam --organization "https://dev.azure.com/$DevOpsOrganization" --output json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
      Write-Host "  Successfully updated work item [$workItemId]" -ForegroundColor Green
      return $true
    } else {
      Write-Host "  ERROR: Failed to update work item [$workItemId]" -ForegroundColor Red
      Write-Host "  Error details: $updateResult" -ForegroundColor Red
      return $false
    }
  }
  catch {
    Write-Host "ERROR: Exception updating work item [$workItemId]: $($_.Exception.Message)" -ForegroundColor Red
    return $false
  }
}

# Main execution block
if ($PSCommandPath) {
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Java Package Work Item Migration Script" -ForegroundColor Cyan
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host ""
  Write-Host "Organization: $DevOpsOrganization"
  Write-Host "Dry Run:      $DryRun"
  Write-Host ""
  
  # Find all Java work items
  $javaWorkItems = Find-JavaPackageWorkItem -DevOpsOrganization $DevOpsOrganization
  
  if (!$javaWorkItems -or $javaWorkItems.Count -eq 0) {
    Write-Host "No Java work items to process. Exiting." -ForegroundColor Yellow
    exit 0
  }
  
  Write-Host ""
  Write-Host "Found $($javaWorkItems.Count) Java work items to process" -ForegroundColor Cyan
  Write-Host ""
  
  # Process each work item
  $successCount = 0
  $failCount = 0
  
  foreach ($workItem in $javaWorkItems) {
    $result = Update-JavaPackageName -WorkItem $workItem -DevOpsOrganization $DevOpsOrganization -DryRun:$DryRun
    
    if ($result -eq $true) {
      $successCount++
    } elseif ($result -eq $false) {
      $failCount++
    }
    
    Write-Host ""
  }
  
  # Summary
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Migration Summary" -ForegroundColor Cyan
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Total work items: $($javaWorkItems.Count)"
  Write-Host "Successfully updated: $successCount" -ForegroundColor Green
  Write-Host "Failed: $failCount" -ForegroundColor Red
  
  if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN MODE - No actual changes were made" -ForegroundColor Cyan
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Cyan
  }
}
