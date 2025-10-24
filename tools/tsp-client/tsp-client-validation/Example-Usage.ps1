#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Example usage of the TSP-Client validation automation script.

.DESCRIPTION
    This script demonstrates how to use the Invoke-TspClientValidation.ps1 script
    with the current tsp-client update scenario (PR #12360).

.PARAMETER DryRun
    Run in dry-run mode to see what would be executed
#>

[CmdletBinding()]
param(
    [switch]$DryRun
)

# Current scenario data (from our analysis)
$PRNumber = 12360
$SyncBranch = "sync-eng/common-update-tsp-client-12360"

Write-Host "TSP-Client Validation Example - PR #$PRNumber" -ForegroundColor Yellow
Write-Host "=" * 50 -ForegroundColor Gray

Write-Host "`nThis example demonstrates validating the current tsp-client update:" -ForegroundColor White
Write-Host "  • PR: Azure/azure-sdk-tools#$PRNumber" -ForegroundColor Cyan
Write-Host "  • Version: 0.28.3 → 0.29.0" -ForegroundColor Cyan
Write-Host "  • Sync Branch: $SyncBranch" -ForegroundColor Cyan

Write-Host "`nSync PRs created:" -ForegroundColor White
$syncPRs = @(
    @{ Language = "C#"; Repository = "azure-sdk-for-net"; PR = "#53032" }
    @{ Language = "Java"; Repository = "azure-sdk-for-java"; PR = "#46913" }
    @{ Language = "JavaScript"; Repository = "azure-sdk-for-js"; PR = "#36123" }
    @{ Language = "Python"; Repository = "azure-sdk-for-python"; PR = "#43259" }
    @{ Language = "Go"; Repository = "azure-sdk-for-go"; PR = "#25362" }
)

foreach ($pr in $syncPRs) {
    Write-Host "  • $($pr.Language): $($pr.Repository) $($pr.PR)" -ForegroundColor Gray
}

if ($DryRun) {
    Write-Host "`n🔍 Running in DRY RUN mode..." -ForegroundColor Yellow
    $dryRunParam = "-DryRun"
} else {
    Write-Host "`n⚠️  This will trigger actual pipelines!" -ForegroundColor Red
    Write-Host "Press Enter to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
    Read-Host
    $dryRunParam = ""
}

# Example 1: Validate all languages
Write-Host "`n" + "=" * 50 -ForegroundColor Gray
Write-Host "EXAMPLE 1: Validate All Languages" -ForegroundColor Yellow
Write-Host "=" * 50 -ForegroundColor Gray

$command1 = ".\Invoke-TspClientValidation.ps1 -PRNumber $PRNumber -SyncBranch '$SyncBranch' $dryRunParam"
Write-Host "Command: $command1" -ForegroundColor Green

if (Test-Path ".\Invoke-TspClientValidation.ps1") {
    Invoke-Expression $command1
} else {
    Write-Host "Script not found. Run this from the tools/tsp-client-validation directory." -ForegroundColor Red
}

Write-Host "`n" + "=" * 50 -ForegroundColor Gray
Write-Host "EXAMPLE 2: Validate Specific Languages" -ForegroundColor Yellow
Write-Host "=" * 50 -ForegroundColor Gray

Write-Host "Example command for Python and .NET only:" -ForegroundColor White
$command2 = ".\Invoke-TspClientValidation.ps1 -PRNumber $PRNumber -SyncBranch '$SyncBranch' -Languages @('Python', 'NET') $dryRunParam"
Write-Host $command2 -ForegroundColor Green

Write-Host "`n" + "=" * 50 -ForegroundColor Gray
Write-Host "EXAMPLE 3: Monitor Existing Pipelines" -ForegroundColor Yellow
Write-Host "=" * 50 -ForegroundColor Gray

Write-Host "To monitor existing pipeline runs:" -ForegroundColor White
Write-Host ".\Invoke-TspClientValidation.ps1 -MonitorOnly" -ForegroundColor Green
Write-Host "Then enter build IDs like: 5487215,5487216,5487217" -ForegroundColor Gray

Write-Host "`n✅ Example completed!" -ForegroundColor Green