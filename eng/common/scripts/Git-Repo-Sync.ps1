[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string]$SourceRepo,

  [Parameter(Mandatory = $false)]
  [string]$SourceBranch,

  [Parameter(Mandatory = $true)]
  [string]$TargetRepo,

  [Parameter(Mandatory = $false)]
  [string]$TargetBranch,

  [Parameter(Mandatory = $true)]
  [string]$AuthToken,

  [Parameter(Mandatory = $false)]
  [string]$Rebase,
  
  $user="azure-sdk",
  $email="azuresdk@microsoft.com"
)

. (Join-Path $PSScriptRoot common.ps1)

Set-PsDebug -Trace 1
if (-not (Test-Path $SourceRepo)) {
  New-Item -Path $SourceRepo -ItemType Directory -Force
  Set-Location $SourceRepo
  git init
  git remote add Source "https://$($AuthToken)@github.com/$($SourceRepo).git"
} else {
  Set-Location $SourceRepo
}

# Check the default branch
if (!$SourceBranch) {
  $defaultBranch = (git remote show Source | Out-String) -replace "(?ms).*HEAD branch: (\w+).*", '$1'
  Write-Host "No source branch. Fetch default branch $defaultBranch."
  $SourceBranch = $defaultBranch
}

git fetch --no-tags Source $SourceBranch
if ($LASTEXITCODE -ne 0) {
  Write-Host "#`#vso[task.logissue type=error]Failed to fetch $($SourceRepo):$($SourceBranch)"
  exit 1
}

git checkout ${SourceBranch}

Function FailOnError([string]$ErrorMessage, $CleanUpScripts = 0) {
  if ($LASTEXITCODE -ne 0) {
    Write-Host "#`#vso[task.logissue type=error]$ErrorMessage"
    if ($CleanUpScripts -ne 0) { Invoke-Command $CleanUpScripts }
    exit 1
  }
}

try {
  git remote add Target "https://$($AuthToken)@github.com/$($TargetRepo).git"

  $defaultBranch = (git remote show Target | Out-String) -replace "(?ms).*HEAD branch: (\w+).*", '$1'
  if (!$SourceBranch) {
    $SourceBranch = $defaultBranch
  }
  if (!$TargetBranch) {
    $TargetBranch = $defaultBranch
  }

  if (-not $($Rebase)) {
    git checkout -B target_branch $($SourceBranch)
    git push --force Target "target_branch:refs/heads/$($TargetBranch)"
    FailOnError "Failed to push to $($TargetRepo):$($TargetBranch)"

  } else {
    git fetch --no-tags Target $TargetBranch
    FailOnError "Failed to fetch TargetBranch $($TargetBranch)."

    git checkout -B target_branch "refs/remotes/Target/$($TargetBranch)"
    git -c user.name="azure-sdk" -c user.email="azuresdk@microsoft.com" rebase --strategy-option=theirs $($SourceBranch)
    FailOnError "Failed to rebase for $($TargetRepo):$($TargetBranch)" {
      git status
      git diff
      git rebase --abort
    }

    git push --force Target "target_branch:refs/heads/$($TargetBranch)"
    FailOnError "Failed to push to $($TargetRepo):$($TargetBranch)"
  }
} finally {
  git remote remove Target
}

Set-PsDebug -Off
