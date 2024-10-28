[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string] $engCommonSyncPRNumber

)

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

foreach ($repo in $repos)
{
  $prstate = gh pr view $engCommonSyncBranch -R Azure/$repo --json "url,state,mergeable,mergeStateStatus,reviews" | ConvertFrom-Json

  Write-Host "$($prstate.url) - " -NoNewline
  if ($prstate.state -eq "MERGED") {
    Write-Host "MERGED"
    continue
  }
  
  if ($prstate.reviews.author.login -notcontains $ghloggedInUser) {
    gh pr review $engCommonSyncBranch -R Azure/$repo --approve
    # Refresh after approval
    $prstate = gh pr view $engCommonSyncBranch -R Azure/$repo --json "url,state,mergeable,mergeStateStatus,reviews" | ConvertFrom-Json
  }
  else {
    Write-Host "Already approved"
  }

  if ($prstate.mergeStateStatus -ne "CLEAN") {
    Write-Host "****PR is not mergeable [$($prstate.mergeStateStatus)] and may need to be manually merged"
  }
}
