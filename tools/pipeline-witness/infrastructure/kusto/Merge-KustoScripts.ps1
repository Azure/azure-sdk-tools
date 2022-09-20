param (
  [Parameter(Mandatory=$true)]
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

$lines = @()

$lines += ReadFiles (Get-ChildItem -Path "$PSScriptRoot/tables/" -Include "*.kql" -Recurse)
$lines += ReadFiles (Get-ChildItem -Path "$PSScriptRoot/functions/" -Include "*.kql" -Exclude "DashboardQuery.kql", "ManagedDefinitionBuild.kql" -Recurse)
$lines += ReadFiles (Get-Item -Path "$PSScriptRoot/functions/DashboardQuery.kql")
$lines += ReadFiles (Get-Item -Path "$PSScriptRoot/functions/ManagedDefinitionBuild.kql")

$lines | Set-Content $OutputPath
