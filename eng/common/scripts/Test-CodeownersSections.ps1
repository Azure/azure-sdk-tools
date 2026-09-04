# cSpell:ignore CODEOWNERS
<#
  .SYNOPSIS
  Tests that specified CODEOWNERS sections are identical between two file versions.

  .DESCRIPTION
  Slices named sections out of a "before" and "after" copy of the CODEOWNERS file
  and compares them.  If any of the specified sections differ between the two
  files the script exits with code 1.

  A section is delimited by the three-line fence the CODEOWNERS format uses:

    ###
    # Client Libraries
    ###

  and runs until the next "###" line or the end of the file.

  All filesystem and git setup (creating the before/after files, etc.) is
  expected to be done by the calling pipeline step template.

  .PARAMETER BeforeFile
  Path to the CODEOWNERS file representing the base state (e.g. parent commit).

  .PARAMETER AfterFile
  Path to the CODEOWNERS file representing the current state (e.g. PR head).

  .PARAMETER Sections
  An array of section names to compare (e.g. "Client Libraries").

  .PARAMETER TempDirectory
  Scratch directory for intermediate section export files.
#>
[CmdletBinding()]
param (
  [Parameter(Mandatory)]
  [string] $BeforeFile,

  [Parameter(Mandatory)]
  [string] $AfterFile,

  [Parameter(Mandatory)]
  [string[]] $Sections,

  [string] $TempDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) "codeowners-check")
)

."$PSScriptRoot\common.ps1"

Set-StrictMode -Version 3
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. Validate inputs
# ---------------------------------------------------------------------------
if (-not (Test-Path $BeforeFile)) {
  Write-Error "BeforeFile not found: $BeforeFile"
  exit 1
}
if (-not (Test-Path $AfterFile)) {
  Write-Error "AfterFile not found: $AfterFile"
  exit 1
}
# ---------------------------------------------------------------------------
# 2. Section slicing
# ---------------------------------------------------------------------------
function Get-CodeownersSection {
  param (
    [Parameter(Mandatory)] [string[]] $Lines,
    [Parameter(Mandatory)] [string] $SectionName
  )

  $fence = '###'
  $header = "# $SectionName"

  for ($i = 0; $i -lt $Lines.Count - 2; $i++) {
    if ($Lines[$i].Trim() -ne $fence) { continue }
    if ($Lines[$i + 1].Trim() -ne $header) { continue }
    if ($Lines[$i + 2].Trim() -ne $fence) { continue }

    $end = $Lines.Count
    for ($j = $i + 3; $j -lt $Lines.Count; $j++) {
      if ($Lines[$j].Trim() -eq $fence) { $end = $j; break }
    }

    return , $Lines[$i..($end - 1)]
  }

  return $null
}

# ---------------------------------------------------------------------------
# 3. Ensure temp directory exists
# ---------------------------------------------------------------------------
if (-not (Test-Path $TempDirectory)) {
  New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
}

# ---------------------------------------------------------------------------
# 4. Export and compare each section
# ---------------------------------------------------------------------------
$failed = $false

$beforePath = Resolve-Path $BeforeFile
Write-Host "Before file: $beforePath"
$afterPath  = Resolve-Path $AfterFile
Write-Host "After file:  $afterPath"

$beforeLines = @(Get-Content -Path $beforePath)
$afterLines  = @(Get-Content -Path $afterPath)

foreach ($section in $Sections) {
  $safeName      = $section -replace ' ', '_'
  $beforeSection = Join-Path $TempDirectory "before.${safeName}.txt"
  $afterSection  = Join-Path $TempDirectory "after.${safeName}.txt"

  $beforeSlice = Get-CodeownersSection -Lines $beforeLines -SectionName $section
  if ($null -eq $beforeSlice) {
    LogError "Section '$section' not found in before file."
    exit 1
  }

  $afterSlice = Get-CodeownersSection -Lines $afterLines -SectionName $section
  if ($null -eq $afterSlice) {
    LogError "Section '$section' not found in after file."
    exit 1
  }

  Set-Content -Path $beforeSection -Value $beforeSlice
  Set-Content -Path $afterSection -Value $afterSlice

  $beforeContent = $beforeSlice -join "`n"
  $afterContent  = $afterSlice -join "`n"

  if ($beforeContent -ne $afterContent) {
    LogError "Protected CODEOWNERS section '$section' has been modified. Changes to this section are not allowed through normal PRs. To update CODEOWNERS, follow instructions at https://aka.ms/azsdk/codeowners"
    Write-Host "--- Diff for section '$section' ---"
    Write-Host ""
    git diff --no-index -- $beforeSection $afterSection
    $failed = $true
  } else {
    Write-Host "Section '$section' is unchanged."
  }
}

# ---------------------------------------------------------------------------
# 5. Exit
# ---------------------------------------------------------------------------
if ($failed) {
  $sectionList = ($Sections | ForEach-Object { "'$_'" }) -join ", "

  Write-Host ""
  LogError "##vso[task.LogIssue type=error;]One or more protected CODEOWNERS sections have been modified. Please revert changes to the $sectionList sections."
  exit 1
}

Write-Host "All protected CODEOWNERS sections are unchanged. Check passed."
exit 0
