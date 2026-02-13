param(
  [string] $RepoRoot,
  [string] $OutFile
)

. (Join-Path $RepoRoot 'eng/common/scripts/common.ps1')

$pkgProperties = Get-AllPkgProperties

if ($OutFile) {
  $pkgProperties | ConvertTo-Json -Depth 100 | Set-Content -Path $OutFile
}

$pkgProperties
