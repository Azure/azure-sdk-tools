<#
.SYNOPSIS
Evaluates Copilot pipeline fixes for a set of Azure SDK repositories.

.PARAMETER Owner
GitHub repository owner.

.PARAMETER RepositoriesJson
JSON array containing the repository names to evaluate.

.PARAMETER SinceDays
Number of days of merged pull requests to evaluate.

.PARAMETER JobTimeoutMinutes
Overall pipeline job timeout used to calculate a per-repository timeout.

.PARAMETER EvalArtifactDir
Directory where evaluation reports are written.

.PARAMETER MetricsPrefix
Blob key prefix included in the report directory layout.

.PARAMETER AzSdkPath
Path to the azsdk CLI executable.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Owner,

    [string] $RepositoriesJson = $env:REPOSITORIES_JSON,

    [Parameter(Mandatory = $true)]
    [int] $SinceDays,

    [Parameter(Mandatory = $true)]
    [int] $JobTimeoutMinutes,

    [Parameter(Mandatory = $true)]
    [string] $EvalArtifactDir,

    [Parameter(Mandatory = $true)]
    [string] $MetricsPrefix,

    [Parameter(Mandatory = $true)]
    [string] $AzSdkPath
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$reportDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
$repositories = @($RepositoriesJson | ConvertFrom-Json)

if ($repositories.Count -eq 0) {
    throw 'At least one repository must be provided.'
}

$shareableMinutes = $JobTimeoutMinutes * 0.75
$perRepositoryMinutes = [int][math]::Max(1, [math]::Floor($shareableMinutes / $repositories.Count))
$timeoutArgument = "${perRepositoryMinutes}m"
Write-Host "Job timeout ${JobTimeoutMinutes}m over $($repositories.Count) repo(s): allowing $timeoutArgument each."

$failed = @()

foreach ($repository in $repositories) {
    Write-Host "##[group]Evaluating $Owner/$repository"

    $repositoryDirectory = Join-Path $EvalArtifactDir "$MetricsPrefix/$repository"
    New-Item -ItemType Directory -Force -Path $repositoryDirectory | Out-Null
    $reportPath = Join-Path $repositoryDirectory "$reportDate.json"

    $evaluationArguments = @(
        'eng', 'evaluate', $Owner, $repository,
        '--since-days', $SinceDays,
        '--output', 'json'
    )

    timeout --signal=TERM --kill-after=60s $timeoutArgument `
        $AzSdkPath @evaluationArguments > $reportPath
    $exitCode = $LASTEXITCODE

    if ($exitCode -in @(124, 137)) {
        Write-Host "##vso[task.logissue type=error]eng evaluate for $repository exceeded $timeoutArgument and was terminated"
        Remove-Item -Path $reportPath -Force -ErrorAction SilentlyContinue
        $failed += $repository
        Write-Host '##[endgroup]'
        continue
    }

    if ($exitCode -ne 0) {
        Write-Host "##vso[task.logissue type=error]eng evaluate failed for $repository with exit code $exitCode"
        Remove-Item -Path $reportPath -Force -ErrorAction SilentlyContinue
        $failed += $repository
        Write-Host '##[endgroup]'
        continue
    }

    try {
        $report = Get-Content -Raw -Path $reportPath | ConvertFrom-Json
    } catch {
        Write-Host "##vso[task.logissue type=error]eng evaluate produced invalid JSON for ${repository}: $_"
        Remove-Item -Path $reportPath -Force -ErrorAction SilentlyContinue
        $failed += $repository
        Write-Host '##[endgroup]'
        continue
    }

    $hasResults = $report.PSObject.Properties.Name -contains 'results' -and $null -ne $report.results
    $count = if ($hasResults) { @($report.results).Count } else { 0 }
    Write-Host "$repository -> $reportPath ($count evaluation(s))"
    Write-Host '##[endgroup]'
}

if ($failed.Count -eq $repositories.Count) {
    throw 'Evaluation failed for every repository; nothing to publish.'
}

if ($failed.Count -gt 0) {
    Write-Host "##vso[task.complete result=SucceededWithIssues;]Evaluation failed for: $($failed -join ', ')"
}