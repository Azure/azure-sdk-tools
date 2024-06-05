<#
.SYNOPSIS
Retrieve a copy of all the assets files related to a folder in one or multiple repos
and dumps the results into a folder .results alongside the script.

.DESCRIPTION
Uses the Azure.Sdk.Tools.Assets.MaintenanceTool to retrieve a copy of all the assets files related to a folder in one or multiple repos.

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