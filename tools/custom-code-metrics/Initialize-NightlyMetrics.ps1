#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][ValidatePattern("^[0-9a-f]{40,64}$")][string]$ExpectedNetCommit,
    [Parameter(Mandatory)][ValidatePattern("^[0-9a-f]{40,64}$")][string]$ExpectedToolsCommit
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$toolsRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." "..")).Path

function Assert-Checkout {
    param([string]$Root, [string]$ExpectedCommit)
    $actual = & git -C $Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actual -cne $ExpectedCommit) {
        throw "Checkout does not match its resolved revision: $Root."
    }
    $status = & git -C $Root status --porcelain --untracked-files=no
    if ($LASTEXITCODE -ne 0 -or $status) { throw "A clean tracked checkout is required: $Root." }
    Write-Host "Verified metrics checkout: $Root at $actual"
}

Assert-Checkout -Root $toolsRoot -ExpectedCommit $ExpectedToolsCommit
Assert-Checkout -Root $RepoRoot -ExpectedCommit $ExpectedNetCommit
$configuration = Get-Content -LiteralPath (Join-Path $RepoRoot "global.json") -Raw | ConvertFrom-Json -AsHashtable
if (-not $configuration.ContainsKey("sdk") -or $configuration["sdk"] -isnot [System.Collections.IDictionary] -or
    $configuration["sdk"]["version"] -notmatch "^[0-9]+\.[0-9]+\.[0-9]+$") {
    throw "Missing or invalid exact .NET SDK version in global.json."
}
$version = $configuration["sdk"]["version"]
Write-Host "##vso[task.setvariable variable=MetricsDotNetSdkVersion]$version"
