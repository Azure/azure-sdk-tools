<#
.SYNOPSIS
Evaluates Copilot pipeline fixes for a set of Azure SDK repositories.

.PARAMETER Owner
GitHub repository owner.

.PARAMETER RepositoriesJson
JSON array containing the repository names to evaluate.

.PARAMETER SinceDays
Number of one-day evaluation windows to include in each repository report.

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

$githubToken = $env:GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($githubToken)) {
    throw "GITHUB_TOKEN is not set. Ensure the 'Login to GitHub' pipeline step completed and exported GH_TOKEN."
}

$env:GITHUB_TOKEN = $githubToken.Trim()
try {
    Invoke-RestMethod `
        -Uri 'https://api.github.com/installation/repositories?per_page=1' `
        -Headers @{
            Authorization = "Bearer $env:GITHUB_TOKEN"
            Accept = 'application/vnd.github+json'
            'User-Agent' = 'azure-sdk-pipeline-fix-evaluator'
            'X-GitHub-Api-Version' = '2022-11-28'
        } `
        -Method Get | Out-Null
}
catch {
    throw "GITHUB_TOKEN was rejected by GitHub. Check the Azure SDK Automation App installation and repository permissions, then rerun the pipeline. $($_.Exception.Message)"
}

$runUntil = (Get-Date).ToUniversalTime()
$repositories = @($RepositoriesJson | ConvertFrom-Json)

if ($repositories.Count -eq 0) {
    throw 'At least one repository must be provided.'
}
if ($SinceDays -le 0) {
    throw 'SinceDays must be greater than zero.'
}

$shareableMinutes = $JobTimeoutMinutes * 0.75
$evaluationCount = $repositories.Count
$perEvaluationMinutes = [int][math]::Max(1, [math]::Floor($shareableMinutes / $evaluationCount))
$timeoutMilliseconds = [int][TimeSpan]::FromMinutes($perEvaluationMinutes).TotalMilliseconds
Write-Host "Job timeout ${JobTimeoutMinutes}m over $evaluationCount evaluation(s): allowing ${perEvaluationMinutes}m each."

$failed = @()

foreach ($repository in $repositories) {
    Write-Host "##[group]Evaluating $Owner/$repository"

    $repositoryDirectory = Join-Path $EvalArtifactDir "$MetricsPrefix/$repository"
    New-Item -ItemType Directory -Force -Path $repositoryDirectory | Out-Null

    # CLI stdout is temporary input for splitting; only the dated *.json files are uploaded.
    $combinedReportPath = Join-Path $repositoryDirectory 'report.tmp'
    $standardErrorPath = Join-Path $repositoryDirectory 'report.stderr.log'
    Remove-Item -Path $combinedReportPath, $standardErrorPath -Force -ErrorAction SilentlyContinue

    $evaluationArguments = @(
        'eng', 'evaluate', $Owner, $repository,
        '--since-days', $SinceDays,
        '--until', $runUntil.ToString('o'),
        '--output', 'json'
    )
    $process = Start-Process `
        -FilePath $AzSdkPath `
        -ArgumentList $evaluationArguments `
        -RedirectStandardOutput $combinedReportPath `
        -RedirectStandardError $standardErrorPath `
        -PassThru
    $completed = $process.WaitForExit($timeoutMilliseconds)
    if (-not $completed) {
        $process.Kill($true)
        $process.WaitForExit()
    }

    $standardError = ''
    if (Test-Path $standardErrorPath) { 
        $standardError = Get-Content -Raw -Path $standardErrorPath 
    }
    
    Remove-Item -Path $standardErrorPath -Force -ErrorAction SilentlyContinue
    if (-not $completed -or $process.ExitCode -ne 0) {
        $reason = if ($completed) { "failed with exit code $($process.ExitCode)" } else { "exceeded ${perEvaluationMinutes}m" }
        Write-Host "##vso[task.logissue type=error]eng evaluate for $repository $reason"
        if (-not [string]::IsNullOrWhiteSpace($standardError)) { Write-Host $standardError }
        Remove-Item -Path $combinedReportPath -Force -ErrorAction SilentlyContinue
        $failed += $repository
        Write-Host '##[endgroup]'
        continue
    }

    try {
        $currentReport = Get-Content -Raw -Path $combinedReportPath | ConvertFrom-Json
        if ($currentReport.owner -ne $Owner -or $currentReport.repo -ne $repository -or $null -eq $currentReport.dates) {
            throw 'The report does not contain the requested owner, repository, and dates.'
        }

        $seenDates = @{}
        foreach ($dateReport in @($currentReport.dates)) {
            $dateKey = [string] $dateReport.date
            if ($dateKey -notmatch '^\d{4}-\d{2}-\d{2}$' -or $null -eq $dateReport.evaluations) {
                throw "The report contains an invalid dated evaluation '$dateKey'."
            }
            if ($seenDates.ContainsKey($dateKey)) {
                throw "The report contains duplicate date '$dateKey'."
            }
            $seenDates[$dateKey] = $true
        }

        foreach ($dateReport in @($currentReport.dates)) {
            $dateKey = [string] $dateReport.date
            $datedReportPath = Join-Path $repositoryDirectory "$dateKey.json"
            $datedReport = [ordered]@{
                operation_status = $currentReport.operation_status
                owner = $currentReport.owner
                repo = $currentReport.repo
                dates = @($dateReport)
            }
            $datedReport | ConvertTo-Json -Depth 100 | Set-Content -Path $datedReportPath -Encoding utf8
            Write-Host "$repository/$dateKey -> $datedReportPath ($(@($dateReport.evaluations).Count) PR(s))"
        }
    } catch {
        Write-Host "##vso[task.logissue type=error]eng evaluate produced an invalid report for ${repository}: $_"
        Get-ChildItem -Path $repositoryDirectory -Filter '*.json' -File | Remove-Item -Force
        $failed += $repository
        Write-Host '##[endgroup]'
        continue
    } finally {
        Remove-Item -Path $combinedReportPath -Force -ErrorAction SilentlyContinue
    }

    Write-Host '##[endgroup]'
}

if ($failed.Count -eq $evaluationCount) {
    throw 'Evaluation failed for every repository; nothing to publish.'
}

if ($failed.Count -gt 0) {
    Write-Host "##vso[task.complete result=SucceededWithIssues;]Evaluation failed for: $($failed -join ', ')"
}
