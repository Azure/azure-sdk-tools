 #!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Sync all files from SourceRepo to TargetRepo directlly.

.PARAMETER SourceRepo
The GitHub repository that needs to be referenced.

.PARAMETER SourceBranch
The branch of Source GitHub repository that needs to be referenced.

.PARAMETER TargetRepo
The GitHub repository will synced.

.PARAMETER TargetBranch
The branch of Target GitHub repository will synced.

.PARAMETER Rebase
Keep the commit record when syning.

.PARAMETER RemovePathsJson
A JSON array of repository-relative paths that must not exist in the target repository.

#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string] $SourceRepo,

  [Parameter(Mandatory = $false)]
  [string] $SourceBranch,

  [Parameter(Mandatory = $true)]
  [string] $TargetRepo,

  [Parameter(Mandatory = $false)]
  [string] $TargetBranch,

  [Parameter(Mandatory = $false)]
  [string] $Rebase,

  [Parameter(Mandatory = $false)]
  [string] $RemovePathsJson = $env:SYNC_REMOVE_PATHS
)

. (Join-Path $PSScriptRoot ../common/scripts/common.ps1)

$User="azure-sdk"
$Email="azuresdk@microsoft.com"

Set-PsDebug -Trace 1

function Get-RepoUrl([string]$Repo) 
{
  $owner = $Repo.Split("/")[0]
  $token = Get-Item -Path "env:GH_TOKEN_${owner}" -ErrorAction SilentlyContinue
  if (!$token) {
    $token = Get-Item -Path "env:GH_TOKEN" -ErrorAction SilentlyContinue
  }

  if ($token) {
    return "https://x-access-token:$($token.Value)@github.com/${Repo}.git"
  }

  return "https://github.com/${Repo}.git"
}
Function FailOnError([string]$ErrorMessage, $CleanUpScripts = 0) {
    if ($LASTEXITCODE -ne 0) {
      $failedCode = $LASTEXITCODE
      Write-Host "#`#vso[task.logissue type=error]$ErrorMessage"
      if ($CleanUpScripts -ne 0) { Invoke-Command $CleanUpScripts }
      exit $failedCode
    }
  }

$RemovePaths = @()
if ($RemovePathsJson) {
  $RemovePaths = @($RemovePathsJson | ConvertFrom-Json)
  foreach ($path in $RemovePaths) {
    if (!$path -or [System.IO.Path]::IsPathRooted($path) -or $path -match '(^|[\\/])\.\.([\\/]|$)' -or $path -eq '.') {
      throw "RemovePaths entries must be repository-relative paths without parent directory traversal. Invalid path: '$path'"
    }
  }
}

if ($RemovePaths.Count -gt 0 -and $Rebase) {
  throw 'RemovePaths cannot be combined with Rebase.'
}

if (-not (Test-Path $SourceRepo)) {
  New-Item -Path $SourceRepo -ItemType Directory -Force
  Set-Location $SourceRepo
  git init
  git remote add Source (Get-RepoUrl $SourceRepo)
} else {
  Set-Location $SourceRepo
}

# Check the default branch
if (!$SourceBranch) {
  $defaultBranch = (git remote show Source | Out-String) -replace "(?ms).*HEAD branch: (\w+).*", '$1'
  Write-Host "No source branch. Fetch default branch $defaultBranch."
  $SourceBranch = $defaultBranch
}

git fetch --filter=tree:0 --no-tags Source $SourceBranch
FailOnError "Failed to fetch $($SourceRepo):$($SourceBranch)"

git checkout ${SourceBranch}

try {
  git remote add Target (Get-RepoUrl $TargetRepo)

  $defaultBranch = (git remote show Target | Out-String) -replace "(?ms).*HEAD branch: (\w+).*", '$1'
  if (!$SourceBranch) {
    $SourceBranch = $defaultBranch
  }
  if (!$TargetBranch) {
    $TargetBranch = $defaultBranch
  }

  if ($RemovePaths.Count -gt 0) {
    git fetch --no-tags Target $TargetBranch
    FailOnError "Failed to fetch TargetBranch $($TargetBranch)."

    $targetRef = "refs/remotes/Target/$TargetBranch"
    git checkout -B target_branch $targetRef
    FailOnError "Failed to check out TargetBranch $($TargetBranch)."

    git merge --strategy=ours --no-ff --no-commit $SourceBranch
    FailOnError "Failed to merge $($SourceRepo):$($SourceBranch) into $($TargetRepo):$($TargetBranch)" {
      git merge --abort
    }

    git read-tree --reset -u $SourceBranch
    FailOnError "Failed to update the target tree from $($SourceRepo):$($SourceBranch)."

    git rm -r -f --ignore-unmatch -- $RemovePaths
    FailOnError "Failed to remove configured paths from $($TargetRepo):$($TargetBranch)."

    $gitDirectory = git rev-parse --git-dir
    FailOnError 'Failed to find the Git directory.'
    $mergeInProgress = Test-Path (Join-Path $gitDirectory 'MERGE_HEAD')
    git diff --cached --quiet
    $hasChanges = $LASTEXITCODE -ne 0

    if ($mergeInProgress -or $hasChanges) {
      git -c user.name=$user -c user.email=$Email commit -m "Sync $SourceRepo excluding configured paths"
      FailOnError "Failed to commit filtered sync for $($TargetRepo):$($TargetBranch)."
    }

    git push Target "target_branch:refs/heads/$($TargetBranch)"
    FailOnError "Failed to push to $($TargetRepo):$($TargetBranch)"

  } elseif (-not $($Rebase)) {
    git checkout -B target_branch $($SourceBranch)
    git push Target "target_branch:refs/heads/$($TargetBranch)"
    FailOnError "Failed to push to $($TargetRepo):$($TargetBranch)"

  } else {
    git fetch --filter=tree:0 --no-tags Target $TargetBranch
    FailOnError "Failed to fetch TargetBranch $($TargetBranch)."

    git checkout -B target_branch "refs/remotes/Target/$($TargetBranch)"
    git -c user.name=$user -c user.email=$Email rebase --strategy-option=theirs $($SourceBranch)
    FailOnError "Failed to rebase for $($TargetRepo):$($TargetBranch)" {
      git status
      git diff
      git rebase --abort
    }

    git push Target "target_branch:refs/heads/$($TargetBranch)"
    FailOnError "Failed to push to $($TargetRepo):$($TargetBranch)"
  }
} finally {
  git remote remove Target
}

Set-PsDebug -Off
