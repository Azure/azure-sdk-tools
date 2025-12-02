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
Validates that the work item exists and is for Java language before updating.

.PARAMETER WorkItemId
The ID of the work item to update

.PARAMETER DevOpsOrganization
The Azure DevOps organization name. Default: azure-sdk

.PARAMETER DryRun
If specified, shows what would be updated without making actual changes

.OUTPUTS
Boolean indicating success or failure

.EXAMPLE
Update-JavaPackageName -WorkItemId 12345 -DevOpsOrganization "azure-sdk" -DevOpsProject "internal"

.EXAMPLE
Update-JavaPackageName -WorkItemId 12345 -DryRun
#>
function Update-JavaPackageName {
  param (
    [Parameter(Mandatory = $true)]
    [int]$WorkItemId,
    [Parameter(Mandatory = $false)]
    [string]$DevOpsOrganization = "azure-sdk",
    [Parameter(Mandatory = $false)]
    [switch]$DryRun = $false
  )

  try {
    Write-Host "Processing work item [$WorkItemId]..."
    
    # Get the work item to verify it exists and get current package name
    # Note: az boards work-item show doesn't accept --project parameter, work item IDs are org-level
    $workItemJson = az boards work-item show --id $WorkItemId --organization "https://dev.azure.com/$DevOpsOrganization" --output json 2>&1
    
    if ($LASTEXITCODE -ne 0) {
      Write-Host "ERROR: Work item [$WorkItemId] not found or access denied" -ForegroundColor Red
      Write-Host "Command: az boards work-item show --id $WorkItemId --organization https://dev.azure.com/$DevOpsOrganization" -ForegroundColor Yellow
      Write-Host "Exit Code: $LASTEXITCODE" -ForegroundColor Yellow
      Write-Host "Output: $workItemJson" -ForegroundColor Yellow
      return $false
    }
    
    $workItem = $workItemJson | ConvertFrom-Json
    
    # Verify it's a Java work item
    $language = $workItem.fields."Custom.Language"
    if ($language -ne "Java") {
      Write-Host "WARNING: Work item [$WorkItemId] is not a Java package (Language: $language). Skipping." -ForegroundColor Yellow
      return $false
    }
    
    # Get current package name
    $currentPackageName = $workItem.fields."Custom.Package"
    
    if ([string]::IsNullOrWhiteSpace($currentPackageName)) {
      Write-Host "WARNING: Work item [$WorkItemId] has empty Package field. Skipping." -ForegroundColor Yellow
      return $false
    }
    
    # Check if already in the new format (contains '+')
    if ($currentPackageName -like "*+*") {
      Write-Host "INFO: Work item [$WorkItemId] package name '$currentPackageName' already contains '+'. Skipping." -ForegroundColor Cyan
      return $true
    }
    
    # Create new package name in format "com.azure+{artifactName}"
    $newPackageName = "com.azure+$currentPackageName"
    
    Write-Host "  Current Package: $currentPackageName"
    Write-Host "  New Package:     $newPackageName"
    
    if ($DryRun) {
      Write-Host "  [DRY RUN] Would update work item [$WorkItemId]" -ForegroundColor Cyan
      return $true
    }
    
    # Update the work item using Azure CLI format
    $fieldsParam = "Custom.Package=$newPackageName"
    
    $updateResult = az boards work-item update --id $WorkItemId --fields $fieldsParam --organization "https://dev.azure.com/$DevOpsOrganization" --output json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
      Write-Host "  Successfully updated work item [$WorkItemId]" -ForegroundColor Green
      return $true
    } else {
      Write-Host "  ERROR: Failed to update work item [$WorkItemId]" -ForegroundColor Red
      Write-Host "  Error details: $updateResult" -ForegroundColor Red
      return $false
    }
  }
  catch {
    Write-Host "ERROR: Exception updating work item [$WorkItemId]: $($_.Exception.Message)" -ForegroundColor Red
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
  $skipCount = 0
  $failCount = 0
  
  foreach ($workItem in $javaWorkItems) {
    $result = Update-JavaPackageName -WorkItemId $workItem.id -DevOpsOrganization $DevOpsOrganization -DryRun:$DryRun
    
    if ($result -eq $true) {
      $successCount++
    } elseif ($result -eq $false) {
      # Check if it was skipped or failed
      $currentPackage = $workItem.fields["Custom.Package"]
      if ($currentPackage -like "*+*" -or [string]::IsNullOrWhiteSpace($currentPackage)) {
        $skipCount++
      } else {
        $failCount++
      }
    }
    
    Write-Host ""
  }
  
  # Summary
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Migration Summary" -ForegroundColor Cyan
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Total work items: $($javaWorkItems.Count)"
  Write-Host "Successfully updated: $successCount" -ForegroundColor Green
  Write-Host "Skipped: $skipCount" -ForegroundColor Yellow
  Write-Host "Failed: $failCount" -ForegroundColor Red
  
  if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN MODE - No actual changes were made" -ForegroundColor Cyan
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Cyan
  }
}
