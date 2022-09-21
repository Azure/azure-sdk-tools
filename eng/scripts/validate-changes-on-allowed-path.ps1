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

Set-StrictMode -Version 3
. (Join-Path $PSScriptRoot ../common/scripts/common.ps1)

# Fetch all branches, e.g. main, live
Write-Host "git fetch origin"
git fetch origin | Write-Host

# Working branch is base branch, e.g. main
# Checking whether the base branch exists. Suppress the error message and use the exit code for error handling.
Write-Host "Check existence on base branch"
git rev-parse --verify $BaseBranch 2>$null
if ($LASTEXITCODE -eq 0) {
  Write-Host "Swtiching to base branch $BaseBranch..."
  git checkout $BaseBranch
}
else {
  Write-Host "Creating base branch $BaseBranch..."
  git checkout -b $BaseBranch -t origin/$BaseBranch
}

# git diff between local base and remote target
$changedFiles = Get-ChangedFiles -SourceCommittish $BaseBranch `
  -TargetCommittish "origin/$TargetBranch" `
  -DiffFilterType ''

# Get all allowPath, allowPath is the regex of the path
$allowedPath = Get-Content -path $AllowlistPath

# forloop to see whether there are any changes outside of the allowlist
Write-Host "Validating the changed files..."
$fileNeedsManuallyCheck = @()
foreach ($file in $changedFiles) {
  $isValid = $false
  foreach ($path in $allowedPath) {
    if ($file -match $path) {
      $isValid = $true
      break
    }
  }
  if (!$isValid) {
    $fileNeedsManuallyCheck += $file
  }
}
if ($fileNeedsManuallyCheck) {
  Write-Warning "Here are the files outside of the allow list. Please manually check if it is safe to check into target branch."
  foreach($file in $fileNeedsManuallyCheck) {
    Write-Warning "$file"
  }
  exit 1
}
Write-Host "No changes are outside of the allowlist. Ready to merge."
