<#
.SYNOPSIS
  Ensures a repo-relative shallow+sparse cache clone of
  Azure/azure-rest-api-specs exists and is reasonably fresh.

.DESCRIPTION
  Primes the repo-relative azure-rest-api-specs cache that Vally's
  `environment.git` worktree fixtures point at. Used in two places:
  - Locally, before the **live** suite (`vally eval --suite scenarios-live`).
  - In the mock-vertical CI (eng/pipelines/vally-eval.yml), before each eval
    shard — the mock workflow scenarios use the same git worktree fixture, so
    the cache must exist on the agent ('git worktree add' needs the source on
    disk; Vally does not clone it).
  The unit tool suites do not consume the cache, but priming it is cheap
  (shallow + blobless + cone-sparse) so the CI step runs unconditionally.

  What it does:
  - First run: shallow + blobless + cone-sparse clone (only
    specification/contosowidgetmanager/ to keep size minimal).
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
  Cone-sparse paths to include. Default: specification/contosowidgetmanager.
  Pass @() to disable sparse-checkout (full tree).

.PARAMETER CacheRoot
  Override the cache root directory. Defaults to <repoRoot>/artifacts/specs-cache.

.EXAMPLE
  # From the Vally project root, prime the cache before the live suite:
  ./scripts/ensure-specs-clone.ps1
#>
[CmdletBinding()]
param(
    [int]      $MaxAgeHours         = 24,
    [string[]] $SparseCheckoutPaths = @('specification/contosowidgetmanager'),
    [string]   $CacheRoot,

    # Which repo/ref to cache. Defaults target Azure/azure-rest-api-specs @ main
    # (the only fixture today) so existing no-arg callers are unaffected.
    # Prime-EvalGitFixtures.ps1 passes these per discovered fixture.
    [string]   $RepoUrl  = 'https://github.com/Azure/azure-rest-api-specs.git',
    [string]   $RepoName = 'azure-rest-api-specs',
    [string]   $Ref      = 'main'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

if (-not $CacheRoot) {
    # repoRoot = four levels up from this script
    # (.../Vally/scripts -> repo root). Resolve to an absolute path.
    $repoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
    $CacheRoot = Join-Path $repoRoot 'artifacts/specs-cache'
}
$cache = Join-Path $CacheRoot $RepoName
$stamp = Join-Path $cache '.vally-last-fetch'

if (-not (Test-Path (Join-Path $cache '.git'))) {
    Write-Host "[ensure-specs-clone] Cloning $RepoName ($Ref) into cache: $cache"
    New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null
    git clone --depth 1 --filter=blob:none --no-checkout `
        $RepoUrl $cache | Out-Null
    if ($SparseCheckoutPaths.Count -gt 0) {
        git -C $cache sparse-checkout init --cone | Out-Null
        git -C $cache sparse-checkout set @SparseCheckoutPaths | Out-Null
    }
    git -C $cache checkout $Ref | Out-Null
    Set-Content -Path $stamp -Value (Get-Date -Format o)
} else {
    $isStale = $true
    if (Test-Path $stamp) {
        $age = (Get-Date) - (Get-Item $stamp).LastWriteTime
        $isStale = $age.TotalHours -gt $MaxAgeHours
    }
    if ($isStale) {
        Write-Host "[ensure-specs-clone] Refreshing cache (>$MaxAgeHours h old): $cache"
        git -C $cache fetch --depth 1 origin $Ref | Out-Null
        git -C $cache reset --hard "origin/$Ref" | Out-Null
        Set-Content -Path $stamp -Value (Get-Date -Format o)
    } else {
        Write-Host "[ensure-specs-clone] Cache is fresh (<$MaxAgeHours h): $cache"
    }
}

# Echo the cache path so the wrapper can capture it.
Write-Output $cache
