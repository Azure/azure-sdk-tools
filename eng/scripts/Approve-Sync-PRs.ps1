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
  exit $LASTEXITCODE
}

$ToolsRepo = "Azure/azure-sdk-tools"

$ghloggedInUser = (gh api user -q .login)
# Get a temp access token from the logged in az cli user for azure devops resource
$account = (az account show -o json | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0) {
  Write-Host "Az login failed, try logging in again."
  exit $LASTEXITCODE
}
if ($account.homeTenantId -ne "72f988bf-86f1-41af-91ab-2d7cd011db47") {
  Write-Host "Currently not logged into correct tenant so setting the subscription to EngSys sub in the correct tenant so token will be valid."
  az account set -s "a18897a6-7e44-457d-9260-f2854c0aca42"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to set Azure subscription. Please check your permissions and try again."
    exit $LASTEXITCODE
  }
}
$jwt_accessToken = (az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query "accessToken" --output tsv)
$headers = @{ Authorization = "Bearer $jwt_accessToken" }

# Approve pending pipeline stages (CreateSyncPRs / VerifyAndMerge) for sync pipelines
$devOpsBaseUrl = "https://dev.azure.com/azure-sdk/internal"

$syncPipelineDefinitionIds = @(
  1372, # sync-eng-common.yml
  6130  # sync-.github.yml
)

foreach ($defId in $syncPipelineDefinitionIds) {
  $buildsUrl = "$devOpsBaseUrl/_apis/build/builds?definitions=$defId&branchName=refs/pull/$ToolsPRNumber/merge&`$top=1&queryOrder=queueTimeDescending&api-version=7.0"

  try {
    $buildsResponse = Invoke-RestMethod $buildsUrl -Headers $headers
  }
  catch {
    Write-Warning "Failed to query builds for pipeline definition ${defId}: $_"
    continue
  }

  if ($buildsResponse.count -eq 0) {
    Write-Host "No builds found for pipeline definition $defId for PR #$ToolsPRNumber"
    continue
  }

  $build = $buildsResponse.value[0]
  $buildUrl = "$devOpsBaseUrl/_build/results?buildId=$($build.id)"
  Write-Host "Found build $($build.id) for pipeline definition $defId (status: $($build.status)) - $buildUrl"

  # Use build timeline to find stages with pending approvals.
  # The Checkpoint.Approval record ID in the timeline IS the approval ID.
  $timelineUrl = "$devOpsBaseUrl/_apis/build/builds/$($build.id)/timeline?api-version=7.0"

  try {
    $timeline = Invoke-RestMethod $timelineUrl -Headers $headers
  }
  catch {
    Write-Warning "Failed to get timeline for build $($build.id): $_"
    continue
  }

  $pendingCheckpointApprovals = @($timeline.records | Where-Object {
    $_.type -eq "Checkpoint.Approval" -and $_.state -eq "inProgress"
  })

  if ($pendingCheckpointApprovals.Count -eq 0) {
    Write-Host "No stages waiting for approval in build $($build.id)"
    continue
  }

  foreach ($checkpointApproval in $pendingCheckpointApprovals) {
    if ($PSCmdlet.ShouldProcess($buildUrl, "Approve pending pipeline stage")) {
      Write-Host "Approving pipeline stage (approval $($checkpointApproval.id)) for build $($build.id)..."
      $approveUrl = "$devOpsBaseUrl/_apis/pipelines/approvals?api-version=7.1"
      $body = ConvertTo-Json @(@{
        approvalId = $checkpointApproval.id
        status     = "approved"
        comment    = "Approved via Approve-Sync-PRs.ps1 for tools PR #$ToolsPRNumber"
      })
      Invoke-RestMethod $approveUrl -Method Patch -Headers $headers -Body $body -ContentType "application/json" | Out-Null
      Write-Host "Approved pipeline stage (approval $($checkpointApproval.id)) for build $($build.id)"
    }
  }
}

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
