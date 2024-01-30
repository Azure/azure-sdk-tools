param (
  [string]$OutputPath = (Join-Path $PSScriptRoot '../artifacts/merged.kql')
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

$lines += ReadFiles (Get-ChildItem -Path "$PSScriptRoot/tables/" -Include "*.kql" -Recurse)
$lines += ReadFiles (Get-ChildItem -Path "$PSScriptRoot/views/" -Include "*.kql" -Recurse)
$lines += ReadFiles (Get-ChildItem -Path "$PSScriptRoot/functions/" -Include "*.kql" -Recurse)

$lines | Set-Content $OutputPath
