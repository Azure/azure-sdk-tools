#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$false)]
    [string]$AzsdkCliPath = "$PSScriptRoot/../../artifacts/bin/Azure.Sdk.Tools.Cli/Release/net8.0/azsdk",
    
    [Parameter(Mandatory=$false)]
    [string]$CsprojPath = "$PSScriptRoot/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj"
)

function Start-McpServer {
    param([string]$ExePath)
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ExePath
    $psi.Arguments = "start"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    
    Start-Sleep -Seconds 3
    
    if ($process.HasExited) {
        throw "MCP server exited with code: $($process.ExitCode)"
    }
    
    return $process
}

function Get-McpTools {
    param([System.Diagnostics.Process]$Process)
    
    $request = @{
        jsonrpc = "2.0"
        id = 1
        method = "tools/list"
        params = @{}
    } | ConvertTo-Json -Compress
    
    $Process.StandardInput.WriteLine($request)
    $Process.StandardInput.Flush()
    
    Start-Sleep -Seconds 2
    $response = $Process.StandardOutput.ReadLine()
    
    if (-not $response -or $response -notmatch '^\{.*"jsonrpc".*\}$') {
        throw "No valid JSON-RPC response received"
    }
    
    $responseObj = $response | ConvertFrom-Json
    
    if (-not $responseObj.result -or -not $responseObj.result.tools) {
        throw "Invalid response format"
    }
    
    return $responseObj.result.tools
}

function New-ToolsMarkdown {
    param(
        [array]$Tools,
        [string]$Version
    )
    
    $content = @"
# Tools available in Azure SDK MCP server

This document provides a comprehensive list of all MCP (Model Context Protocol) tools supported by the Azure SDK MCP server version $Version.

## Tools list

| Name | Description |
|----------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
"@
    
    foreach ($tool in $Tools | Sort-Object name) {
        $name = $tool.name
        $description = ($tool.description -replace "\r?\n", " ") -replace "\s+", " "
        $content += "`n| $name | $description |"
    }
    
    return $content
}

function Get-Version {
    param([string]$CsprojPath)
    
    if (Test-Path $CsprojPath) {
        [xml]$csproj = Get-Content $CsprojPath
        $versionPrefix = $csproj.Project.PropertyGroup.VersionPrefix
        if ($versionPrefix) {
            return $versionPrefix
        }
    }
    return "latest"
}

# Main script
$docsPath = Join-Path $PWD "tools/azsdk-cli/docs/mcp-tools.md"

if (-not (Test-Path $AzsdkCliPath)) {
    Write-Error "Azure SDK CLI tool not found at: $AzsdkCliPath"
    exit 1
}

$mcpProcess = $null

try {
    $version = Get-Version -CsprojPath $csprojPath
    $mcpProcess = Start-McpServer -ExePath $AzsdkCliPath
    $tools = Get-McpTools -Process $mcpProcess
    $markdown = New-ToolsMarkdown -Tools $tools -Version $version
    
    $docsDir = Split-Path $docsPath -Parent
    if (-not (Test-Path $docsDir)) {
        New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    }
    
    $markdown | Out-File -FilePath $docsPath -Encoding UTF8 -Force
    
    Write-Host "Updated documentation with $($tools.Count) tools: $docsPath"
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
finally {
    if ($null -ne $mcpProcess -and -not $mcpProcess.HasExited) {
        $mcpProcess.Kill()
        $mcpProcess.WaitForExit(5000)
    }
}
