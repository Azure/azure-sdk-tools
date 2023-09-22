<#
.SYNOPSIS
The script is to do file content replacements under current folder.

.DESCRIPTION
We are having the script to help search all occurrences of specified strings within scope and replace to the target strings.
The script will scan all files by default. 

.PARAMETER ExcludePathsRegex
The regex of excluded file paths. It is standard regex applied to the file path of the agent. The file path is relative to repo folder. 
In win agent, the path is like ".\eng\scripts\Repo-File-Content-Replacements.ps1", so the regex path can be like "^.\\eng\\"
In linux agent, the path is like "./eng/scripts/Repo-File-Content-Replacements.ps1", so the regex path can be like "^./eng/"
To compitable with both cases, you can also specify your regex path like "^./|\\eng/|\\"
e.g we can specified multiple paths using '(^./sdk/)|(^./eng/common/)'
The script will exclude the specified paths from scanned file list first. 

.PARAMETER IncludeFromExcludedPathsRegex
The regex of file pattern which is used to add back the files you don't want to exclude. The IncludeFromExcludedPathsRegex is supposed to match a subset of ExcludePathsRegex
It is standard regex applied to the file path of the agent. The file path is relative to repo folder. 
In win agent, the path is like ".\eng\scripts\Repo-File-Content-Replacements.ps1", so the regex path can be like "^.\\eng\\"
In linux agent, the path is like "./eng/scripts/Repo-File-Content-Replacements.ps1", so the regex path can be like "^./eng/"
To compitable with both cases, you can also specify your regex path like "^./|\\eng/|\\"
e.g we can specified multiple paths using "(^./sdk/.*/platform-matrix.*.json$)|(^./sdk/.*/.*yml$)"

.PARAMETER MigrationMapJson
The map includes information of from-to pair. E.g:
[
  {
    "MigrateFrom": "azsdk-pool-mms-win-2022-general",
    "MigrateTo": "azsdk-pool-mms-win-2022-general"
  },
  {
    "MigrateFrom": "windows-2022",
    "MigrateTo": "windows-2022"
  },
  {
    "MigrateFrom": "windows2022",
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
  [string]$IncludeFromExcludedPathsRegex = '.*',
  [Parameter(Mandatory = $true)]
  [string]$MigrationMapJson,
  [Parameter(Mandatory = $false)]
  [string]$ScannedDirectory = '.'
)

$files = Get-ChildItem -LiteralPath $ScannedDirectory -Recurse -File 
$newFileCollection = @()
foreach ($file in $files) {
  $relativePath = Resolve-Path -LiteralPath $file.FullName -Relative
  $checkIncludes = $true
  if (!$ExcludePathsRegex -or $relativePath -notmatch $ExcludePathsRegex) {
    $newFileCollection += $file
    $checkIncludes = $false
  }
  if ($checkIncludes -and $IncludeFromExcludedPathsRegex -and ($relativePath -match $IncludeFromExcludedPathsRegex)) {
    $newFileCollection += $file
  }
}

# Without -NoEnumerate, a single element array[T] gets unwrapped as a single item T.
$migrationMap = ConvertFrom-Json $MigrationMapJson -NoEnumerate

Write-Host "The number of matching files: $($newFileCollection.Count)"
# Scan all Files and check the match
foreach ($file in $newFileCollection) {
  $newcontent = Get-Content -LiteralPath $file.FullName -Raw
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
