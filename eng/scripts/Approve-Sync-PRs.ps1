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

$syncApprovalEnvironmentName = "githubmerges"

function Get-SyncPRArtifactContent([int]$BuildId) {
  $artifactUrl = "$devOpsBaseUrl/_apis/build/builds/$BuildId/artifacts?artifactName=pullrequestdata&api-version=7.1"
  $response = Invoke-RestMethod $artifactUrl -Headers $headers
  $artifactDownload = $response.resource.downloadUrl

  if (!$artifactDownload) {
    throw "No pullrequestdata artifact download URL was returned for build '$BuildId'."
  }

  return Invoke-RestMethod $artifactDownload.Replace("format=zip", "format=file&subPath=/PRsCreated.txt") -Headers $headers
}

function Get-ApprovalStageRecord($Timeline, $ApprovalTimelineRecord) {
  $currentRecord = $ApprovalTimelineRecord

  while ($currentRecord -and $currentRecord.parentId) {
    $currentRecord = @($Timeline.records | Where-Object { $_.id -eq $currentRecord.parentId }) | Select-Object -First 1
    if ($currentRecord -and $currentRecord.type -eq "Stage") {
      return $currentRecord
    }
  }

  return $null
}

function Get-BuildApprovals($Build, $Timeline) {
  $approvalTimelineRecords = @($Timeline.records | Where-Object { $_.type -eq "Checkpoint.Approval" -and $_.identifier })
  return @($approvalTimelineRecords | ForEach-Object {
      $approvalTimelineRecord = $_
      $approval = Invoke-RestMethod "$devOpsBaseUrl/_apis/pipelines/approvals/$($approvalTimelineRecord.identifier)?`$expand=steps&api-version=7.1" -Headers $headers
      $stageRecord = Get-ApprovalStageRecord -Timeline $Timeline -ApprovalTimelineRecord $approvalTimelineRecord
      $environmentName = if ($stageRecord -and $Build.definition.id -in $syncPipelineDefinitionIds -and $stageRecord.identifier -in @("CreateSyncPRs", "VerifyAndMerge")) { $syncApprovalEnvironmentName } else { $null }

      [PSCustomObject]@{
        Approval                = $approval
        ApprovalTimelineRecord  = $approvalTimelineRecord
        StageRecord             = $stageRecord
        EnvironmentName         = $environmentName
      }
    })
}

function Get-BuildApprovalContext([int]$BuildId) {
  $build = Invoke-RestMethod "$devOpsBaseUrl/_apis/build/builds/$($BuildId)?api-version=7.1" -Headers $headers
  $timeline = Invoke-RestMethod "$devOpsBaseUrl/_apis/build/builds/$($BuildId)/timeline?api-version=7.1" -Headers $headers

  return [PSCustomObject]@{
    Build                = $build
    Timeline             = $timeline
    BuildApprovalRecords = Get-BuildApprovals -Build $build -Timeline $timeline
  }
}


function Get-StageRecord($Timeline, [string]$StageIdentifier) {
  return @($Timeline.records | Where-Object {
      $_.type -eq "Stage" -and
      ($_.identifier -eq $StageIdentifier -or $_.name -eq $StageIdentifier)
    }) | Select-Object -First 1
}

function Select-PendingStageApproval($BuildApprovalRecords, [string]$StageIdentifier) {
  $matchingApprovals = @($BuildApprovalRecords | Where-Object {
      $_.Approval.status -eq "pending" -and
      $_.StageRecord -and
      $_.StageRecord.identifier -eq $StageIdentifier -and
      $_.EnvironmentName -eq $syncApprovalEnvironmentName
    })

  if (!$matchingApprovals) {
    return $null
  }

  if ($matchingApprovals.Count -eq 1) {
    return $matchingApprovals[0]
  }

  Write-Host "Found multiple pending approvals for the '$StageIdentifier' stage in the '$syncApprovalEnvironmentName' environment. The script will not guess which one to approve."
  foreach ($candidate in $matchingApprovals) {
    Write-Host "  ApprovalId=$($candidate.Approval.id) Status=$($candidate.Approval.status) Stage=$($candidate.StageRecord.identifier) Environment=$($candidate.EnvironmentName)"
  }

  return $null
}

