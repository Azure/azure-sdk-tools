[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$label,
  [Parameter(Mandatory = $true)]
  [string]$issueId,
  [string]$issueNumber = $issueId,
  [switch]$labelAdded,
  [string]$labelToProjectFile
)
Set-StrictMode -Version 3

. $PSScriptRoot/Github-Project-Helpers.ps1

$labelToProject = @{}

if (Test-Path $labelToProjectFile) {
  foreach ($labelMapping in (ConvertFrom-CSV (Get-Content $labelToProjectFile))) {
    $labelToProject[$labelMapping.Label] = $labelMapping
  }
}

$labelMapping = $labelToProject[$label]
if (!$labelMapping) {
  Write-Host "Label '$label' does not map to a project so skipping"
  exit 0
}
$projectName = $labelMapping.Project
$projectId = $labelMapping.ProjectId

Write-Verbose $(gh api -H "Accept: application/vnd.github.v3+json" /rate_limit --jq '.resources')

if ($labelAdded) {
  Write-Host "Adding issue '$issueNumber' to project '$projectName' because label '$label' was applied to it."
}
# Always add the item to the project even in the delete case because we need to get the project item id
# and adding an item that is already a project item does nothing but return the existing project item id
$projectItemId = Add-GithubIssueToProject $projectId $issueId

if (!$labelAdded) {
  Write-Host "Removing issue '$issueNumber' from project '$projectName' because label '$label' was removed from it."
  $projectDeletedItemId = Remove-GithubIssueFromProject $projectId $projectItemId

  if ($projectItemId -ne $projectDeletedItemId) {
    Write-Host "Failed to delete '$projectItemId' -ne '$projectDeletedItemId'."
    exit 1
  }
}
Write-Verbose (gh api -H "Accept: application/vnd.github.v3+json" /rate_limit --jq '.resources')