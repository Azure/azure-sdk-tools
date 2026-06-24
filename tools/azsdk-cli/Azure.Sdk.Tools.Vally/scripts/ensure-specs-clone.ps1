<#
.SYNOPSIS
  LOCAL-ONLY helper. Ensures a repo-relative shallow+sparse cache clone of
  Azure/azure-rest-api-specs exists and is reasonably fresh.

.DESCRIPTION
  This script is a convenience for running the **live** suite
  (`vally eval --suite scenarios-live`) on your own machine. It is NOT
  invoked by any pipeline — the skill-eval pipeline does not call it, and the
  hermetic PR-gate suites (unit + scenarios-mock) do not need it. CI that runs
  the live suite in the future would check out the same cache path by its own
  means; this script just saves you from doing that checkout by hand locally.

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
    [string]   $CacheRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

if (-not $CacheRoot) {
    # repoRoot = four levels up from this script
    # (.../Vally/scripts -> repo root). Resolve to an absolute path.
    $repoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
    $CacheRoot = Join-Path $repoRoot 'artifacts/specs-cache'
}
$cache = Join-Path $CacheRoot 'azure-rest-api-specs'
$stamp = Join-Path $cache '.vally-last-fetch'

if (-not (Test-Path (Join-Path $cache '.git'))) {
    Write-Host "[ensure-specs-clone] Cloning azure-rest-api-specs into cache: $cache"
    New-Item -ItemType Directory -Force -Path $CacheRoot | Out-Null
    git clone --depth 1 --filter=blob:none --no-checkout `
        https://github.com/Azure/azure-rest-api-specs.git $cache | Out-Null
    if ($SparseCheckoutPaths.Count -gt 0) {
        git -C $cache sparse-checkout init --cone | Out-Null
        git -C $cache sparse-checkout set @SparseCheckoutPaths | Out-Null
    }
    git -C $cache checkout main | Out-Null
    Set-Content -Path $stamp -Value (Get-Date -Format o)
} else {
    $isStale = $true
    if (Test-Path $stamp) {
        $age = (Get-Date) - (Get-Item $stamp).LastWriteTime
        $isStale = $age.TotalHours -gt $MaxAgeHours
    }
    if ($isStale) {
        Write-Host "[ensure-specs-clone] Refreshing cache (>$MaxAgeHours h old): $cache"
        git -C $cache fetch --depth 1 origin main | Out-Null
        git -C $cache reset --hard origin/main | Out-Null
        Set-Content -Path $stamp -Value (Get-Date -Format o)
    } else {
        Write-Host "[ensure-specs-clone] Cache is fresh (<$MaxAgeHours h): $cache"
    }
}

# Echo the cache path so the wrapper can capture it.
Write-Output $cache