function Confirm-StageApproval([string]$StageIdentifier, [int]$BuildId) {
  do {
    $readInput = Read-Host -Prompt "$StageIdentifier for build '$BuildId' is waiting for approval. Approve it now? [y/n]"
  } while ($readInput -notmatch '^[ynYN]$')

  return $readInput -match '^[yY]$'
}

function Approve-SyncPipelineStage([int]$BuildId, [string]$StageIdentifier, [scriptblock]$PreApprovalAction = $null, $BuildApprovalContext = $null) {
  if (!$BuildApprovalContext) {
    $BuildApprovalContext = Get-BuildApprovalContext -BuildId $BuildId
  }

  $build = $BuildApprovalContext.Build
  if ($build.definition.id -notin $syncPipelineDefinitionIds) {
    return $false
  }

  $timeline = $BuildApprovalContext.Timeline
  $buildApprovalRecords = $BuildApprovalContext.BuildApprovalRecords
  $stageRecord = Get-StageRecord -Timeline $timeline -StageIdentifier $StageIdentifier

  if (!$stageRecord) {
    Write-Host "Unable to find the $StageIdentifier stage in build '$BuildId'."
    return $false
  }

  if ($stageRecord.state -ne "pending") {
    Write-Host "$StageIdentifier for build '$BuildId' is not waiting for approval. Current state: '$($stageRecord.state)'; result: '$($stageRecord.result)'."
    return $false
  }

  $approvalRecord = Select-PendingStageApproval -BuildApprovalRecords $buildApprovalRecords -StageIdentifier $StageIdentifier
  if (!$approvalRecord) {
    Write-Host "No unique pending approval was found for the $StageIdentifier stage in the '$syncApprovalEnvironmentName' environment for build '$BuildId', so the script will not approve anything automatically."
    return $false
  }

  $approval = $approvalRecord.Approval
  Write-Host "Pending $StageIdentifier approval found for build '$BuildId'."

  if ($WhatIfPreference) {
    if ($PreApprovalAction) { & $PreApprovalAction | Out-Null }
    $PSCmdlet.ShouldProcess("Azure DevOps approval '$($approval.id)'", "Approve $StageIdentifier for build '$BuildId'") | Out-Null
    return $false
  }

  if (!(Confirm-StageApproval -StageIdentifier $StageIdentifier -BuildId $BuildId)) {
    Write-Host "Skipping approval for $StageIdentifier on build '$BuildId'."
    return $false
  }

  if ($PreApprovalAction -and !(& $PreApprovalAction)) {
    return $false
  }

  if (!$PSCmdlet.ShouldProcess("Azure DevOps approval '$($approval.id)'", "Approve $StageIdentifier for build '$BuildId'")) {
    return $false
  }

  $approvalBody = ConvertTo-Json -InputObject @(
    @{
      approvalId = $approval.id
      comment    = "Approved by Approve-Sync-PRs.ps1 for Tools PR $ToolsPRNumber"
      status     = "approved"
    }
  )

  $approveUrl = "$devOpsBaseUrl/_apis/pipelines/approvals?api-version=7.1"
  Invoke-RestMethod $approveUrl -Method Patch -Headers $headers -ContentType "application/json" -Body $approvalBody | Out-Null
  Write-Host "Approved $StageIdentifier for build '$BuildId'."

  return $true
}

