<#
.SYNOPSIS

.DESCRIPTION
The script is to validate if the diff changes between base and target are within the scope of allowlist.

.PARAMETER BaseBranch
The base branch where we want to merge changes from. E.g. main branch in docs repo

.PARAMETER TargetBranch
The target branch where we want to merge changes to. E.g. live branch in docs repo

.PARAMETER AllowlistPath
The file path of allowlist.
#>
param(
  [Parameter(mandatory=$true)]
  [string] $BaseBranch,
  [Parameter(mandatory=$true)]
  [string] $TargetBranch,
  [Parameter(mandatory=$true)]
  [string] $AllowlistPath
)

# Working branch is base branch, e.g. main
# Fetch all branches, e.g. main
Write-Host "git -c user.name=`"azure-sdk`" -c user.email=`"azuresdk@microsoft.com`" fetch origin"
git -c user.name="azure-sdk" -c user.email="azuresdk@microsoft.com" fetch origin | Write-Host

# Live branch tracks remote live branch.
Write-Host "git checkout -b $BaseBranch -t origin/$BaseBranch"
git rev-parse --verify $BaseBranch
if ($LASTEXITCODE -eq 0) {
  git checkout $BaseBranch | Write-Host
}
else {
  git checkout -b $BaseBranch -t origin/$BaseBranch | Write-Host
}

# git diff on all files
$changedFiles = git diff --name-only $TargetBranch

# Get all allowPath, allowPath is the regex of the path
$allowedPath = Get-Content -path $AllowlistPath

# forloop to see whether there are any changes outside of the allowlist
Write-Host "Validating the changed files..."
foreach ($file in $changedFiles) {
    $matched = $allowedPath | Where-Object {$file -match $_}
    if ($matched.Count -eq 0) {
        Write-Error "The $file is the change outside of the allowlist. Please go check your changes and manually merge to $TargetBranch"
        exit 1
    } 
}
Write-Host "No changes are outside of the allowlist. Ready to merge."