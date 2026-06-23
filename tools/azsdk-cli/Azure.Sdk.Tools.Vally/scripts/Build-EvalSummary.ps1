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
    ./Build-EvalSummary.ps1 -ResultsRoot $(Pipeline.Workspace) -OutputPath ./eval-summary.md
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

    # Fallback when the path isn't under an `eval-result-<shard>` folder (e.g. a
    # local combined run, or an unexpected download layout): use the nearest
    # ancestor directory that isn't a Vally timestamp folder, so the shard column
    # shows something diagnostic instead of a useless 'unknown'.
    $segments = @($Path -split '[\\/]' | Where-Object { $_ })
    for ($i = $segments.Count - 2; $i -ge 0; $i--) {
        if ($segments[$i] -notmatch '^\d{4}-\d{2}-\d{2}T') {
            return $segments[$i]
        }
    }
    return 'unknown'
}

function Get-StimulusName {
    # Strips Vally's ' (trial N)' suffix so every trial of a stimulus collapses to
    # one stimulus. With --runs N, JUnit emits one <testcase> PER TRIAL named
    # '<stimulus> (trial 1)', '(trial 2)', ... — grouping on the bare name turns
    # those trials back into a single stimulus so counts are not inflated by N.
    [CmdletBinding()]
    param(
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Name)) { return '(unnamed scenario)' }
    return ($Name -replace '\s*\(trial\s+\d+\)\s*$', '').Trim()
}

