# Note: This script will add or replace version title in change log
param (
  [Parameter(Mandatory = $true)]
  [String]$Version,
  [Parameter(Mandatory = $true)]
  [String]$ChangeLogPath,
  [String]$Unreleased = $True,
  [String]$ReplaceVersion = $False
)

# Parameter description
# Version : Version to add or replace in change log
# ChangeLogPath: Path to change log file. If change log path is set to directory then script will probe for change log file in that path 
# Unreleased: Default is true. If it is set to false, then today's date will be set in verion title. If it is True then title will show "Unreleased"
# ReplaceVersion: This is useful when replacing current version title with new title.( Helpful to update the title before package release)


function Get-ChangelogPath
{
  Param (
        [String]$Path
   )
  
   # Check if CHANGELOG.md is present in path 
   $ChangeLogPath = Join-Path -Path $Path -ChildPath "CHANGELOG.md"
   if ((Test-Path -Path $ChangeLogPath) -eq $False){
      # Check if change log exists with name HISTORY.md
      $ChangeLogPath = Join-Path -Path $Path -ChildPath "HISTORY.md"
      if ((Test-Path -Path $ChangeLogPath) -eq $False){
         Write-Host "Change log is not found in path[$Path]"
         exit(1)
      }
   }
  
   Write-Host "Change log is found at path [$ChangeLogPath]"
   return $ChangeLogPath
}


function Get-VersionTitle
{
   Param (
        [String]$Version,
        [String]$Unreleased
   )
   # Generate version title
   $newVersionTitle = "## $Version Unreleased" -join [Environment]::NewLine
   if ($Unreleased -eq $False){
      $releaseDate = Get-Date -Format "(yyyy-MM-dd)"
      $newVersionTitle = "## $Version $releaseDate" -join [Environment]::NewLine      
   }
   return $newVersionTitle
}


function Get-NewChangeLog
{
   Param (
        [System.Collections.ArrayList]$ChangelogLines,
        [String]$Version,
        [String]$Unreleased,
        [String]$ReplaceVersion
   ) 

   # Version Parser module
   Import-Module $PSScriptRoot/Version-Parser.psm1

   # version parameter is to pass new version to add or replace
   # Unreleased parameter can be set to False to set today's date instead of "Unreleased in title"
   # ReplaceVersion param can be set to true to replace current version title( useful at release time to change title)

   # find index of current version
   $Index = 0
   $CurrentTitle = ""
   for(; $Index -lt $ChangelogLines.Count; $Index++){
      if (Version-Matches($ChangelogLines[$Index])){
         $CurrentTitle = $ChangelogLines[$Index]
         Write-Host "Current Version title: $CurrentTitle"
         break
      }
   }

   # Generate version title
   $newVersionTitle = Get-VersionTitle -Version $Version -Unreleased $Unreleased

   # if version is already found and not replacing then nothing to do
   if ($ReplaceVersion -eq $False){
      # Check if version is already present in log      
      if ($CurrentTitle.Contains($Version)){
         Write-Host "Version is already present in change log. Please set ReplaceVersion parameter if current title needs to be updated"
         exit(1)
      }
      
      Write-Host "Adding version title $newVersionTitle"
      $ChangelogLines.insert($Index, $newVersionTitle)
   }
   else{
      # Script is executed to replace an existing version title
      Write-Host "Replacing current version title to $newVersionTitle"
      $ChangelogLines[$index] = $newVersionTitle
   }

   return $ChangelogLines      
}


# Make sure path is valid
if ((Test-Path -Path $ChangeLogPath) -eq $False){
   Write-Host "Change log path is invalid. [$ChangeLogPath]"
   exit(1)
}

# probe change log path if path is directory 
if (Test-Path -Path $ChangeLogPath -PathType Container)
{   
   $ChangeLogPath = Get-ChangelogPath -Path $ChangeLogPath
}

# Read current change logs and add/update version
$ChangelogLines = [System.Collections.ArrayList](Get-Content -Path $ChangeLogPath)
$NewContents = Get-NewChangeLog -ChangelogLines $ChangelogLines -Version $Version -Unreleased $Unreleased -ReplaceVersion $ReplaceVersion

Write-Host "Writing change log to file [$ChangeLogPath]"
Set-Content -Path $ChangeLogPath $NewContents
Write-Host "Version is added/updated in change log"
