<#
.SYNOPSIS
    Updates the MCP tools documentation by extracting tool names and descriptions from the Azure SDK CLI.

.DESCRIPTION
    This script runs the Azure SDK CLI to list all available MCP tools and generates
    a markdown documentation file (mcp-tools.md) containing the tool names and descriptions
    in a table format.

.PARAMETER AzsdkExePath
    Path to the Azure SDK CLI executable (azsdk).

.EXAMPLE
    ./update-mcp-tools-docs.ps1 -AzsdkExePath "./azsdk"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$AzsdkExePath
)

$ErrorActionPreference = "Stop"

function Get-McpVersion {
    param([string]$exePath)
    
    if (-not (Test-Path $exePath)) {
        throw "Azure SDK CLI executable not found at: $exePath"
    }
    
    $versionOutput = & $exePath --version 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run azsdk --version command. Exit code: $LASTEXITCODE"
    }
    
    # Remove the +sha part from version (e.g., "0.5.9+adef6a4419984a5852e4118d05f695b90d007044" -> "0.5.9")
    $version = $versionOutput.Trim() -replace '\+.*$', ''
    
    return $version
}

function Get-McpToolsList {
    param([string]$exePath)
    
    if (-not (Test-Path $exePath)) {
        throw "Azure SDK CLI executable not found at: $exePath"
    }
    
    $jsonOutput = & $exePath list --output json 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run azsdk list command. Exit code: $LASTEXITCODE"
    }
    
    $toolsData = $jsonOutput | ConvertFrom-Json
    
    if (-not $toolsData.tools) {
        throw "Unexpected JSON structure. Expected 'tools' property."
    }
    
    return $toolsData.tools
}

function Generate-McpToolsMarkdown {
    param(
        [array]$tools,
        [string]$version,
        [string]$outputPath
    )
    
    # sort by name
    $mcpTools = $tools | Sort-Object -Property commandLine
    
    # Generate markdown content
    $markdown = @"
# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tool commands supported by the Azure SDK MCP server version $version.

## Tools list

| Command | Description |
|---------|-------------|

"@
    
    foreach ($tool in $mcpTools) {
        $command = $tool.commandLine
        $description = $tool.description

        $description = $description -replace '\|', '\|'

        # Skip commands (not tools)
        if ([string]::IsNullOrEmpty($command)) {
            continue
        }
        
        $markdown += "| $command | $description |`n"
    }

    # Write to file
    $outputDir = Split-Path -Parent $outputPath
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    Set-Content -Path $outputPath -Value $markdown -Encoding UTF8
}

# Main script execution
try {
    $version = Get-McpVersion -exePath $AzsdkExePath
    $tools = Get-McpToolsList -exePath $AzsdkExePath
    $outputPath = Join-Path (Get-Location) "tools/azsdk-cli/docs/mcp-commands.md"
    
    Generate-McpToolsMarkdown -tools $tools -version $version -outputPath $outputPath
    
    Write-Host "Updated MCP tools documentation: $outputPath"
}
catch {
    Write-Error "Failed to update MCP tools documentation: $_"
    exit 1
}
