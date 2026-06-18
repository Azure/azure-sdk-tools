#Requires -Version 7.0

<#
.SYNOPSIS
    Verdict helpers for the Vally eval shard gate. Dot-source this file to use
    `Get-VallyShardVerdict` (and `Get-Prop`) without running an eval.

.DESCRIPTION
    Kept separate from Invoke-VallyEvalShard.ps1 so unit tests can dot-source the
    pure functions while the runner script always executes its `vally` call. (The
    PowerShell@2 pipeline task itself dot-sources the script it runs, so a
    "skip when dot-sourced" guard in the runner is unreliable — hence this split.)
#>

Set-StrictMode -Version 4

function Get-Prop {
    # StrictMode-safe property read for ConvertFrom-Json PSCustomObjects: returns
    # $Default when the property is absent instead of throwing.
    [CmdletBinding()]
    param(
        [object]$Object,
        [string]$Name,
        [object]$Default = $null
    )

    if ($null -ne $Object -and $Object.PSObject.Properties[$Name]) {
        return $Object.PSObject.Properties[$Name].Value
    }
    return $Default
}

function Get-VallyShardVerdict {
    # Reads the canonical `run-summary` record from the newest results.jsonl under
    # $ResultsDir and decides pass/fail from the eval verdict (threshold-based for
    # scored evals; binary for unscored). Returns a result object so the gating is
    # unit-testable without running `vally`.
    [CmdletBinding()]
    param(
        [string]$ResultsDir,
        [double]$Threshold = 0.8
    )

    $result = [ordered]@{
        Found              = $false
        Passed             = $false
        HadExecutionErrors = $false
        Lines              = [System.Collections.Generic.List[string]]::new()
    }

    if (-not (Test-Path -LiteralPath $ResultsDir)) {
        $result.Lines.Add("No results directory at '$ResultsDir'.")
        return [pscustomobject]$result
    }

    $summaryFile = Get-ChildItem -Path $ResultsDir -Filter 'results.jsonl' -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime |
        Select-Object -Last 1
    if (-not $summaryFile) {
        $result.Lines.Add("No results.jsonl found under '$ResultsDir'.")
        return [pscustomobject]$result
    }

    # The last `run-summary` line is the canonical end-of-run verdict.
    $runSummary = $null
    foreach ($line in (Get-Content -LiteralPath $summaryFile.FullName)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $obj = $line | ConvertFrom-Json } catch { continue }
        if ((Get-Prop $obj 'type') -eq 'run-summary') { $runSummary = $obj }
    }
    if ($null -eq $runSummary) {
        $result.Lines.Add("No run-summary record in '$($summaryFile.FullName)'.")
        return [pscustomobject]$result
    }

    $result.Found = $true
    $result.HadExecutionErrors = [bool](Get-Prop $runSummary 'hadExecutionErrors' $false)

    $evals = @(Get-Prop $runSummary 'evals' @())
    if ($evals.Count -eq 0) {
        $result.Lines.Add('run-summary contains no evals.')
        return [pscustomobject]$result
    }

    $allPassed = $true
    foreach ($e in $evals) {
        $name = Get-Prop $e 'name' '(unnamed eval)'
        $ran = [int](Get-Prop $e 'stimuliRun' 0)

        if ([bool](Get-Prop $e 'scoringApplied' $false)) {
            $score = [double](Get-Prop $e 'overallScore' 0)
            $thr = [double](Get-Prop $e 'threshold' $Threshold)
            # 1e-9 epsilon so an exact boundary (e.g. 0.80 >= 0.80) is not dropped
            # below the gate by float rounding.
            $pass = ($ran -gt 0) -and (($score + 1e-9) -ge $thr)
            $pct = '{0:N1}' -f ($score * 100)
            $thrPct = '{0:N1}' -f ($thr * 100)
            if ($pass) {
                $result.Lines.Add("PASS  $name — $pct% >= $thrPct% ($ran stimuli)")
            }
            else {
                $result.Lines.Add("FAIL  $name — $pct% < $thrPct% ($ran stimuli)")
                $allPassed = $false
            }
        }
        else {
            $pass = [bool](Get-Prop $e 'passed' $false) -and ($ran -gt 0)
            if ($pass) {
                $result.Lines.Add("PASS  $name — graders passed ($ran stimuli)")
            }
            else {
                $result.Lines.Add("FAIL  $name — graders failed ($ran stimuli)")
                $allPassed = $false
            }
        }
    }

    $result.Passed = $allPassed
    return [pscustomobject]$result
}
