[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string] $project,

  [Parameter(Mandatory = $true, Position = 1)]
  [string[]] $labels,

  [Parameter()]
  [ValidateNotNullOrEmpty()]
  [string[]] $repos = @('Azure/azure-sdk', 'Azure/azure-sdk-tools', 'Azure/azure-sdk-for-net', 'Azure/azure-sdk-for-java', 'Azure/azure-sdk-for-js', 'Azure/azure-sdk-for-python', 'Azure/azure-sdk-for-c', 'Azure/azure-sdk-for-cpp', 'Azure/azure-sdk-for-go', 'Azure/azure-sdk-for-ios', 'Azure/azure-sdk-for-android'),

  [Parameter()]
  [ValidateRange(30, 1000)]
  [int] $limit = 1000
)
Set-StrictMode -Version 3

. $PSScriptRoot/Github-Project-Helpers.ps1

$projectId = Get-GithubProjectId $project

if (!$projectId) {
  Write-Error "Faild to find project id for '$project'"
  exit 1
}

Write-Host "Found project id '$projectId' for '$project'"

foreach ($repo in $repos)
{
  $issueIds = gh issue list --repo $repo --label "$labels" --limit $limit --json id --jq ".[].id"

  Write-Host "Found $($issueIds.Count) issues with label $labels and adding them to project $project"
  foreach ($issueId in $issueIds)
  {
    $projectItemId = Add-GithubIssueToProject $projectId $issueId
    Write-Verbose "Adding issue $issueId to project $project"

    if (!$projectItemId) {
      Write-Error "Failed to add issue $issueId to project $projectId"
    }
  }
}

