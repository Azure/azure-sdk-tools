# given a CHANGELOG.md file, extract the relevant info we need to decorate a release
param (
  [Parameter(Mandatory = $true)]
  [String]$ChangeLogLocation
)

Import-Module "${PSScriptRoot}/modules/ChangeLog-Operations.psm1"
return Get-ReleaseNotes -ChangeLogLocation $ChangeLogLocation
