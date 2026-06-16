#Requires -Version 7.0

<#
.SYNOPSIS
    Renders a human-readable Markdown rollup from the per-shard Vally JUnit results.

.DESCRIPTION
    The Summarize stage (Stage 3 of the mock-vertical skeleton) already publishes the
    merged JUnit via `PublishTestResults@2`, which gates the build but only shows a flat
    list of test cases in the ADO "Tests" tab. This script adds a *glanceable* layer:
    it reads every shard's JUnit XML, groups results by shard (one shard == one area in
    `area` mode, one file in `file` mode), and writes a Markdown table plus a list of the
    failing scenarios.

    When run under Azure Pipelines it emits `##vso[task.uploadsummary]`, which renders the
    Markdown directly on the run's Summary page — no clicking into the Tests tab to learn
    which area went red. The gate still lives in `PublishTestResults@2`; this script is
    presentation only and never changes pass/fail.

.PARAMETER ResultsRoot
    Folder that contains the downloaded per-shard result artifacts. Each shard lives in a
    sub-folder named `eval-result-<shardName>` holding one or more JUnit `*.xml` files.

.PARAMETER OutputPath
    Path to write the generated Markdown summary file.

.OUTPUTS
    The summary object (totals + per-shard rows) is returned as a hashtable. The Markdown
    is written to -OutputPath and, under Azure Pipelines, uploaded to the Summary page.

.EXAMPLE
    ./New-EvalSummary.ps1 -ResultsRoot $(Pipeline.Workspace) -OutputPath ./eval-summary.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResultsRoot,

    [Parameter()]
    [string]$OutputPath = 'eval-summary.md'
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

function Get-ShardName {
    # Maps a JUnit file path back to its shard. Result artifacts download into
    # folders named `eval-result-<shardName>`; everything below that is the shard.
    [CmdletBinding()]
    param(
        [string]$Path
    )

    foreach ($segment in ($Path -split '[\\/]')) {
        if ($segment -like 'eval-result-*') {
            return $segment -replace '^eval-result-', ''
        }
    }
    return 'unknown'
}

function Get-EvalSummary {
    [CmdletBinding()]
    param(
        [string]$ResultsRoot
    )

    $root = (Resolve-Path -LiteralPath $ResultsRoot).Path
    $xmlFiles = @(Get-ChildItem -Path $root -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue)

    $shards = [ordered]@{}
    foreach ($xmlFile in ($xmlFiles | Sort-Object FullName)) {
        $shardName = Get-ShardName -Path $xmlFile.FullName
        if (-not $shards.Contains($shardName)) {
            $shards[$shardName] = [ordered]@{
                shardName = $shardName
                total     = 0
                failed    = 0
                skipped   = 0
                durationS = 0.0
                failures  = [System.Collections.Generic.List[string]]::new()
            }
        }

        [xml]$doc = Get-Content -LiteralPath $xmlFile.FullName -Raw
        foreach ($case in $doc.SelectNodes('//testcase')) {
            $shards[$shardName].total++

            $time = 0.0
            if ($case.HasAttribute('time')) {
                [double]::TryParse($case.GetAttribute('time'),
                    [System.Globalization.NumberStyles]::Float,
                    [System.Globalization.CultureInfo]::InvariantCulture, [ref]$time) | Out-Null
            }
            $shards[$shardName].durationS += $time

            $isFailure = $null -ne $case.SelectSingleNode('failure') -or $null -ne $case.SelectSingleNode('error')
            $isSkipped = $null -ne $case.SelectSingleNode('skipped')
            if ($isFailure) {
                $shards[$shardName].failed++
                $name = $case.GetAttribute('name')
                if ([string]::IsNullOrWhiteSpace($name)) { $name = '(unnamed scenario)' }
                $shards[$shardName].failures.Add($name)
            }
            elseif ($isSkipped) {
                $shards[$shardName].skipped++
            }
        }
    }

    return $shards
}

function Format-EvalSummaryMarkdown {
    [CmdletBinding()]
    param(
        [System.Collections.Specialized.OrderedDictionary]$Shards
    )

    $totalTests = 0
    $totalFailed = 0
    $totalSkipped = 0
    foreach ($shard in $Shards.Values) {
        $totalTests += $shard.total
        $totalFailed += $shard.failed
        $totalSkipped += $shard.skipped
    }
    $totalPassed = $totalTests - $totalFailed - $totalSkipped

    $sb = [System.Text.StringBuilder]::new()
    $overall = if ($totalFailed -eq 0) { 'PASSED' } else { 'FAILED' }
    $overallIcon = if ($totalFailed -eq 0) { '✅' } else { '❌' }

    [void]$sb.AppendLine("## $overallIcon Vally eval results — $overall")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("**$totalPassed passed**, **$totalFailed failed**, $totalSkipped skipped across $($Shards.Count) shard(s).")
    [void]$sb.AppendLine('')

    if ($Shards.Count -eq 0) {
        [void]$sb.AppendLine('> No JUnit results were found.')
        return $sb.ToString()
    }

    [void]$sb.AppendLine('| Shard | Result | Passed | Failed | Skipped | Time (s) |')
    [void]$sb.AppendLine('| --- | :---: | ---: | ---: | ---: | ---: |')
    foreach ($shard in ($Shards.Values | Sort-Object shardName)) {
        $passed = $shard.total - $shard.failed - $shard.skipped
        $icon = if ($shard.failed -eq 0) { '✅' } else { '❌' }
        $time = '{0:N1}' -f $shard.durationS
        [void]$sb.AppendLine("| $($shard.shardName) | $icon | $passed | $($shard.failed) | $($shard.skipped) | $time |")
    }

    if ($totalFailed -gt 0) {
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('<details><summary>Failing scenarios</summary>')
        [void]$sb.AppendLine('')
        foreach ($shard in ($Shards.Values | Sort-Object shardName)) {
            if ($shard.failed -eq 0) { continue }
            [void]$sb.AppendLine("- **$($shard.shardName)**")
            foreach ($name in $shard.failures) {
                [void]$sb.AppendLine("  - $name")
            }
        }
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('</details>')
    }

    return $sb.ToString()
}

$shards = Get-EvalSummary -ResultsRoot $ResultsRoot
$markdown = Format-EvalSummaryMarkdown -Shards $shards
Set-Content -LiteralPath $OutputPath -Value $markdown -Encoding utf8

Write-Host $markdown

if ($env:TF_BUILD) {
    $resolved = (Resolve-Path -LiteralPath $OutputPath).Path
    Write-Host "##vso[task.uploadsummary]$resolved"
}

return $shards