function Enable-ToolsPRAutoMerge() {
  $toolsPR = gh pr view $ToolsPRNumber -R $ToolsRepo --json "state,url,autoMergeRequest,isDraft" | ConvertFrom-Json

  if ($toolsPR.state -eq "MERGED") {
    Write-Host "Tools PR '$($toolsPR.url)' is already merged."
    return $true
  }

  if ($toolsPR.state -eq "CLOSED") {
    Write-Host "Tools PR '$($toolsPR.url)' is closed, so auto-merge cannot be enabled."
    return $false
  }

  if ($toolsPR.autoMergeRequest) {
    Write-Host "Auto-merge is already enabled for tools PR '$($toolsPR.url)'."
    return $true
  }

  if ($WhatIfPreference) {
    $PSCmdlet.ShouldProcess("Tools PR '$($toolsPR.url)'", "Enable auto-merge") | Out-Null
    return $false
  }

  if (!$PSCmdlet.ShouldProcess("Tools PR '$($toolsPR.url)'", "Enable auto-merge")) {
    return $false
  }

  gh pr merge $ToolsPRNumber -R $ToolsRepo --auto --squash
  if ($LASTEXITCODE) {
    throw "Failed to enable auto-merge for tools PR '$($toolsPR.url)'."
  }

  Write-Host "Enabled auto-merge for tools PR '$($toolsPR.url)'."
  return $true
}

$checks = gh pr checks $ToolsPRNumber -R $ToolsRepo --json "name,link" | ConvertFrom-Json
$syncChecks = $checks | Where-Object { $_.name -match "tools - sync-[^(]*$" }
if (!$syncChecks) {
  Write-Error "No sync pipeline runs were linked to the PR! Ensure the pipelines were triggered and linked to the PR."
  Write-Host "eng/common sync - https://dev.azure.com/azure-sdk/internal/_build?definitionId=1372"
  Write-Host ".github/* sync - https://dev.azure.com/azure-sdk/internal/_build?definitionId=6130"
  exit 1
}
$prList = @()

foreach ($check in $syncChecks) {
  Write-Host "Gathering PRs from check [$($check.name)]'$($check.link)'"

  $buildIdMatch = [regex]::Match($check.link, 'buildId=(\d+)')
  if (!$buildIdMatch.Success) {
    Write-Host "Unable to determine the Azure DevOps build ID from '$($check.link)'. Skipping."
    continue
  }
  $buildId = [int]$buildIdMatch.Groups[1].Value

  try {
    $buildApprovalContext = $null
    if ($PRFileOverride) {
      $PrsCreatedContent = Get-Content -Raw $PRFileOverride
    }
    else {
      $buildApprovalContext = Get-BuildApprovalContext -BuildId $buildId
      Write-Host "Approvals for build '$buildId':"
      foreach ($record in $buildApprovalContext.BuildApprovalRecords) {
        $stageId = if ($record.StageRecord) { $record.StageRecord.identifier } else { "<unknown>" }
        Write-Host "  ApprovalId=$($record.Approval.id) Status=$($record.Approval.status) Stage=$stageId"
      }
      $createSyncPRsStage = Get-StageRecord -Timeline $buildApprovalContext.Timeline -StageIdentifier "CreateSyncPRs"

      if (!$createSyncPRsStage) {
        Write-Host "Unable to find the CreateSyncPRs stage in build '$buildId'."
        continue
      }

      if ($createSyncPRsStage.state -eq "completed" -and $createSyncPRsStage.result -eq "succeeded") {
        $PrsCreatedContent = Get-SyncPRArtifactContent -BuildId $buildId
      }
      elseif ($createSyncPRsStage.state -eq "pending") {
        if (Approve-SyncPipelineStage -BuildId $buildId -StageIdentifier "CreateSyncPRs" -BuildApprovalContext $buildApprovalContext) {
          Write-Host "CreateSyncPRs stage approved for build '$buildId'. Run again later to get the current status."
        }
        continue
      }
      else {
        Write-Host "CreateSyncPRs for build '$buildId' has not completed successfully yet. Current state: '$($createSyncPRsStage.state)'; result: '$($createSyncPRsStage.result)'. Skipping PR artifact download."
        continue
      }
    }

    if ($PrsCreatedContent) {
      $PrsCreatedContent = $PrsCreatedContent.Split("`n") | Where-Object { $_ }
      foreach ($line in $PrsCreatedContent) {
        $repoOwner, $repoName, $Number = $line.Trim().Split(";")
        $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number; Repo = "$repoOwner/$repoName"; BuildId = $buildId }
      }
    }
  } catch {
    Write-Host "Failed while processing build '$buildId'. $_"
    continue
  }
}

