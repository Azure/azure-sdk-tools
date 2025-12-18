#!/usr/bin/env pwsh
# Simple script to run the MCP labeler accuracy test

Write-Host "=== MCP Labeler Accuracy Test ===" -ForegroundColor Cyan
Write-Host ""

# Navigate to the test directory
Set-Location $PSScriptRoot

# Run the test
dotnet run

Write-Host ""
Write-Host "Test complete. Check mcp_evaluation_results.csv for detailed results." -ForegroundColor Green
