<#
.SYNOPSIS
Used to retrieve a list of references to test recordings that that "contain" a string in their contents.

.DESCRIPTION
Uses the Azure.Sdk.Tools.Assets.MaintenanceTool to retrieve a copy of all the assets files, then grep each tag's contents
for the specified string. Intermediary results are stored in the .results folder, and the final ressults will be output to the
console on successful completion of the script.

The output will be a list of links directly to individual files on github that match the search string.

This will enable easy access to the test recordings that are affected by a change in the SDK, and will help to identify
which tests need to be updated.

.PARAMETER ConfigFilePath
The query configuration file, which contains targeted repos, branches, and paths.
#>

param(
    [string]$ConfigFilePath
)

Set-StrictMode -Version 4
. $PSScriptRoot\utilities.ps1

if (!(Test-Path $ConfigFilePath -PathType Leaf)) {
    Write-Error "Config file not found: $ConfigFilePath"
    exit 1
}

$ResultsFolder = Create-If-Not-Exists "$PSScriptRoot\.results"

try {
    Push-Location $ResultsFolder
    Azure.Sdk.Tools.Assets.MaintenanceTool scan --config $ConfigFilePath
}
finally {
    Pop-Location
}

