<#
.SYNOPSIS

.DESCRIPTION
Sync the base branch to the target branch. E.g Sync base branch to target branch

.PARAMETER BaseBranch
The base branch where we want to merge changes from. E.g. main branch in docs repo

.PARAMETER TargetBranch
The target branch where we want to merge changes to. E.g. live branch in docs repo

#>
param(
  [Parameter(mandatory=$true)]
  [string] $BaseBranch,
  [Parameter(mandatory=$true)]
  [string] $TargetBranch
)

Set-StrictMode -Version 3

# Switching working branch to base branch, e.g. main
$currentBranch = (git branch --show-current)
Write-Host "Current branch: $currentBranch, Base branch: $BaseBranch."
if ($BaseBranch -ne $currentBranch) {
    git show-ref --verify --quiet refs/heads/$BaseBranch
    if ($LASTEXITCODE -eq 0) {
        Write-Host "git checkout $BaseBranch"
        git checkout $BaseBranch
    }
    else {
        Write-Host "git checkout -b $BaseBranch -t origin/$BaseBranch"
        git checkout -b $BaseBranch -t origin/$BaseBranch
    }
}

# Always print out the commit SHA
$commitSHA = git log -1 --format=tformat:%H 
Write-Host "The $TargetBranch last commit SHA: $commitSHA"

# Sync base branch to target
git show-ref --verify --quiet refs/heads/$TargetBranch
if ($LASTEXITCODE -eq 0) {
    Write-Host "git branch -D $TargetBranch"
    git branch -D $TargetBranch
}
Write-Host "git checkout -b $TargetBranch"
git checkout -b $TargetBranch
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to checkout the $TargetBranch. "
    exit 1
}

Write-Host "Synced $BaseBranch to $TargetBranch"
