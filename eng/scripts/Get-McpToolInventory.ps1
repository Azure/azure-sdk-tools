<#
.SYNOPSIS
    Verify the Azure.Sdk.Tools.Mock handler set matches the live
    Azure.Sdk.Tools.Cli MCP tool list.

.DESCRIPTION
    Builds the CLI + Mock projects (incremental), queries the live tool list,
    enumerates IMockToolHandler implementations, and prints three buckets:

        both       - tool exists on both sides (no action).
        live-only  - live tool with no mock handler (add one).
        mock-only  - mock handler with no live tool (delete or rename).

    Exits non-zero on any drift. Same command for local dev and CI.

.EXAMPLE
    pwsh eng/scripts/Get-McpToolInventory.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$repoRoot    = Resolve-Path (Join-Path $PSScriptRoot '../..')
$cliProject  = Join-Path $repoRoot 'tools/azsdk-cli/Azure.Sdk.Tools.Cli'
$mockProject = Join-Path $repoRoot 'tools/azsdk-cli/Azure.Sdk.Tools.Mock'

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

    # Parse `public string ToolName => "azsdk_xxx";` from handler source files.
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

Invoke-DotnetBuild -Project $cliProject
Invoke-DotnetBuild -Project $mockProject

$liveTools = @(Get-LiveMcpTools         -CliProject  $cliProject)
$mockTools = @(Get-MockHandlerToolNames -MockProject $mockProject)

$liveSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$liveTools, [System.StringComparer]::OrdinalIgnoreCase)
$mockSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$mockTools, [System.StringComparer]::OrdinalIgnoreCase)

$liveOnly = @($liveTools | Where-Object { -not $mockSet.Contains($_) })
$mockOnly = @($mockTools | Where-Object { -not $liveSet.Contains($_) })
$both     = @($liveTools | Where-Object {       $mockSet.Contains($_) })

Write-Host "MCP tool inventory" -ForegroundColor White
Write-Host "  live tools:    $($liveTools.Count)"
Write-Host "  mock handlers: $($mockTools.Count)"

Write-Section -Title 'both (live + mock handler)'  -Items $both     -Color Green
Write-Section -Title 'live-only (no mock handler)' -Items $liveOnly -Color Yellow
Write-Section -Title 'mock-only (stale handler)'   -Items $mockOnly -Color Magenta

if ($liveOnly.Count -gt 0 -or $mockOnly.Count -gt 0) {
    Write-Host ""
    if ($liveOnly.Count -gt 0) {
        Write-Host "Drift: $($liveOnly.Count) live tool(s) have no mock handler. Add one under tools/azsdk-cli/Azure.Sdk.Tools.Mock/Handlers/." -ForegroundColor Red
    }
    if ($mockOnly.Count -gt 0) {
        Write-Host "Drift: $($mockOnly.Count) mock handler(s) target tools that no longer exist. Delete or rename them." -ForegroundColor Red
    }
    Write-Host "See tools/azsdk-cli/Azure.Sdk.Tools.Mock/README.md for the contract." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "OK - mock handler set matches the live MCP tool list." -ForegroundColor Green
