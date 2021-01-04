param(
  $Repository,
  $Tag,
  $AuthToken
)

. (Join-Path $PSScriptRoot common.ps1)

Write-Host "Pretending to delete $Tag from $Repository."