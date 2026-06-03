<#
.SYNOPSIS
    Inventory drift report between the live Azure.Sdk.Tools.Cli MCP server and
    the Azure.Sdk.Tools.Mock handler set.

.DESCRIPTION
    Boots the live MCP server, captures its tool list, then enumerates the
    IMockToolHandler implementations in Azure.Sdk.Tools.Mock and emits a diff:

        live-only  - tool exists in the live server, no mock handler -> mock
                     returns the generic default response (potential gap).
        mock-only  - mock handler exists, tool no longer in live server
                     (stale handler).
        both       - tool exists on both sides.

    Also collects the set of MCP tools referenced by mock-tier evals
    (tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/{unit,integration,e2e}/*.eval.yaml)
    so -CheckOnly can fail the build only when drift affects something an eval
    actually relies on.

.PARAMETER CheckOnly
    Exit non-zero when there is drift on any tool referenced by a mock-tier eval.
    Intended for CI: see https://github.com/Azure/azure-sdk-tools/issues/15829.

.PARAMETER OutputJson
    Optional path to write the diff as JSON for downstream tooling.

.PARAMETER SkipBuild
    Skip `dotnet build` for the CLI and Mock projects (assumes they are up to date).

.EXAMPLE
    pwsh eng/scripts/Get-McpToolInventory.ps1

.EXAMPLE
    pwsh eng/scripts/Get-McpToolInventory.ps1 -CheckOnly
#>
[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [string]$OutputJson,
    [switch]$SkipBuild
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '../..')
$cliProject = Join-Path $repoRoot 'tools/azsdk-cli/Azure.Sdk.Tools.Cli'
$mockProject = Join-Path $repoRoot 'tools/azsdk-cli/Azure.Sdk.Tools.Mock'
$evalsRoot  = Join-Path $repoRoot 'tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals'

if (-not (Test-Path $cliProject))  { throw "CLI project not found: $cliProject" }
if (-not (Test-Path $mockProject)) { throw "Mock project not found: $mockProject" }

function Invoke-DotnetBuild {
    param([string]$Project)
    Write-Host "Building $Project ..." -ForegroundColor DarkGray
    & dotnet build $Project --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $Project (exit $LASTEXITCODE)"
    }
}

function Get-LiveMcpTools {
    param([string]$CliProject)

    Write-Host "Querying live MCP tool list via 'azsdk list -o json' ..." -ForegroundColor DarkGray
    $json = & dotnet run --project $CliProject --no-build -- list -o json
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run 'list -o json' on $CliProject (exit $LASTEXITCODE)"
    }

    $parsed = $json | ConvertFrom-Json
    $names = @()
    foreach ($t in $parsed.Tools) {
        if ($t.McpToolName) {
            $names += [string]$t.McpToolName
        }
    }
    return $names | Sort-Object -Unique
}

function Get-MockHandlerToolNames {
    param([string]$MockProject)

    # The README documents the handler contract as
    #     public string ToolName => "azsdk_xxx";
    # Parsing source is robust and avoids loading the assembly + all its
    # dependencies into the PowerShell process.
    $pattern = [regex]'(?m)ToolName\s*=>\s*"([^"]+)"'
    $names = @()
    Get-ChildItem -LiteralPath (Join-Path $MockProject 'Handlers') -Recurse -Filter *.cs |
        ForEach-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            foreach ($m in $pattern.Matches($text)) {
                $names += $m.Groups[1].Value
            }
        }
    return $names | Sort-Object -Unique
}

function Get-EvalReferencedTools {
    param([string]$EvalsRoot)

    if (-not (Test-Path $EvalsRoot)) {
        return @()
    }
    $pattern = [regex]'azsdk_[A-Za-z0-9_]+'
    $names = @()
    Get-ChildItem -LiteralPath $EvalsRoot -Recurse -Filter *.yaml |
        ForEach-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            foreach ($m in $pattern.Matches($text)) {
                $names += $m.Value
            }
        }
    return $names | Sort-Object -Unique
}

function Write-Section {
    param([string]$Title, [string[]]$Items, [ConsoleColor]$Color = [ConsoleColor]::Gray)
    Write-Host ""
    Write-Host "== $Title ($($Items.Count)) ==" -ForegroundColor $Color
    if ($Items.Count -eq 0) {
        Write-Host "  (none)" -ForegroundColor DarkGray
    }
    else {
        $Items | ForEach-Object { Write-Host "  $_" -ForegroundColor $Color }
    }
}

if (-not $SkipBuild) {
    Invoke-DotnetBuild -Project $cliProject
    Invoke-DotnetBuild -Project $mockProject
}

$liveTools  = @(Get-LiveMcpTools         -CliProject  $cliProject)
$mockTools  = @(Get-MockHandlerToolNames -MockProject $mockProject)
$evalTools  = @(Get-EvalReferencedTools  -EvalsRoot   $evalsRoot)

$liveSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$liveTools, [System.StringComparer]::OrdinalIgnoreCase)
$mockSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$mockTools, [System.StringComparer]::OrdinalIgnoreCase)
$evalSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$evalTools, [System.StringComparer]::OrdinalIgnoreCase)

$liveOnly = @($liveTools | Where-Object { -not $mockSet.Contains($_) })
$mockOnly = @($mockTools | Where-Object { -not $liveSet.Contains($_) })
$both     = @($liveTools | Where-Object {       $mockSet.Contains($_) })

# Mock-tier eval gaps: tool referenced by an eval, exists live, but no mock handler.
$evalGaps = @($liveOnly | Where-Object { $evalSet.Contains($_) })

Write-Host "MCP tool inventory" -ForegroundColor White
Write-Host "  live tools:   $($liveTools.Count)"
Write-Host "  mock handlers: $($mockTools.Count)"
Write-Host "  eval refs:    $($evalTools.Count)"

Write-Section -Title 'both (live + mock handler)' -Items $both     -Color Green
Write-Section -Title 'live-only (no mock handler)' -Items $liveOnly -Color Yellow
Write-Section -Title 'mock-only (stale handler)'   -Items $mockOnly -Color Magenta
Write-Section -Title 'mock-tier eval gaps (require handler)' -Items $evalGaps -Color Red

if ($OutputJson) {
    $payload = [ordered]@{
        liveTools = $liveTools
        mockTools = $mockTools
        evalTools = $evalTools
        both      = $both
        liveOnly  = $liveOnly
        mockOnly  = $mockOnly
        evalGaps  = $evalGaps
    }
    $payload | ConvertTo-Json -Depth 4 | Out-File -LiteralPath $OutputJson -Encoding utf8
    Write-Host "Wrote $OutputJson" -ForegroundColor DarkGray
}

if ($CheckOnly) {
    $fail = $false
    if ($mockOnly.Count -gt 0) {
        Write-Host ""
        Write-Host "Drift detected: $($mockOnly.Count) mock handler(s) target tools that no longer exist in the live MCP server." -ForegroundColor Red
        Write-Host "Either delete the stale handler(s) or rename them to match the new live tool name." -ForegroundColor Red
        $fail = $true
    }
    if ($evalGaps.Count -gt 0) {
        Write-Host ""
        Write-Host "Drift detected: $($evalGaps.Count) mock-tier eval tool(s) lack a mock handler." -ForegroundColor Red
        Write-Host "Add a handler under tools/azsdk-cli/Azure.Sdk.Tools.Mock/Handlers/ for each tool above." -ForegroundColor Red
        Write-Host "See tools/azsdk-cli/Azure.Sdk.Tools.Mock/README.md for the contract." -ForegroundColor Red
        $fail = $true
    }
    if ($fail) {
        exit 1
    }
    Write-Host ""
    Write-Host "OK - no stale handlers, no mock-tier eval gaps." -ForegroundColor Green
}

return
