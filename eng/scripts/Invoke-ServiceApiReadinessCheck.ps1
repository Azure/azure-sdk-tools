<#
.SYNOPSIS
Runs the service API-version readiness check for one SDK pull request and
publishes the result on that pull request.

.DESCRIPTION
The SDK pull request is read through the GitHub API; its code is never checked
out or run. The script:
1. Runs `azsdk pkg validate-service-api-readiness --record-release-plan`.
2. Creates or updates one result comment on the pull request.
3. Adds `service-api-version-check-passed` only when the result is Passed, and
   removes it otherwise.
Nothing is published when the pull request is closed or its head changed during
the check. The script fails when the CLI fails, after publishing an Unknown result.

GH_TOKEN must allow writing comments and labels on the SDK repository.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string]$PullRequestUrl,

  [Parameter(Mandatory)]
  [string]$AzsdkPath,

  [Parameter(Mandatory)]
  [string]$PipelineRunUrl
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ServiceApiReadiness-Helpers.ps1')

function Invoke-GitHubApi {
  $output = $input | gh api @args
  if ($LASTEXITCODE -ne 0) { throw "gh api $($args -join ' ') failed with exit code $LASTEXITCODE." }
  return $output
}

function Get-PullRequestState([string]$Repository, [int]$Number) {
  return Invoke-GitHubApi "repos/$Repository/pulls/$Number" | ConvertFrom-Json
}

function Set-ResultComment([string]$Repository, [int]$Number, [string]$Body) {
  $query = '.[] | select(.user.type == "Bot" and (.body | startswith("{0}"))) | .id' -f $ServiceApiReadinessCommentMarker
  $commentId = Invoke-GitHubApi --paginate "repos/$Repository/issues/$Number/comments" --jq $query | Select-Object -First 1
  $request = @{ body = $Body } | ConvertTo-Json -Compress
  if ($commentId) {
    $request | Invoke-GitHubApi -X PATCH "repos/$Repository/issues/comments/$commentId" --input - | Out-Null
    Write-Host "Updated result comment $commentId."
  }
  else {
    $request | Invoke-GitHubApi -X POST "repos/$Repository/issues/$Number/comments" --input - | Out-Null
    Write-Host 'Created result comment.'
  }
}

$pullRequest = Get-SdkPullRequest $PullRequestUrl
$repository = $pullRequest.Repository
$number = $pullRequest.Number

$before = Get-PullRequestState $repository $number
if ($before.state -ne 'open') {
  Write-Host "$PullRequestUrl is $($before.state). No check was run."
  return
}

Write-Host "Checking $PullRequestUrl at head $($before.head.sha)."
$output = & $AzsdkPath pkg validate-service-api-readiness --pull-request-url $PullRequestUrl --record-release-plan --output json
$cliExitCode = $LASTEXITCODE

if ($cliExitCode -eq 0) {
  $result = ($output -join "`n") | ConvertFrom-Json
  Write-Host "Readiness status: $($result.status) ($($result.plane))."
  $headSha = $result.headSha
  $body = Format-ServiceApiReadinessComment $result $PipelineRunUrl
  $passed = Test-ServiceApiReadinessPassed $result
}
else {
  Write-Host "##vso[task.logissue type=error]The azsdk readiness command failed with exit code $cliExitCode."
  $headSha = $before.head.sha
  $body = Format-ServiceApiReadinessErrorComment $headSha $PipelineRunUrl
  $passed = $false
}

$after = Get-PullRequestState $repository $number
if ($after.state -ne 'open' -or $after.head.sha -ne $headSha) {
  Write-Host "The pull request closed or changed during the check (checked $headSha, now $($after.head.sha)). The result was not published."
}
else {
  Set-ResultComment $repository $number $body

  $hasPassedLabel = @($after.labels | Where-Object name -EQ $ServiceApiReadinessPassedLabel).Count -gt 0
  if ($passed -and -not $hasPassedLabel) {
    @{ labels = @($ServiceApiReadinessPassedLabel) } | ConvertTo-Json -Compress | Invoke-GitHubApi -X POST "repos/$repository/issues/$number/labels" --input - | Out-Null
    Write-Host "Added $ServiceApiReadinessPassedLabel."
  }
  elseif (-not $passed -and $hasPassedLabel) {
    Invoke-GitHubApi -X DELETE "repos/$repository/issues/$number/labels/$ServiceApiReadinessPassedLabel" | Out-Null
    Write-Host "Removed $ServiceApiReadinessPassedLabel."
  }
}

if ($cliExitCode -ne 0) {
  exit 1
}
