<#
.SYNOPSIS
Used to retrieve a list of references to test recordings that that "contain" a string in their contents.

.DESCRIPTION
Uses the Azure.Sdk.Tools.Assets.MaintenanceTool to retrieve a copy of all the assets files, then grep each tag's contents
for the specified string. Intermediary results are stored in the .results folder, and the final ressults will be output to the
console on successful completion of the script.

This will enable easy access to the test recordings that are affected by a change in the SDK, and will help to identify
which tests need to be updated.

PreReqs:
    - Azure.Sdk.Tools.Assets.MaintenanceTool must be available on the PATH
    - git is available on the PATH
    - Running powershell core
    - ripgrip installed on the machine. 'choco install ripgrep' on elevated terminal

.PARAMETER ConfigFilePath
The query configuration file, which contains targeted repos, branches, and paths.

.PARAMETER SearchString
The regex string to search for in the contents of the test recordings.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigFilePath,
    [Parameter(Mandatory = $true)]
    [string]$SearchString
)

Set-StrictMode -Version 4
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot ".." "utilities.ps1")

if (!(Test-Path $ConfigFilePath -PathType Leaf)) {
    Write-Error "Config file not found: $ConfigFilePath"
    exit 1
}

$ResultsFolder = Create-If-Not-Exists (Join-Path $PSScriptRoot ".results")
$TagsFolder = Create-If-Not-Exists (Join-Path $ResultsFolder "tags")
$ScanOutputJson = Join-Path $ResultsFolder "output.json"

try {
    Push-Location $ResultsFolder

    if (!(Test-Path $ScanOutputJson)) {
        Azure.Sdk.Tools.Assets.MaintenanceTool scan --config $ConfigFilePath
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
    }
    else {
        Write-Host "Skipping scan, using cached results"
    }
}
finally {
    Pop-Location
}

$DiscoveredAssets = Get-Content $ScanOutputJson | ConvertFrom-Json 

foreach ($asset in $DiscoveredAssets) {
    $TagFolder = Get-AssetsRepoSlice -Tag $asset.Tag -WorkDirectory $ResultsFolder
}

Write-Host "rg $SearchString -g `"*.json`" $TagsFolder"
rg $SearchString -g "*.json" $TagsFolder
