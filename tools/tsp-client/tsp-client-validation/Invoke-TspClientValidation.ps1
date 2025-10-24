#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Automates the triggering of SDK validation pipelines for tsp-client version updates.

.DESCRIPTION
    This script helps automate the process of running SDK validation pipelines across all languages
    when a new tsp-client version is released. It follows the checklist from the 
    tspclient-automation-check.prompt.md file.

.PARAMETER PRNumber
    The PR number from azure-sdk-tools that updates the tsp-client version (e.g., 12360)

.PARAMETER SyncBranch
    The sync branch name (e.g., "sync-eng/common-update-tsp-client-12360")

.PARAMETER Languages
    Array of languages to validate. Default: @("Python", "NET", "Java", "JS", "Go")

.PARAMETER DryRun
    If specified, shows what commands would be executed without actually running them

.PARAMETER MonitorOnly
    If specified, only monitors existing pipeline runs without triggering new ones

.EXAMPLE
    .\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch "sync-eng/common-update-tsp-client-12360"

.EXAMPLE
    .\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch "sync-eng/common-update-tsp-client-12360" -Languages @("Python", "NET") -DryRun

.EXAMPLE
    .\Invoke-TspClientValidation.ps1 -MonitorOnly

.NOTES
    Prerequisites:
    - Azure CLI installed with azure-devops extension
    - Authenticated to Azure DevOps (azure-sdk organization)
    - Access to the azure-sdk DevOps pipelines
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [int]$PRNumber,
    
    [Parameter(Mandatory = $false)]
    [string]$SyncBranch,
    
    [Parameter(Mandatory = $false)]
    [string[]]$Languages = @("Python", "NET", "Java", "JS", "Go"),
    
    [Parameter(Mandatory = $false)]
    [switch]$DryRun,
    
    [Parameter(Mandatory = $false)]
    [switch]$MonitorOnly
)

# SDK validation pipeline mapping
$PipelineMapping = @{
    "Python" = @{
        PipelineId = 7519
        Repository = "azure-sdk-for-python"
        PRPattern = "Azure/azure-sdk-for-python/pull/"
    }
    "NET" = @{
        PipelineId = 7516
        Repository = "azure-sdk-for-net"
        PRPattern = "Azure/azure-sdk-for-net/pull/"
    }
    "Java" = @{
        PipelineId = 7515
        Repository = "azure-sdk-for-java"
        PRPattern = "Azure/azure-sdk-for-java/pull/"
    }
    "JS" = @{
        PipelineId = 7518
        Repository = "azure-sdk-for-js"
        PRPattern = "Azure/azure-sdk-for-js/pull/"
    }
    "Go" = @{
        PipelineId = 7517
        Repository = "azure-sdk-for-go"
        PRPattern = "Azure/azure-sdk-for-go/pull/"
    }
}

function Write-Header {
    param([string]$Title)
    Write-Host "`n" -NoNewline
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Yellow
    Write-Host "=" * 80 -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n✓ " -NoNewline -ForegroundColor Green
    Write-Host $Message -ForegroundColor White
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ " -NoNewline -ForegroundColor Blue
    Write-Host $Message -ForegroundColor Gray
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ " -NoNewline -ForegroundColor Yellow
    Write-Host $Message -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "  ✗ " -NoNewline -ForegroundColor Red
    Write-Host $Message -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Step "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    try {
        $azVersion = az --version 2>$null
        if ($azVersion) {
            Write-Info "Azure CLI is installed"
        }
    }
    catch {
        Write-Error "Azure CLI is not installed. Please install it first."
        return $false
    }
    
    # Check if azure-devops extension is installed
    $extensions = az extension list --output json | ConvertFrom-Json
    $devopsExtension = $extensions | Where-Object { $_.name -eq "azure-devops" }
    
    if ($devopsExtension) {
        Write-Info "Azure DevOps extension is installed (version $($devopsExtension.version))"
    }
    else {
        Write-Error "Azure DevOps extension is not installed. Run: az extension add --name azure-devops"
        return $false
    }
    
    # Check Azure DevOps configuration
    try {
        $config = az devops configure --list 2>$null
        if ($config -match "organization.*azure-sdk") {
            Write-Info "Azure DevOps is configured for azure-sdk organization"
        }
        else {
            Write-Warning "Azure DevOps may not be configured for azure-sdk organization"
            Write-Info "Run: az devops configure --defaults organization=https://dev.azure.com/azure-sdk"
        }
    }
    catch {
        Write-Warning "Could not verify Azure DevOps configuration"
    }
    
    return $true
}

