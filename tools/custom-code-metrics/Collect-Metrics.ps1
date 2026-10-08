#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet("dotnet", "java", "js", "python", "go", "rust", "cpp")][string]$Language = "dotnet",
    [Parameter(Mandatory)][string]$RepoRoot,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
if ($Language -ne "dotnet") {
    throw "Metrics collection for '$Language' is not implemented. No observation was produced."
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$schema = Join-Path $RepoRoot "eng" "scripts" "CustomCodeMetrics.schema.json"
& node (Join-Path $PSScriptRoot "schema.mjs") check-copy $schema
if ($LASTEXITCODE -ne 0) { throw "The .NET schema mirror differs from the canonical tools contract." }

Push-Location $RepoRoot
try {
    $result = Invoke-Pester -Path (Join-Path $RepoRoot "eng" "scripts" "tests" "Collect-CustomCodeMetrics.Tests.ps1") -PassThru
    if ($result.FailedCount -gt 0 -or $result.TotalCount -eq 0) { throw ".NET collector tests failed or none were discovered." }

    $snapshots = @(& (Join-Path $RepoRoot "eng" "scripts" "Collect-CustomCodeMetrics.ps1") -RepoRoot $RepoRoot -OutputDirectory $OutputDirectory)
    if ($snapshots.Count -ne 1 -or -not $snapshots[0] -or -not (Test-Path -LiteralPath $snapshots[0] -PathType Leaf)) {
        throw ".NET collection did not produce exactly one complete snapshot."
    }
    return $snapshots[0]
}
finally {
    Pop-Location
}
