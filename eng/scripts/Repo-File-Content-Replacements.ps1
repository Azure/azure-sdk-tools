<#
.SYNOPSIS
The script is to do file content replacements under current folder.

.DESCRIPTION
We are having the script to help search all occurances of specified strings within scope and replace to the target strings.
The script will scan all files by default. 

.PARAMETER ExcludePathsRegex
The regex of excluded file paths. e.g we can specified multiple paths using '(^./sdk/)|(^./eng/common/)'
The script will exclude the specified paths from scanned file list first. 

.PARAMETER IncludePathsRegex
The regex of included file paths. e.g we can specified multiple paths using "(^./sdk/.*/platform-matrix.*.json$)|(^./sdk/.*/.*yml$)"
The include paths will be added back to the scanned file list after exclusion.

.PARAMETER MigrationMapJson
The map includes information of from-to pair. E.g:
[
  {
    "MigrateFrom": "azsdk-pool-mms-win-2019-general",
    "MigrateTo": "azsdk-pool-mms-win-2022-general"
  },
  {
    "MigrateFrom": "windows-2019",
    "MigrateTo": "windows-2022"
  },
  {
    "MigrateFrom": "windows2019",
    "MigrateTo": "windows2022"
  }
]

.PARAMETER ScannedDirectory
The directory to scan against. Usually, it is repo root.

.EXAMPLE
./eng/scripts/check-spelling-in-changed-files.ps1 

This will run spell check with changes in the current branch with respect to 
`target_branch_name`

#>

[CmdletBinding()]
param (
  [Parameter(Mandatory = $false)]
  [string]$ExcludePathsRegex = '',
  [Parameter(Mandatory = $false)]
  [string]$IncludePathsRegex = '',
  [Parameter(Mandatory = $true)]
  [string]$MigrationMapJson,
  [Parameter(Mandatory = $false)]
  [string]$ScannedDirectory = '.'
)

$files = Get-ChildItem $ScannedDirectory -Recurse -File 
$newFileCollection = @()
foreach ($file in $files) {
  $relativePath = Resolve-Path -LiteralPath $file.FullName -Relative
  if ($relativePath -notmatch $ExcludePathsRegex) {
    $newFileCollection += $file
  }
  elseif ($relativePath -match $ExcludePathsRegex -and $relativePath -match $IncludePathsRegex) {
    $newFileCollection += $file
  }
}

# Without -NoEnumerate, a single element array[T] gets unwrapped as a single item T.
$migrationMap = ConvertFrom-Json $MigrationMapJson -NoEnumerate

Write-Host "The number of matching files: $($newFileCollection.Count)"
# Scan all Files and check the match
foreach ($file in $newFileCollection) {
  $newcontent = Get-Content $file.FullName -Raw
  $fileChanged = $False
  foreach ($migrate in $MigrationMap) {
    if ($newcontent -match $migrate.MigrateFrom) {
      $fileChanged = $True
      $newcontent = $newcontent -replace "$($migrate.MigrateFrom)", "$($migrate.MigrateTo)"
    }
  }
  if ($fileChanged) {
    Write-Host "File to update: $($file.FullName)"
    Set-Content -Path $file.FullName -Value $newcontent -NoNewline
  }
}
