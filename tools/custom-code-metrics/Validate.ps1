#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
Push-Location $PSScriptRoot
try {
    foreach ($command in @("check", "test", "test:dashboard:browser", "test:dashboard:multirepo")) {
        & npm run $command
        if ($LASTEXITCODE -ne 0) { throw "Custom code metrics validation failed: npm run $command." }
    }
    $result = Invoke-Pester -Path (Join-Path $PSScriptRoot "tests") -PassThru
    if ($result.FailedCount -gt 0 -or $result.TotalCount -eq 0) { throw "Custom code metrics PowerShell tests failed or none were discovered." }
    & npm run build:dashboard
    if ($LASTEXITCODE -ne 0) { throw "Could not reset the dashboard to a clean unseeded build after testing." }
}
finally {
    Pop-Location
}
