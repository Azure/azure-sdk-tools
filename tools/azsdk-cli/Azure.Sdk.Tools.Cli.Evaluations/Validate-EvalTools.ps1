<#
.SYNOPSIS
    Validates that all tool names referenced in trigger.eval.yaml files exist in the MCP server.

.DESCRIPTION
    This script:
    1. Runs `azsdk list` to get all registered MCP tool names from the server.
    2. Parses all trigger.eval.yaml files under the Evaluations subdirectories.
    3. Reports any eval tool references that don't exist on the server,
       and any server tools that are missing eval coverage.

.PARAMETER ProjectPath
    Path to the Azure.Sdk.Tools.Cli project. Defaults to the project relative to this script.

.PARAMETER EvalPath
    Path to the Evaluations directory containing trigger.eval.yaml files.
    Defaults to the Evaluations directory relative to this script.

.PARAMETER SkipBuild
    If set, passes --no-build to dotnet run (requires a prior build).
#>
[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EvalPath,
    [switch]$SkipBuild
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

if (-not $ProjectPath) {
    $ProjectPath = Join-Path $repoRoot "Azure.Sdk.Tools.Cli"
}
if (-not $EvalPath) {
    $EvalPath = Join-Path $repoRoot "Azure.Sdk.Tools.Cli.Evaluations"
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "CLI project not found at: $ProjectPath"
    return 1
}
if (-not (Test-Path $EvalPath)) {
    Write-Error "Evaluations directory not found at: $EvalPath"
    return 1
}

# Step 1: Get tool names from the MCP server via `azsdk list`
Write-Host "Running 'azsdk list' to get registered MCP tools..." -ForegroundColor Cyan

$dotnetArgs = @("run", "--project", $ProjectPath)
if ($SkipBuild) {
    $dotnetArgs += "--no-build"
}
$dotnetArgs += @("--", "list", "--output", "json")

$listOutput = & dotnet @dotnetArgs 2>&1
$jsonLines = $listOutput | Where-Object { $_ -is [string] -and $_ -notmatch "^Using launch settings" }
$jsonText = $jsonLines -join "`n"

try {
    $parsed = $jsonText | ConvertFrom-Json
    [string[]]$serverTools = @($parsed.Tools | ForEach-Object { $_.McpToolName } | Where-Object { $_ } | Sort-Object -Unique)
} catch {
    Write-Error "Failed to parse 'azsdk list --output json'. Error: $_"
    return 1
}

# Filter out tools that are excluded from eval coverage (example, test, and utility tools)
$excludedTools = @(
    "azsdk_hello_world",
    "azsdk_hello_world_fail",
    "azsdk_example_process_execution",
    "azsdk_example_powershell_execution",
    "azsdk_example_azure_service",
    "azsdk_example_ai_service",
    "azsdk_example_error_handling",
    "azsdk_example_agent_fibonacci",
    "azsdk_example_github_service",
    "azsdk_example_devops_service",
    "azsdk_upgrade",
    "azsdk_engsys_codeowner_view",
    "azsdk_engsys_codeowner_add_label_owner",
    "azsdk_engsys_codeowner_remove_label_owner",
    "azsdk_engsys_codeowner_add_package_owner",
    "azsdk_engsys_codeowner_remove_package_owner",
    "azsdk_engsys_codeowner_add_package_label",
    "azsdk_engsys_codeowner_remove_package_label"
)

[string[]]$serverTools = @($serverTools | Where-Object { $_ -notin $excludedTools })

if ($serverTools.Count -eq 0) {
    Write-Error "No tools found from 'azsdk list'. Check that the CLI project builds and runs correctly."
    return 1
}

Write-Host "Found $($serverTools.Count) tools registered on the MCP server ($($excludedTools.Count) excluded).`n" -ForegroundColor Green

# Step 2: Parse all trigger.eval.yaml files for tool name references
$evalFiles = Get-ChildItem -Path $EvalPath -Recurse -Filter "trigger.eval.yaml" |
    Where-Object { $_.DirectoryName -ne $EvalPath }

if ($evalFiles.Count -eq 0) {
    Write-Error "No trigger.eval.yaml files found in subdirectories of: $EvalPath"
    return 1
}

$evalToolsByFile = @{}
$allEvalTools = [System.Collections.Generic.HashSet[string]]::new()

foreach ($file in $evalFiles) {
    $folder = Split-Path $file.DirectoryName -Leaf
    $matchResults = Select-String -Path $file.FullName -Pattern 'name:\s*"azure-sdk-mcp-([^"]+)"'
    [string[]]$tools = @($matchResults | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique)
    $evalToolsByFile[$folder] = $tools
    foreach ($t in $tools) {
        [void]$allEvalTools.Add($t)
    }
}

Write-Host "Found $($allEvalTools.Count) unique tools across $($evalFiles.Count) eval files.`n" -ForegroundColor Green

# Step 3: Compare
[string[]]$missingFromServer = @($allEvalTools | Where-Object { $_ -notin $serverTools } | Sort-Object)
[string[]]$missingFromEvals = @($serverTools | Where-Object { $_ -notin $allEvalTools } | Sort-Object)

$hasErrors = $false

if ($missingFromServer.Count -gt 0) {
    $hasErrors = $true
    Write-Host "ERROR: Eval references tools NOT found on the MCP server:" -ForegroundColor Red
    foreach ($tool in $missingFromServer) {
        # Find which eval file references it
        $sources = $evalToolsByFile.GetEnumerator() | Where-Object { $_.Value -contains $tool } | ForEach-Object { $_.Key }
        Write-Host "  - $tool (referenced in: $($sources -join ', '))" -ForegroundColor Red
    }
    Write-Host ""
}

if ($missingFromEvals.Count -gt 0) {
    $hasErrors = $true
    Write-Host "ERROR: Server tools with no eval coverage:" -ForegroundColor Red
    foreach ($tool in $missingFromEvals) {
        Write-Host "  - $tool" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host ""
if ($hasErrors) {
    Write-Host "RESULT: FAIL - Eval tools and MCP server tools are out of sync." -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULT: PASS - All eval tools exist on the MCP server." -ForegroundColor Green
    exit 0
}
