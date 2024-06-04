

<#
.SYNOPSIS
# Retrieve a copy of all the assets files related to a folder in one or multiple repos
# and dumps the results into a folder .results alongside the script.

.PARAMETER ConfigFilePath
The query configuration file, which contains targeted repos, branches, and paths.
#>

param(
    [string]$ConfigFilePath
)

Set-StrictMode -Version 4

if (Test-Path $ConfigFilePath -PathType Leaf) {
    $Config = Get-Content $ConfigFilePath | ConvertFrom-Json
} else {
    Write-Error "Config file not found: $ConfigFilePath"
    exit 1
}

Write-Host ($Config | ConvertTo-Json)