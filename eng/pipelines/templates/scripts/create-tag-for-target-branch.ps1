<#
.SYNOPSIS
.DESCRIPTION
The script is to push target branch to remote tag. It is for backup purpose.
.PARAMETER TargetBranch
The target branch where we want to merge changes to. E.g. in docs repo, live
.PARAMETER ReleaseTag
The tag to back up target branch.
#>
param(
  [Parameter(mandatory=$false)]
  [string] $TargetBranch,
  [Parameter(mandatory=$false)]
  [string] $ReleaseTag = "livebackup-$(Get-Date -Format 'yyyy-MM-dd')",
  [Parameter(mandatory=$true)]
  [string] $GithubUrl
)

$currentBranch = (git branch --show-current)
Write-Host "git remote add set-url $GithubUrl"
git remote add set-url $GithubUrl
if ($TargetBranch -and $TargetBranch -ne $currentBranch) {
  Write-Host "Current branch: $currentBranch, Target branch: $TargetBranch."
  git show-ref --verify --quiet refs/heads/$TargetBranch
  if ($LASTEXITCODE -eq 0) {
    Write-Host "git checkout $TargetBranch -f"
    git checkout $TargetBranch -f
  }
  else {
    Write-Host "git checkout -b $TargetBranch -t set-url/$TargetBranch -f"
    git checkout -b $TargetBranch -t set-url/$TargetBranch -f
  }
}
# git push target branch to tag.
Write-Host "git tag $ReleaseTag"
git tag $ReleaseTag
Write-Host "git push origin $ReleaseTag -f"
git push set-url $ReleaseTag -f
# Always print out the commit SHA
git log -1 --format=tformat:%H | Write-Host