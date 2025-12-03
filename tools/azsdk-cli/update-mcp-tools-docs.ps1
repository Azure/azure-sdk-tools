<#
.SYNOPSIS
    Updates the MCP tools documentation by extracting tool names and descriptions from the Azure SDK CLI.

.DESCRIPTION
    This script runs the Azure SDK CLI to list all available MCP tools and generates
    a markdown documentation file (mcp-tools.md) containing the tool names and descriptions
    in a table format.

.PARAMETER AzsdkCliPath
    Path to the Azure SDK CLI executable (azsdk).

.PARAMETER CsprojPath
    Path to the Azure.Sdk.Tools.Cli.csproj file to extract the version number.

.EXAMPLE
    ./update-mcp-tools-docs.ps1 -AzsdkCliPath "./artifacts/bin/Azure.Sdk.Tools.Cli/Release/net8.0/azsdk" -CsprojPath "./tools/azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$AzsdkCliPath,
    
    [Parameter(Mandatory=$true)]
    [string]$CsprojPath
)

$ErrorActionPreference = "Stop"

function Get-McpVersion {
    param([string]$azsdkPath)
    
    if (-not (Test-Path $azsdkPath)) {
        throw "Azure SDK CLI executable not found at: $azsdkPath"
    }
    
    $versionOutput = & $azsdkPath --version 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run azsdk --version command. Exit code: $LASTEXITCODE"
    }
    
    return $versionOutput.Trim()
}

function Get-McpToolsList {
    param([string]$azsdkPath)
    
    if (-not (Test-Path $azsdkPath)) {
        throw "Azure SDK CLI executable not found at: $azsdkPath"
    }
    
    $jsonOutput = & $azsdkPath list --output json 2>&1
    
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
    $mcpTools = $tools | Sort-Object -Property mcpToolName
    
    # Generate markdown content
    $markdown = @"
# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools supported by the Azure SDK MCP server version $version.

## Tools list

| Name | Command | Description |
|------|---------|-------------|

"@
    
    foreach ($tool in $mcpTools) {
        $name = $tool.mcpToolName
        $command = $tool.commandLine
        $description = $tool.description

        $description = $description -replace '\|', '\|'
        
        # Skip commands (not tools)
        if ([string]::IsNullOrEmpty($name)) {
            continue
        }
        
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
    $version = Get-McpVersion -azsdkPath $AzsdkCliPath
    $tools = Get-McpToolsList -azsdkPath $AzsdkCliPath
    $outputPath = Join-Path (Get-Location) "tools/azsdk-cli/docs/mcp-tools.md"
    
    Generate-McpToolsMarkdown -tools $tools -version $version -outputPath $outputPath
    
    Write-Host "Updated MCP tools documentation: $outputPath"
}
catch {
    Write-Error "Failed to update MCP tools documentation: $_"
    exit 1
}
