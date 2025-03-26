[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string] $engCommonSyncPRNumber
)

. "${PSScriptRoot}/../common/scripts/logging.ps1"
. "${PSScriptRoot}/../common/scripts/Helpers/git-helpers.ps1"

gh auth status

if ($LASTEXITCODE -ne 0) {
  Write-Error "Please login via gh auth login"
  exit 1
}

$ghloggedInUser = (gh api user -q .login)
$engCommonToolsBranch = gh pr view $engCommonSyncPRNumber -R Azure/azure-sdk-tools --json "headRefName" --jq ".headRefName"

if (!$engCommonToolsBranch) {
  Write-Error "Didn't find branch for PR $engCommonSyncPRNumber in Azure/azure-sdk-tools"
  exit 1
} 

# needs to remain in sync with \eng\pipelines\templates\stages\archetype-sdk-tool-repo-sync.yml
$engCommonSyncBranch = "sync-eng/common-${engCommonToolsBranch}-${engCommonSyncPRNumber}"

# needs to remain in sync with  \eng\pipelines\eng-common-sync.yml
$repos = @(
  "azure-sdk",
  "azure-sdk-for-android",
  "azure-sdk-for-c",
  "azure-sdk-for-cpp",
  "azure-sdk-for-go",
  "azure-sdk-for-ios",
  "azure-sdk-for-java",
  "azure-sdk-for-js",
  "azure-sdk-for-net",
  "azure-sdk-for-python",
  "azure-sdk-for-rust",
  "azure-rest-api-specs"
)

$owner = "Azure"
$prFields = "number,url,state,mergeable,mergeStateStatus,reviews"

foreach ($repo in $repos)
{
  $prstate = gh pr view $engCommonSyncBranch -R benbp/$repo --json $prFields | ConvertFrom-Json

  Write-Host "$($prstate.url) - " -NoNewline
  if ($prstate.state -eq "MERGED") {
    Write-Host "MERGED"
    continue
  }
  
  if ($prstate.reviews.author.login -notcontains $ghloggedInUser) {
    gh pr review $engCommonSyncBranch -R "${owner}/${repo}" --approve
    # Refresh after approval
    $prstate = gh pr view $engCommonSyncBranch -R "${owner}/${repo}" --json $prFields | ConvertFrom-Json
  }
  else {
    Write-Host "Already approved"
  }

  if ($prstate.mergeStateStatus -eq "BLOCKED") {
    $variables = @{ owner = $owner; name = $repo; number = $prstate.number }
    $resolved = TryResolveAIReviewThreads -repoOwner $owner -repoName $repo -prNumber $prstate.number
    if ($resolved) {
      $prstate = gh pr view $engCommonSyncBranch -R "${owner}/${repo}" --json $prFields | ConvertFrom-Json
    }
  }

  if ($prstate.mergeStateStatus -ne "CLEAN") {
    Write-Host "****PR $($prstate.url) is not mergeable [state: $($prstate.mergeStateStatus)] and may need to be manually merged"
  }
}
