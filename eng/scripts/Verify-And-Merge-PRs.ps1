param(
  [string]$PRDataArtifactPath,
  # @('Azure;azure-sdk-for-net;1', ...)
  [array]$PRDataInline,
  [string]$AuthToken,
  [switch]$SkipMerge
)

. "${PSScriptRoot}/../common/scripts/common.ps1"

function Main([string]$prFile, [array]$prs, [string]$ghToken, [switch]$noMerge) {
  # Setup GH_TOKEN for the gh cli commands
  if ($ghToken) {
    $env:GH_TOKEN = $ghToken
  }

  $prList = @()
  if ($prFile) {
    foreach ($line in (Get-Content $prFile)) {
      $repoOwner, $repoName, $Number = $line.Split(";")
      $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number }
    }
  }
  foreach ($line in $prs) {
    $repoOwner, $repoName, $Number = $line.Split(";")
    $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number }
  }

  $mergeable = ProcessPRMergeStatuses -prData $prList -noMerge:$noMerge

  if ($noMerge) {
    LogInfo "Skipping merge of $($mergeable.Length) PRs."
    return
  }

  MergePRs $mergeable
}

function ProcessPRMergeStatuses([array]$prData, [switch]$noMerge) {
  for ($retry = 1; $retry -le 5; $retry++) {
    $currPRSet = $prData
    $prData = @()
    foreach ($pr in $currPRSet) {
      $prData += GetOrSetMergeablePR -repoOwner $pr.RepoOwner -repoName $pr.RepoName -prNumber $pr.Number
    }

    if ($noMerge -or $prData.Retry -notcontains $true) {
      break
    }

    $sleep = [Math]::Pow(2, $retry)
    LogInfo "Some PRs were not in a mergeable state, retrying after $sleep seconds..."
    Start-Sleep -Seconds $sleep
  }

  if ($prData.Block -contains $true) {
    LogError "The following sync PRs are not able to be merged. Investigate and then retry running this job to auto-merge them again"
    $prData | Where-Object { $_.Block } | ForEach-Object { LogInfo $_.Url }
    exit 1
  }

  return $prData
}

function MergePRs([array]$toMerge) {
  foreach ($pr in $toMerge) {
    LogInfo "Merging $($pr.Url) at $($pr.HeadSHA)"
    gh pr merge $pr.Url --squash --match-head-commit $pr.HeadSHA
    if ($LASTEXITCODE) {
      LogError "Failed to merge [$($pr.Url)]. See above logs for details."
      exit $LASTEXITCODE
    }
  }
}

function GetOrSetMergeablePR([string]$repoOwner, [string]$repoName, [string]$prNumber, [switch]$SkipResolveReviews) {
  $prUrl = "https://github.com/${repoOwner}/${repoName}/pull/${prNumber}"

  function _pr([switch]$block, [switch]$retry, [string]$headSha) {
    return @{
      Block     = $retry.ToBool() -or $block.ToBool();
      Retry     = $retry.ToBool();
      Url       = $prUrl;
      HeadSHA   = $headSha;
      RepoOwner = $repoOwner;
      RepoName  = $repoName;
      Number    = $prNumber;
    }
  }

  $result = gh pr view $prUrl --json "url,mergeable,state,mergeStateStatus,headRefOid"
  if ($LASTEXITCODE) {
    LogWarning "Failure looking up ${prUrl} ($LASTEXITCODE)."
    return (_pr -retry)
  }
  $pullRequest = $result | ConvertFrom-Json

  if ($pullRequest.state -eq "MERGED") {
    LogInfo "${prUrl} is merged."
    return
  }
  if ($pullRequest.state -eq "CLOSED") {
    LogWarning "${prUrl} is closed. Investigate why it was not merged."
    return (_pr -block -headSha $pullRequest.headRefOid)
  }
  if ($pullRequest.mergeable -eq "MERGEABLE" -and $pullRequest.mergeStateStatus -ieq "CLEAN") {
    LogInfo "${prUrl} is ready to merge."
    return (_pr -headSha $pullRequest.headRefOid)
  }
  if ($pullRequest.mergeStateStatus -ieq "BLOCKED" -and !$SkipResolveReviews) {
    $threads = GetUnresolvedAIReviewThreads -repoOwner $repoOwner -repoName $repoName -prNumber $prNumber
    if ($threads) {
      LogWarning "${prUrl} has state '$($pullRequest.mergeStateStatus)'. Ensure all outstanding PR review conversations are resolved."
      return (_pr -retry -headSha $pullRequest.headRefOid)
    }
    LogWarning "${prUrl} has state '$($pullRequest.mergeStateStatus)'. Ensure PR has been approved after the latest commit."
    return (_pr -retry -headSha $pullRequest.headRefOid)
  }
  if ($pullRequest.mergeStateStatus -ine "CLEAN") {
    LogWarning "${prUrl} has state '$($pullRequest.mergeStateStatus)'. Ensure all checks are green and reviewers have approved the latest commit."
    return (_pr -retry -headSha $pullRequest.headRefOid)
  }

  LogWarning ($pullRequest | ConvertTo-Json -Depth 100)
  LogWarning "${prUrl} is unmergeable with state '$($pullRequest.mergeStateStatus)'. Contact the engineering system team for assistance."
  return (_pr -retry -headSha $pullRequest.headRefOid)
}

Main -prFile $PRDataArtifactPath -prs $PRDataInline -ghToken $AuthToken -noMerge:$SkipMerge