function Get-SyncPRInfo {
    param([int]$PRNumber)
    
    Write-Step "Looking up sync PR information for PR #$PRNumber..."
    
    # Expected sync PR pattern based on PR number
    $syncPRs = @{
        "Python" = @{
            Repository = "azure-sdk-for-python"
            ExpectedBranch = "sync-eng/common-update-tsp-client-$PRNumber"
        }
        "NET" = @{
            Repository = "azure-sdk-for-net"
            ExpectedBranch = "sync-eng/common-update-tsp-client-$PRNumber"
        }
        "Java" = @{
            Repository = "azure-sdk-for-java"
            ExpectedBranch = "sync-eng/common-update-tsp-client-$PRNumber"
        }
        "JS" = @{
            Repository = "azure-sdk-for-js"
            ExpectedBranch = "sync-eng/common-update-tsp-client-$PRNumber"
        }
        "Go" = @{
            Repository = "azure-sdk-for-go"
            ExpectedBranch = "sync-eng/common-update-tsp-client-$PRNumber"
        }
    }
    
    Write-Info "Expected sync branch pattern: sync-eng/common-update-tsp-client-$PRNumber"
    
    return $syncPRs
}

function Invoke-PipelineRun {
    param(
        [string]$Language,
        [string]$SyncBranch,
        [bool]$DryRun = $false
    )
    
    $pipelineInfo = $PipelineMapping[$Language]
    if (-not $pipelineInfo) {
        Write-Error "Unknown language: $Language"
        return $null
    }
    
    $pipelineId = $pipelineInfo.PipelineId
    $command = "az pipelines run --id $pipelineId --branch $SyncBranch --parameters SdkRepoBranch=$SyncBranch"
    
    Write-Info "Command: $command"
    
    if ($DryRun) {
        Write-Warning "[DRY RUN] Would execute: $command"
        return @{
            Language = $Language
            PipelineId = $pipelineId
            Command = $command
            Status = "DryRun"
        }
    }
    
    try {
        Write-Info "Triggering $Language SDK validation pipeline..."
        $result = Invoke-Expression $command | ConvertFrom-Json
        
        if ($result -and $result.id) {
            Write-Step "Successfully triggered $Language pipeline!"
            Write-Info "Build ID: $($result.id)"
            Write-Info "Build Number: $($result.buildNumber)"
            Write-Info "Status: $($result.status)"
            Write-Info "URL: https://dev.azure.com/azure-sdk/public/_build/results?buildId=$($result.id)"
            
            return @{
                Language = $Language
                PipelineId = $pipelineId
                BuildId = $result.id
                BuildNumber = $result.buildNumber
                Status = $result.status
                Url = "https://dev.azure.com/azure-sdk/public/_build/results?buildId=$($result.id)"
            }
        }
        else {
            Write-Error "Failed to trigger $Language pipeline - no build ID returned"
            return $null
        }
    }
    catch {
        Write-Error "Failed to trigger $Language pipeline: $($_.Exception.Message)"
        return $null
    }
}

function Get-PipelineStatus {
    param([string]$BuildId)
    
    try {
        $result = az pipelines runs show --id $BuildId --output json | ConvertFrom-Json
        return @{
            BuildId = $result.id
            BuildNumber = $result.buildNumber
            Status = $result.status
            Result = $result.result
            StartTime = $result.startTime
            FinishTime = $result.finishTime
        }
    }
    catch {
        Write-Warning "Could not get status for build ${BuildId}: $($_.Exception.Message)"
        return $null
    }
}

function Show-Summary {
    param([array]$Results)
    
    Write-Header "SUMMARY"
    
    if ($Results.Count -eq 0) {
        Write-Warning "No pipelines were triggered"
        return
    }
    
    Write-Host "`nTriggered Pipelines:" -ForegroundColor Yellow
    Write-Host ("-" * 80) -ForegroundColor Gray
    
    $tableData = @()
    foreach ($result in $Results) {
        if ($result) {
            $status = if ($result.Status -eq "DryRun") { "DRY RUN" } else { $result.Status }
            $buildInfo = if ($result.BuildId) { $result.BuildId } else { "N/A" }
            
            $tableData += [PSCustomObject]@{
                Language = $result.Language
                Status = $status
                BuildId = $buildInfo
                PipelineId = $result.PipelineId
            }
            
            if ($result.Url -and $result.Status -ne "DryRun") {
                Write-Host "`n$($result.Language) Pipeline:" -ForegroundColor Cyan
                Write-Host "  Build ID: $($result.BuildId)" -ForegroundColor White
                Write-Host "  URL: $($result.Url)" -ForegroundColor Blue
            }
        }
    }
    
    if ($tableData.Count -gt 0) {
        Write-Host "`nPipeline Summary:" -ForegroundColor Yellow
        $tableData | Format-Table -AutoSize
    }
    
    # Monitoring commands
    Write-Host "`nMonitoring Commands:" -ForegroundColor Yellow
    Write-Host ("-" * 80) -ForegroundColor Gray
    foreach ($result in $Results) {
        if ($result -and $result.BuildId -and $result.Status -ne "DryRun") {
            Write-Host "az pipelines runs show --id $($result.BuildId) --output table" -ForegroundColor Green
        }
    }
}