function Format-Pct {
    # Formats a 0..1 ratio as a percentage, dropping a trailing '.0' so whole
    # numbers read as '100%' while fractional rates keep one decimal ('93.5%').
    [CmdletBinding()]
    param(
        [double]$Ratio
    )

    $pct = $Ratio * 100
    if ([math]::Abs($pct - [math]::Round($pct)) -lt 0.05) {
        return ('{0:N0}%' -f $pct)
    }
    return ('{0:N1}%' -f $pct)
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
                # stimulus name -> @{ trials; passed; skipped }
                stimuli   = [ordered]@{}
            }
        }
        $shard = $shards[$shardName]

        [xml]$doc = Get-Content -LiteralPath $xmlFile.FullName -Raw

        # The pass/fail gate is per-stimulus at the suite threshold (e.g. 0.8), not
        # per-trial — so read the threshold from each <testsuite> and aggregate its
        # trials back up to the stimulus, mirroring exactly what `vally eval` gates on.
        foreach ($suite in $doc.SelectNodes('//testsuite')) {
            $threshold = 0.8
            $thresholdNode = $suite.SelectSingleNode("properties/property[@name='threshold']")
            if ($null -ne $thresholdNode) {
                [double]::TryParse($thresholdNode.GetAttribute('value'),
                    [System.Globalization.NumberStyles]::Float,
                    [System.Globalization.CultureInfo]::InvariantCulture, [ref]$threshold) | Out-Null
            }

            foreach ($case in $suite.SelectNodes('testcase')) {
                $stimulus = Get-StimulusName -Name $case.GetAttribute('name')
                if (-not $shard.stimuli.Contains($stimulus)) {
                    $shard.stimuli[$stimulus] = [ordered]@{
                        trials    = 0
                        passed    = 0
                        skipped   = 0
                        threshold = $threshold
                    }
                }
                $entry = $shard.stimuli[$stimulus]
                $entry.threshold = $threshold

                $time = 0.0
                if ($case.HasAttribute('time')) {
                    [double]::TryParse($case.GetAttribute('time'),
                        [System.Globalization.NumberStyles]::Float,
                        [System.Globalization.CultureInfo]::InvariantCulture, [ref]$time) | Out-Null
                }
                $shard.durationS += $time

                $isFailure = $null -ne $case.SelectSingleNode('failure') -or $null -ne $case.SelectSingleNode('error')
                $isSkipped = $null -ne $case.SelectSingleNode('skipped')
                if ($isSkipped) {
                    $entry.skipped++
                }
                else {
                    $entry.trials++
                    if (-not $isFailure) { $entry.passed++ }
                }
            }
        }
    }

    # Collapse each stimulus's trials into a single pass/fail using the suite
    # threshold (pass rate >= threshold). 1e-9 epsilon guards float rounding so
    # e.g. 4/5 = 0.8 is not spuriously dropped below an 0.8 gate.
    #
    # This runs once per shard AFTER every XML file has been read. A shard's
    # artifact folder can hold multiple JUnit files, and `stimuli` accumulates
    # across all of them; collapsing inside the per-file loop would re-tally
    # already-seen stimuli on each subsequent file and double-count totals.
    foreach ($shardName in $shards.Keys) {
        $shard = $shards[$shardName]
        foreach ($stimulus in $shard.stimuli.Keys) {
            $entry = $shard.stimuli[$stimulus]
            $shard.total++

            if ($entry.trials -eq 0) {
                $shard.skipped++
                continue
            }

            $passRate = $entry.passed / $entry.trials
            if ($passRate + 1e-9 -lt $entry.threshold) {
                $shard.failed++
                $shard.failures.Add("$stimulus ($($entry.passed)/$($entry.trials) runs passed)")
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

    # A run that parsed zero testcases is NOT a pass — it means the shards never
    # published JUnit (failed before running, or empty `eval-result-*` artifacts).
    # Surface it as a loud NO RESULTS state instead of a misleading green PASSED.
    if ($totalTests -eq 0) {
        $overall = 'NO RESULTS'
        $overallIcon = '⚠️'
    }
    elseif ($totalFailed -eq 0) {
        $overall = 'PASSED'
        $overallIcon = '✅'
    }
    else {
        $overall = 'FAILED'
        $overallIcon = '❌'
    }

    # Pass rate is measured over scenarios that actually ran (skips excluded), so
    # an all-skipped shard never inflates or deflates the headline number.
    $nonSkipped = $totalPassed + $totalFailed
    $overallPct = if ($nonSkipped -gt 0) { $totalPassed / $nonSkipped } else { 0 }

    [void]$sb.AppendLine("## $overallIcon Vally eval results — $overall")
    [void]$sb.AppendLine('')

    if ($totalTests -eq 0) {
        [void]$sb.AppendLine("No scenarios were found across $($Shards.Count) shard(s).")
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('> ⚠️ No eval testcases were found in the downloaded results. This usually means the')
        [void]$sb.AppendLine('> eval shards did not publish JUnit — the shard jobs failed before running, or the')
        [void]$sb.AppendLine('> `eval-result-*` artifacts were empty. Check the Eval stage shard logs.')
        return $sb.ToString()
    }

    # Glanceable one-liner: scenario/shard totals plus the colour-coded tallies and
    # the headline pass rate, so the verdict is readable without opening the table.
    $scenarioWord = if ($totalTests -eq 1) { 'scenario' } else { 'scenarios' }
    $shardWord = if ($Shards.Count -eq 1) { 'shard' } else { 'shards' }
    $tallies = [System.Collections.Generic.List[string]]::new()
    $tallies.Add("✅ **$totalPassed passed**")
    if ($totalFailed -gt 0) { $tallies.Add("❌ **$totalFailed failed**") }
    if ($totalSkipped -gt 0) { $tallies.Add("⏭️ $totalSkipped skipped") }
    $tallies.Add("**$(Format-Pct $overallPct)** pass rate")
    [void]$sb.AppendLine("**$totalTests $scenarioWord** across **$($Shards.Count) $shardWord** — $([string]::Join(' · ', $tallies))")
    [void]$sb.AppendLine('')

    # Red shards first (then alphabetical) so a reader's eye lands on failures.
    $orderedShards = @($Shards.Values | Sort-Object @{ Expression = { $_.failed -eq 0 } }, shardName)

    [void]$sb.AppendLine('| Shard | Result | Pass rate | Passed | Failed | Skipped | Time (s) |')
    [void]$sb.AppendLine('| --- | :---: | ---: | ---: | ---: | ---: | ---: |')
    foreach ($shard in $orderedShards) {
        $passed = $shard.total - $shard.failed - $shard.skipped
        $icon = if ($shard.failed -eq 0) { '✅' } else { '❌' }
        $shardRan = $passed + $shard.failed
        $shardPct = if ($shardRan -gt 0) { Format-Pct ($passed / $shardRan) } else { '—' }
        $time = '{0:N1}' -f $shard.durationS
        [void]$sb.AppendLine("| $($shard.shardName) | $icon | $shardPct | $passed | $($shard.failed) | $($shard.skipped) | $time |")
    }
    # Totals row anchors the table so the rollup reads at a glance even with many shards.
    $totalIcon = if ($totalFailed -eq 0) { '✅' } else { '❌' }
    $totalDuration = 0.0
    foreach ($shard in $Shards.Values) { $totalDuration += $shard.durationS }
    $totalTime = '{0:N1}' -f $totalDuration
    [void]$sb.AppendLine("| **Total** | $totalIcon | **$(Format-Pct $overallPct)** | **$totalPassed** | **$totalFailed** | **$totalSkipped** | **$totalTime** |")

    if ($totalFailed -gt 0) {
        $failWord = if ($totalFailed -eq 1) { 'scenario' } else { 'scenarios' }
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("<details open><summary><strong>❌ $totalFailed failing $failWord</strong></summary>")
        [void]$sb.AppendLine('')
        foreach ($shard in $orderedShards) {
            if ($shard.failed -eq 0) { continue }
            [void]$sb.AppendLine("- **$($shard.shardName)**")
            foreach ($name in $shard.failures) {
                [void]$sb.AppendLine("  - ❌ $name")
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
