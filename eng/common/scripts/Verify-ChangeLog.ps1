# Wrapper Script for ChangeLog Verification
param (
  [String]$ChangeLogLocation,
  [String]$VersionString,
  [string]$PackageName,
  [string]$ServiceName,
  [string]$RepoRoot,
  [ValidateSet("net", "java", "js", "python")]
  [string]$Language,
  [ValidateSet("True", "False", "true", "false")]
  [string]$ForRelease = 'false'
)

$ProgressPreference = "SilentlyContinue"
. (Join-Path $PSScriptRoot SemVer.ps1)
Import-Module (Join-Path $PSScriptRoot modules ChangeLog-Operations.psm1)

[Boolean]$forRelease = [System.Convert]::ToBoolean($ForRelease)

$validChangeLog = $false
if ($ChangeLogLocation -and $VersionString) 
{
  $validChangeLog = Confirm-ChangeLogEntry -ChangeLogLocation $ChangeLogLocation -VersionString $VersionString -ForRelease $forRelease
}
else
{
  Import-Module (Join-Path $PSScriptRoot modules Package-Properties.psm1)
 
  $PackageProp = Get-PkgProperties -PackageName $PackageName -ServiceName $ServiceName -Language $Language -RepoRoot $RepoRoot
  $validChangeLog = Confirm-ChangeLogEntry -ChangeLogLocation $PackageProp.pkgChangeLogPath -VersionString $PackageProp.pkgVersion -ForRelease $forRelease
}

if (!$validChangeLog)
{
  exit 1
}

exit 0
