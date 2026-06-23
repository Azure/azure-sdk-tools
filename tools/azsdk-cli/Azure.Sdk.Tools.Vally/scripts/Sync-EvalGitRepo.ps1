<#
.SYNOPSIS
  Ensures a repo-relative shallow+sparse cache clone of a git repo
  (defaults to Azure/azure-rest-api-specs) exists and is reasonably fresh.

.DESCRIPTION
  Primes a repo-relative cache that Vally's `environment.git` worktree
  fixtures point at. The repo/ref/sparse-paths are parameterized
  (-RepoUrl/-RepoName/-Ref/-SparseCheckoutPaths); the defaults target
  Azure/azure-rest-api-specs @ main so existing no-arg callers are
  unaffected. Used in two places:
  - Locally, before the **live** suite (`vally eval --suite scenarios-live`).
  - In the mock-vertical CI (eng/pipelines/vally-eval.yml), before each eval
    shard — the mock workflow scenarios use the same git worktree fixture, so
    the cache must exist on the agent ('git worktree add' needs the source on
    disk; Vally does not clone it).
  The unit tool suites do not consume the cache, but priming it is cheap
  (shallow + blobless + cone-sparse) so the CI step runs unconditionally.

  What it does:
  - First run: shallow + blobless + cone-sparse clone (only the configured
    -SparseCheckoutPaths) to keep size minimal.
  - Subsequent runs within -MaxAgeHours: noop.
  - Subsequent runs past -MaxAgeHours: `git fetch --depth 1 origin main` and
    fast-forward `main`.

  Cache lives under the repo's gitignored `artifacts/` dir:
    <repoRoot>/artifacts/specs-cache/azure-rest-api-specs

  The live eval YAMLs point Vally's `environment.git.source` at this cache via
  a repo-relative path, so the same eval works for every contributor without
  per-user edits.

.PARAMETER MaxAgeHours
  Skip the `git fetch` if the cache was last refreshed within this many
  hours. Default: 24.

.PARAMETER SparseCheckoutPaths
  Cone-sparse paths to include. Defaults to the azure-rest-api-specs fixture
  paths the live suite uses (specification/contosowidgetmanager and
  specification/ai/Face), matching the overrides in
  Initialize-EvalGitFixtures.ps1's $KnownRepos so a standalone run mirrors CI.
  Pass @() to disable sparse-checkout (full tree).

.PARAMETER CacheRoot
  Override the cache root directory. Defaults to <repoRoot>/artifacts/specs-cache.

.EXAMPLE
  # From the Vally project root, prime the cache before the live suite:
  ./scripts/Sync-EvalGitRepo.ps1
#>
[CmdletBinding()]
param(
    [int]      $MaxAgeHours         = 24,
    [string[]] $SparseCheckoutPaths = @('specification/contosowidgetmanager', 'specification/ai/Face'),
    [string]   $CacheRoot,

    # Which repo/ref to cache. Defaults target Azure/azure-rest-api-specs @ main
    # (the only fixture today) so existing no-arg callers are unaffected.
    # Initialize-EvalGitFixtures.ps1 passes these per discovered fixture.
    [string]   $RepoUrl  = 'https://github.com/Azure/azure-rest-api-specs.git',
    [string]   $RepoName = 'azure-rest-api-specs',
    [string]   $Ref      = 'main'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

# PowerShell 7 does not throw on a non-zero native exit even under
# ErrorActionPreference='Stop', so a failed `git clone` would otherwise fall
# through to `checkout` and surface a confusing downstream error. Route every
# git call through this wrapper so a failure stops the script immediately.
function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $GitArgs)
    & git @GitArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed (exit $LASTEXITCODE)"
    }
}

if (-not $CacheRoot) {
    # repoRoot = four levels up from this script
    # (.../Vally/scripts -> repo root). Resolve to an absolute path.
    $repoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
    $CacheRoot = Join-Path $repoRoot 'artifacts/specs-cache'
}
$cache = Join-Path $CacheRoot $RepoName
$stamp = Join-Path $cache '.vally-last-fetch'

if (-not (Test-Path (Join-Path $cache '.git'))) {
    Write-Host "[Sync-EvalGitRepo] Cloning $RepoName ($Ref) into cache: $cache"
    New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null
    Invoke-Git clone --depth 1 --filter=blob:none --no-checkout $RepoUrl $cache
    if ($SparseCheckoutPaths.Count -gt 0) {
        Invoke-Git -C $cache sparse-checkout init --cone
        Invoke-Git -C $cache sparse-checkout set @SparseCheckoutPaths
    }
    Invoke-Git -C $cache checkout $Ref
    Set-Content -Path $stamp -Value (Get-Date -Format o)
} else {
    $isStale = $true
    if (Test-Path $stamp) {
        $age = (Get-Date) - (Get-Item $stamp).LastWriteTime
        $isStale = $age.TotalHours -gt $MaxAgeHours
    }
    if ($isStale) {
        Write-Host "[Sync-EvalGitRepo] Refreshing cache (>$MaxAgeHours h old): $cache"
        Invoke-Git -C $cache fetch --depth 1 origin $Ref
        Invoke-Git -C $cache reset --hard "origin/$Ref"
        Set-Content -Path $stamp -Value (Get-Date -Format o)
    } else {
        Write-Host "[Sync-EvalGitRepo] Cache is fresh (<$MaxAgeHours h): $cache"
    }
}

# Echo the cache path so the wrapper can capture it.
Write-Output $cache
