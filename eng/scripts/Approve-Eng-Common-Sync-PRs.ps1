#!/usr/bin/env pwsh

[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string] $ToolsPRNumber,
  [Parameter()]
  [string] $PRFileOverride
)

$PSNativeCommandUseErrorActionPreference = $true

. "${PSScriptRoot}/../common/scripts/logging.ps1"
. "${PSScriptRoot}/../common/scripts/Helpers/git-helpers.ps1"

gh auth status

if ($LASTEXITCODE -ne 0) {
  Write-Error "Please login via gh auth login"
  exit 1
}

$ToolsRepo = "Azure/azure-sdk-tools"

$ghloggedInUser = (gh api user -q .login)
# Get a temp access token from the logged in az cli user for azure devops resource
$jwt_accessToken = (az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query "accessToken" --output tsv)
$headers = @{ Authorization = "Bearer $jwt_accessToken" }

$checks = gh pr checks $ToolsPRNumber -R $ToolsRepo --json "name,link" | ConvertFrom-Json
$syncChecks = $checks | Where-Object { $_.name -match "azure-sdk-tools - sync - [^(]*$" }
$prList = @()

foreach ($check in $syncChecks) {
  Write-Host "Gathering PRs from check [$($check.name)]'$($check.link)'"

  $devOpsBuild = $check.link -replace "_build/results\?buildId=(\d+)", "_apis/build/builds/`$1/artifacts?artifactName=pullrequestdata"
  try {
    if ($PRFileOverride) {
        $PrsCreatedContent = Get-Content -Raw $PRFileOverride
    }
    else {
        $response = Invoke-RestMethod $devOpsBuild -Headers $headers
        $artifactDownload = $response.resource.downloadUrl
        $PrsCreatedContent = Invoke-RestMethod $artifactDownload.Replace("format=zip","format=file&subPath=/PRsCreated.txt") -headers $headers
    }

    if ($PrsCreatedContent) {
      $PrsCreatedContent = $PrsCreatedContent.Split("`n") | Where-Object { $_ }
      foreach ($line in $PrsCreatedContent) {
        $repoOwner, $repoName, $Number = $line.Trim().Split(";")
        $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number; Repo = "$repoOwner/$repoName" }
      }
    }
  } catch {
    Write-Host "Failed while processing '$devOpsBuild'. $_"
    continue
  }
}

function getPRState($pr) {
  $prFields = "number,url,state,headRefOid,mergeable,mergeStateStatus,reviews"
  return gh pr view $pr.Number -R $pr.Repo --json $prFields | ConvertFrom-Json
}

foreach ($pr in $prList) {
  $prstate = getPRState $pr

  Write-Host "$($prstate.url) - " -NoNewline
  if ($prstate.state -eq "MERGED") {
    Write-Host "MERGED"
    continue
  }

  $commitDateString = (gh pr view $ToolsPRNumber -R $ToolsRepo --json "commits" --jq ".commits[-1].committedDate")
  $latestCommitDate = ([datetime]$commitDateString).ToUniversalTime()
  $approvalAfterCommit = $prstate.reviews | Where-Object { $_.state -eq "APPROVED" -and $_.submittedAt -gt $latestCommitDate }

  if (!$approvalAfterCommit -or $prstate.reviews.author.login -notcontains $ghloggedInUser) {
    gh pr review $pr.Number -R $pr.Repo --approve
    # Refresh after re-approval
    $prstate = getPRState $pr
  }
  else {
    Write-Host "Already approved"
  }

  if ($prstate.mergeStateStatus -eq "BLOCKED") {
    $resolved = TryResolveAIReviewThreads -repoOwner $pr.RepoOwner -repoName $pr.RepoName -prNumber $prstate.number
    if ($resolved) {
      $prstate = getPRState $pr
    }
  }

  if ($prstate.mergeStateStatus -ne "CLEAN") {
    Write-Host "****PR $($prstate.url) is not mergeable [state: $($prstate.mergeStateStatus)] and may need to be manually merged"
  }
}