function Monitor-Pipelines {
    param([array]$BuildIds)
    
    if ($BuildIds.Count -eq 0) {
        Write-Warning "No build IDs provided for monitoring"
        return
    }
    
    Write-Header "MONITORING PIPELINE STATUS"
    
    do {
        $allCompleted = $true
        $currentTime = Get-Date -Format "HH:mm:ss"
        
        Write-Host "`n[$currentTime] Pipeline Status:" -ForegroundColor Yellow
        Write-Host ("-" * 60) -ForegroundColor Gray
        
        foreach ($buildId in $BuildIds) {
            $status = Get-PipelineStatus -BuildId $buildId
            if ($status) {
                $statusColor = switch ($status.Status) {
                    "completed" { "Green" }
                    "inProgress" { "Yellow" }
                    "notStarted" { "Cyan" }
                    default { "White" }
                }
                
                Write-Host "Build $($status.BuildId): " -NoNewline -ForegroundColor White
                Write-Host $status.Status -ForegroundColor $statusColor
                
                if ($status.Result) {
                    $resultColor = switch ($status.Result) {
                        "succeeded" { "Green" }
                        "failed" { "Red" }
                        "partiallySucceeded" { "Yellow" }
                        default { "White" }
                    }
                    Write-Host "  Result: " -NoNewline -ForegroundColor Gray
                    Write-Host $status.Result -ForegroundColor $resultColor
                }
                
                if ($status.Status -ne "completed") {
                    $allCompleted = $false
                }
            }
        }
        
        if (-not $allCompleted) {
            Write-Host "`nWaiting 30 seconds before next check..." -ForegroundColor Gray
            Start-Sleep -Seconds 30
        }
        
    } while (-not $allCompleted)
    
    Write-Host "`n✓ All pipelines completed!" -ForegroundColor Green
}

# Main execution
Write-Header "TSP-CLIENT VALIDATION AUTOMATION"

if ($MonitorOnly) {
    Write-Step "Monitor-only mode selected"
    Write-Host "Please provide build IDs to monitor (comma-separated): " -NoNewline -ForegroundColor Yellow
    $buildIdsInput = Read-Host
    
    if ($buildIdsInput) {
        $buildIds = $buildIdsInput.Split(',') | ForEach-Object { $_.Trim() }
        Monitor-Pipelines -BuildIds $buildIds
    }
    else {
        Write-Warning "No build IDs provided"
    }
    exit
}

# Validate parameters
if (-not $PRNumber -or -not $SyncBranch) {
    Write-Error "PRNumber and SyncBranch are required unless using -MonitorOnly"
    Write-Host "`nExample usage:" -ForegroundColor Yellow
    Write-Host "  .\Invoke-TspClientValidation.ps1 -PRNumber 12360 -SyncBranch 'sync-eng/common-update-tsp-client-12360'" -ForegroundColor Green
    exit 1
}

# Check prerequisites
if (-not (Test-Prerequisites)) {
    Write-Error "Prerequisites not met. Please fix the issues above and try again."
    exit 1
}

# Get sync PR information
$syncPRs = Get-SyncPRInfo -PRNumber $PRNumber

# Display what will be done
Write-Header "EXECUTION PLAN"
Write-Host "PR Number: " -NoNewline -ForegroundColor White
Write-Host $PRNumber -ForegroundColor Yellow

Write-Host "Sync Branch: " -NoNewline -ForegroundColor White
Write-Host $SyncBranch -ForegroundColor Yellow

Write-Host "Languages: " -NoNewline -ForegroundColor White
Write-Host ($Languages -join ", ") -ForegroundColor Yellow

Write-Host "Dry Run: " -NoNewline -ForegroundColor White
Write-Host $DryRun -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Green" })

Write-Host "`nPipelines to trigger:" -ForegroundColor Yellow
foreach ($lang in $Languages) {
    $pipelineInfo = $PipelineMapping[$lang]
    if ($pipelineInfo) {
        Write-Host "  $lang`: Pipeline ID $($pipelineInfo.PipelineId) ($($pipelineInfo.Repository))" -ForegroundColor Cyan
    }
}

if (-not $DryRun) {
    Write-Host "`nPress Enter to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
    Read-Host
}

# Trigger pipelines
Write-Header "TRIGGERING PIPELINES"

$results = @()
foreach ($language in $Languages) {
    $result = Invoke-PipelineRun -Language $language -SyncBranch $SyncBranch -DryRun $DryRun
    if ($result) {
        $results += $result
    }
    Start-Sleep -Seconds 2  # Small delay between pipeline triggers
}

# Show summary
Show-Summary -Results $results

# Offer to monitor pipelines
if (-not $DryRun -and $results.Count -gt 0) {
    $buildIds = $results | Where-Object { $_.BuildId } | ForEach-Object { $_.BuildId }
    
    if ($buildIds.Count -gt 0) {
        Write-Host "`nWould you like to monitor the pipeline progress? (y/N): " -NoNewline -ForegroundColor Yellow
        $monitor = Read-Host
        
        if ($monitor -eq 'y' -or $monitor -eq 'Y') {
            Monitor-Pipelines -BuildIds $buildIds
        }
    }
}

Write-Header "COMPLETED"
Write-Step "TSP-Client validation automation completed!"