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
    
    # sort by name - empty names at bottom
    $mcpTools = $tools | Sort-Object -Property @{Expression = {-not [string]::IsNullOrEmpty($_.mcpToolName)}; Descending = $true}, mcpToolName
    
    # Generate markdown content
    $markdown = @"
# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools and commands supported by the Azure SDK MCP server version $version.

## Tools list

| Name | Command | Description |
|------|---------|-------------|

"@
    
    foreach ($tool in $mcpTools) {
        $name = $tool.mcpToolName
        $command = [string]::IsNullOrEmpty($tool.commandLine) ? "" : "``$($tool.commandLine)``"
        $description = $tool.description -replace '\|', '\|'
        
        $markdown += "| $name | $command | $description |`n"
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
    $outputPath = Join-Path (Get-Location) "tools/azsdk-cli/docs/mcp-tools.md"
    
    Generate-McpToolsMarkdown -tools $tools -version $version -outputPath $outputPath
    
    Write-Host "Updated MCP tools documentation: $outputPath"
}
catch {
    Write-Error "Failed to update MCP tools documentation: $_"
    exit 1
}
