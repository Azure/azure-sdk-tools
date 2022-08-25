<#
.SYNOPSIS

.DESCRIPTION
Sync the target branch with the base branch. E.g sync live with main branch in docs repo.

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

# Working branch is base branch, e.g. main
# Fetch all branches, e.g. main
$currentBranch = (git branch --show-current)

if ($BaseBranch -ne $currentBranch) {
    Write-Host "Current branch: $currentBranch, Base branch: $BaseBranch."
    git show-ref --verify --quiet refs/heads/$BaseBranch
    if ($LASTEXITCODE -eq 0) {
        Write-Host "git checkout $BaseBranch -f"
        git checkout $BaseBranch -f 
    }
    else {
        Write-Host "git checkout -b $BaseBranch -t origin/$BaseBranch -f"
        git checkout -b $BaseBranch -t origin/$BaseBranch -f
    }
}

# Delete local target branch
git rev-parse --verify  --quiet $TargetBranch
if ($LASTEXITCODE -eq 0) {
  git branch -D $TargetBranch | Write-Host
}

# Create a new target branch from base branch
Write-Host "git checkout -b $TargetBranch"
git checkout -b $TargetBranch

Write-Host "Created the target branch $TargetBranch which sync from base branch $BaseBranch"