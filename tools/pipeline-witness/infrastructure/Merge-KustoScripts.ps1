<#
.SYNOPSIS
    Merges Kusto scripts into a single file.
#>
param (
  [Parameter(Mandatory)]
  [string]$OutputPath
)

function ReadFiles([IO.FileInfo[]] $files) {
  foreach ($file in $files) {
      Write-Output '/////////////////////////////////////////////////////////////////////////////////////////'
      Write-Output "// Imported from $(Resolve-Path $file.FullName -Relative)"
      Write-Output ''
      Get-Content $file
      Write-Output ''
      Write-Output ''
  }
}

$outputFolder = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputFolder)) {
  New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null
}

$lines = @()

Push-Location "$PSScriptRoot/kusto"
try {
  $lines += ReadFiles (Get-ChildItem -Path "./tables/" -Include "*.kql" -Recurse)
  $lines += ReadFiles (Get-ChildItem -Path "./views/" -Include "*.kql" -Recurse)
  $lines += ReadFiles (Get-ChildItem -Path "./functions/" -Include "*.kql" -Recurse)
} finally {
  Pop-Location
}

$lines | Set-Content $OutputPath
