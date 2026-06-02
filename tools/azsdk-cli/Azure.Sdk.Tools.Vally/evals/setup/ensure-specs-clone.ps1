<#
.SYNOPSIS
  Ensures a per-user shallow+sparse cache clone of Azure/azure-rest-api-specs
  exists and is reasonably fresh.

.DESCRIPTION
  Used as a pre-run step by the Vally live-eval wrapper (Run-LiveEvals.ps1).
  Maintains a cache clone that Vally's `environment.git.source` points at,
  so individual eval YAMLs don't need a pre-existing checkout.

  - First run: shallow + blobless + cone-sparse clone (only
    specification/contosowidgetmanager/ to keep size minimal).
  - Subsequent runs within -MaxAgeHours: noop.
  - Subsequent runs past -MaxAgeHours: `git fetch --depth 1 origin main` and
    fast-forward `main`.

  Cache lives at:
    Windows: $env:USERPROFILE\.vally-cache\azure-rest-api-specs
    *nix:    $HOME/.vally-cache/azure-rest-api-specs

.PARAMETER MaxAgeHours
  Skip the `git fetch` if the cache was last refreshed within this many
  hours. Default: 24.

.PARAMETER SparseCheckoutPaths
  Cone-sparse paths to include. Default: specification/contosowidgetmanager.
  Pass @() to disable sparse-checkout (full tree).
#>
[CmdletBinding()]
param(
    [int]      $MaxAgeHours        = 24,
    [string[]] $SparseCheckoutPaths = @('specification/contosowidgetmanager')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

$cacheRoot = if ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.vally-cache' } else { Join-Path $HOME '.vally-cache' }
$cache     = Join-Path $cacheRoot 'azure-rest-api-specs'
$stamp     = Join-Path $cache '.vally-last-fetch'

if (-not (Test-Path (Join-Path $cache '.git'))) {
    Write-Host "[ensure-specs-clone] Cloning azure-rest-api-specs into cache: $cache"
    New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
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
