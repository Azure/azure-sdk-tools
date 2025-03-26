param(
  [string]$PRDataArtifactPath,
  [array]$PRDataInline,
  [string]$AuthToken,
  [switch]$SkipMerge
)

. "${PSScriptRoot}\..\common\scripts\logging.ps1"

$ResolveReviewAuthors = @('copilot-pull-request-reviewer')

$gql_PullRequestQuery = @'
query PullRequest($owner: String!, $name: String!, $number: Int!) {
  repository(owner: $owner, name: $name) {
    pullRequest(number: $number) {
      url
      merged
      mergeable
      state
      mergeStateStatus
      headRefOid
    }
  }
}
'@

$gql_ReviewThreadsQuery = @'
query ReviewThreads($owner: String!, $name: String!, $number: Int!) {
  repository(owner: $owner, name: $name) {
    pullRequest(number: $number) {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          comments(first: 100) {
            nodes {
              body
              author {
                login
              }
            }
          }
        }
      }
    }
  }
}
'@

$gql_ResolveThreadMutation = @'
mutation ResolveThread($id: ID!) {
  resolveReviewThread(input: { threadId: $id }) {
    thread {
      isResolved
    }
  }
}
'@

function Main() {
  if (!$AuthToken) {
    $AuthToken = gh auth token
    if ($LASTEXITCODE) {
      LogError "Failed to retrieve auth token from gh cli"
      exit 1
    }
  }

  $Headers = @{
    "Content-Type"   = "text/json"
    "Authorization"  = "bearer $AuthToken"
  }

  $prList = @()
  if ($PRDataArtifactPath) {
    foreach ($line in (Get-Content $PRDataArtifactPath)) {
      $repoOwner, $repoName, $Number = $line.Split(";")
      $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number }
    }
  }
  foreach ($line in $PRDataInline) {
    $repoOwner, $repoName, $Number = $line.Split(";")
    $prList += @{ RepoOwner = $repoOwner; RepoName = $repoName; Number = $Number }
  }

  $mergeable = ProcessPRMergeStatuses $prList

  if ($SkipMerge) {
    LogInfo "Skipping merge of $($mergeable.Length) PRs due to -SkipMerge parameter."
    return
  }

  MergePRs $mergeable
}