function getPRState($pr, [int]$MaxRetries = 3, [int]$RetryDelaySeconds = 5) {
  $prFields = "number,url,state,headRefOid,mergeable,mergeStateStatus,reviews"
  for ($i = 0; $i -lt $MaxRetries; $i++) {
    $result = gh pr view $pr.Number -R $pr.Repo --json $prFields | ConvertFrom-Json
    if ($result.state -eq "MERGED" -or $result.mergeStateStatus -ne "UNKNOWN") {
      return $result
    }
    Write-Host "PR merge state is UNKNOWN, retrying in $RetryDelaySeconds seconds... ($($i + 1)/$MaxRetries)"
    Start-Sleep -Seconds $RetryDelaySeconds
  }
  return $result
}

foreach ($pr in $prList) {
  $prstate = getPRState $pr

  Write-Host "$($prstate.url) - " -NoNewline
  if ($prstate.state -eq "MERGED") {
    $pr.ReadyForVerifyAndMerge = $true
    Write-Host "MERGED"
    continue
  }

  $commitDateString = (gh pr view $ToolsPRNumber -R $ToolsRepo --json "commits" --jq ".commits[-1].committedDate")
  $latestCommitDate = ([datetime]$commitDateString).ToUniversalTime()
  $approvalAfterCommit = $prstate.reviews | Where-Object { $_.state -eq "APPROVED" -and $_.submittedAt -gt $latestCommitDate }

  if (!$approvalAfterCommit -or $prstate.reviews.author.login -notcontains $ghloggedInUser) {
    if ($PSCmdlet.ShouldProcess("$($pr.Repo)#$($pr.Number)", "Approve sync pull request")) {
      gh pr review $pr.Number -R $pr.Repo --approve
      # Refresh after re-approval
      $prstate = getPRState $pr
    }
    elseif (!$WhatIfPreference) {
      Write-Host "Skipping pull request approval"
    }
  }
  else {
    Write-Host "Already approved"
  }

  if ($prstate.mergeStateStatus -eq "BLOCKED") {
    if ($PSCmdlet.ShouldProcess("$($pr.Repo)#$($prstate.number)", "Resolve AI review threads")) {
      $resolved = TryResolveAIReviewThreads -repoOwner $pr.RepoOwner -repoName $pr.RepoName -prNumber $prstate.number
      if ($resolved) {
        $prstate = getPRState $pr
      }
    }
    elseif (!$WhatIfPreference) {
      Write-Host "Skipping AI review-thread resolution"
    }
  }

  if ($prstate.mergeStateStatus -ne "CLEAN") {
    Write-Host "****PR $($prstate.url) is not mergeable [state: $($prstate.mergeStateStatus)] and may need to be manually merged"
  }

  $pr.ReadyForVerifyAndMerge = ($prstate.state -eq "MERGED") -or
    ($prstate.state -eq "OPEN" -and $prstate.mergeable -eq "MERGEABLE" -and $prstate.mergeStateStatus -ieq "CLEAN")
}

if (!$PRFileOverride) {
  foreach ($buildGroup in @($prList | Where-Object { $_.BuildId } | Group-Object BuildId)) {
    $buildId = [int]$buildGroup.Name
    if ($buildGroup.Group.ReadyForVerifyAndMerge -contains $false) {
      Write-Host "VerifyAndMerge for build '$buildId' is not ready yet because not all sync PRs are merged or ready to merge."
      continue
    }

    $buildApprovalContext = Get-BuildApprovalContext -BuildId $buildId
    Approve-SyncPipelineStage -BuildId $buildId -StageIdentifier "VerifyAndMerge" -PreApprovalAction { Enable-ToolsPRAutoMerge } -BuildApprovalContext $buildApprovalContext | Out-Null
  }
}