function ProcessPRMergeStatuses([array]$prData) {
  for ($retry = 1; $retry -le 5; $retry++)
  {
    $curr, $prData = $prData, @()
    foreach ($pr in ($curr | Where-Object { $_.Retry -ne $false }))
    {
      $prData += GetOrSetMergeablePR -repoOwner $pr.RepoOwner -repoName $pr.RepoName -prNumber $pr.Number
    }

    if ($SkipMerge -or $prData.Retry -notcontains $true) {
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
  foreach ($pr in $toMerge)
  {
    LogInfo $toMerge.HeadSHA
    $body = @{
      sha = $toMerge.HeadSHA
      merge_method = "squash"
    } | ConvertTo-Json -Compress

    try {
      LogInfo "Merging $($toMerge.MergeUrl)"
      $response = Invoke-RestMethod -Method Put -Headers $Headers $toMerge.MergeUrl -Body $body
    }
    catch {
      LogError "Invoke-RestMethod [$($toMerge.MergeUrl)] failed with exception."
      throw
    }
  }
}

function RequestGithubGraphQL([string]$query, [object]$variables = @{}) {
  $body = @{ query = $query; variables = $variables } | ConvertTo-Json -Depth 100

  try {
    $response = Invoke-RestMethod `
      -Method Post `
      -Uri "https://api.github.com/graphql" `
      -Headers $Headers `
      -Body $body `
      -ContentType "application/json"
  }
  catch {
    throw "Invoke-RestMethod failed for graphql operation: `n$query"
  }

  # The API will return 200 with a list of error messages for bad queries
  if ($response.errors) {
    throw $response.errors.message -join '`n'
  }

  return $response
}

function TryResolveAIReviewThreads([string]$repoOwner, [string]$repoName, [string]$prNumber) {
  $variables = @{ owner = $repoOwner; name = $repoName; number = [int]$prNumber }
  $response = RequestGithubGraphQL -query $gql_ReviewThreadsQuery -variables $variables
  $reviews = $response.data.repository.pullRequest.reviewThreads.nodes

  $threadIds = @()
  # There should be only one threadId for copilot, but make it an array in case there
  # are more, or if we want to resolve threads from multiple ai authors in the future.
  # Don't mark threads from humans as resolved, as those may be real questions/blockers.
  foreach ($thread in $reviews) {
    if ($thread.comments.nodes | Where-Object { $_.author.login -in $ResolveReviewAuthors }) {
      $threadIds += $thread.id
      continue
    }
  }

  if (!$threadIds) {
    return $false
  }

  if ($SkipMerge) {
    LogWarning "Skipping resolution of $($threadIds.Count) AI review threads"
    return $false
  }

  foreach ($threadId in $threadIds) {
    LogInfo "Resolving review thread '$threadId' for '$repoName' PR '$prNumber'"
    $response = RequestGithubGraphQL -query $gql_ResolveThreadMutation -variables @{ id = $threadId }
    $reviews = $response.data.repository.pullRequest.reviewThreads.nodes
  }

  return $true
}

function GetOrSetMergeablePR([string]$repoOwner, [string]$repoName, [string]$prNumber, [switch]$SkipResolveReviews) {
  $prMergeUrl = "https://api.github.com/repos/${repoOwner}/${repoName}/pulls/${prNumber}/merge"
  $prUrl = "https://github.com/${repoOwner}/${repoName}/pull/${prNumber}"

  function _output([switch]$block, [switch]$retry, [string]$headSha) {
    return @{
      Block = $retry.ToBool() -or $block.ToBool();
      Retry = $retry.ToBool();
      Url = $prUrl;
      MergeUrl = $prMergeUrl;
      HeadSHA = $headSha;
      RepoOwner = $repoOwner;
      RepoName = $repoName;
      Number = $prNumber;
    }
  }

  $variables = @{ owner = $repoOwner; name = $repoName; number = [int]$prNumber }
  $response = RequestGithubGraphQL -query $gql_PullRequestQuery -variables $variables
  $pullRequest = $response.data.repository.pullRequest

  if ($pullRequest.merged) {
    LogInfo "${prUrl} is merged."
    return
  }
  if ($pullRequest.state -ieq "CLOSED") {
    LogWarning "${prUrl} is closed. Investigate why it was not merged."
    return (_output -block -headSha $pullRequest.headRefOid)
  }
  if ($pullRequest.mergeable -ieq "MERGEABLE" -and $pullRequest.mergeStateStatus -ieq "CLEAN") {
    LogInfo "${prUrl} is ready to merge."
    return (_output -headSha $pullRequest.headRefOid)
  }
  if ($pullRequest.mergeStateStatus -ieq "BLOCKED" -and !$SkipResolveReviews) {
    $didResolve = TryResolveAIReviewThreads -repoOwner $repoOwner -repoName $repoName -prNumber $prNumber
    # If we resolved reviews, restart the merge evaluation
    if ($didResolve) {
      return GetOrSetMergeablePR -SkipResolveReviews -repoOwner $repoOwner -repoName $repoName -prNumber $prNumber
    }
  }
  if ($pullRequest.mergeStateStatus -ine "CLEAN") {
    LogWarning "${prUrl} has state '$($pullRequest.mergeStateStatus)'. Ensure all checks are green and reviewers have approved the latest commit."
    return (_output -retry -headSha $pullRequest.headRefOid)
  }

  LogWarning ($pullRequest | ConvertTo-Json -Depth 100)
  LogWarning "${prUrl} is unmergeable with state '$($pullRequest.mergeStateStatus)'. Contact the engineering system team for assistance."
  return (_output -retry -headSha $pullRequest.headRefOid)
}

Main
