#Requires -Version 7.0

<#
.SYNOPSIS
    Runs one Vally eval shard and gates the step on the eval *verdict*, not on
    the `vally` process exit code.

.DESCRIPTION
    Vally can exit non-zero AFTER it has already computed a passing verdict and
    written every artifact: when its executor / MCP child processes miss the
    shutdown window it prints "Timed out while shutting down executors." and the
    process exits 1. That teardown flake must not turn a passing shard red.

    The authoritative verdict is the single `run-summary` record Vally appends to
    `results.jsonl`. This script runs `vally eval`, then reads that record and
    decides pass/fail from it:

      * Scored evals (a threshold is configured)  -> pass when the eval's
        pass-rate `overallScore` is >= its `threshold` AND its stimuli actually
        ran. This honours the configured gate (e.g. 0.8) — a >=80% pass rate is a
        PASS — and is independent of the process exit code, so a post-verdict
        shutdown timeout no longer fails a passing shard.
      * Unscored evals (binary graders)           -> pass when the eval's own
        `passed` verdict is true and its stimuli ran.

    A genuine failure (pass-rate below threshold, no stimuli ran, or no
    results.jsonl at all) still exits 1, so real regressions stay red.

.PARAMETER EvalArgs
    The `-e <file>` arguments for the shard, exactly as the matrix emits them
    (e.g. "-e evals/tools/add-arm-resource.eval.yaml"). Whitespace-separated;
    eval paths are forward-slashed and contain no spaces.

.PARAMETER ShardName
    The shard name, used only for log messages.

.PARAMETER OutputDir
    The `--output-dir` Vally writes results into. results.jsonl is found beneath
    it (Vally nests a per-run timestamp folder).

.PARAMETER SkillEvalPrefix
    Path to `eng/skill-eval` (the npm prefix that resolves the pinned Vally CLI).

.PARAMETER Threshold
    Pass-rate gate forwarded to `vally eval --threshold`. Default 0.8.

.OUTPUTS
    Exit code 0 when the shard's eval verdict passes, 1 otherwise.
#>
[CmdletBinding()]
param(
    [string]$EvalArgs,
    [string]$ShardName,
    [string]$OutputDir,
    [string]$SkillEvalPrefix,
    [double]$Threshold = 0.8
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

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

# --- Execution block (skipped when the script is dot-sourced for testing) ------
if ($MyInvocation.InvocationName -ne '.') {
    foreach ($pair in @{ EvalArgs = $EvalArgs; ShardName = $ShardName; OutputDir = $OutputDir; SkillEvalPrefix = $SkillEvalPrefix }.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace($pair.Value)) {
            throw "Required parameter -$($pair.Key) was not supplied."
        }
    }

    # evalArgs is a whitespace-separated string like "-e evals/tools/foo.eval.yaml".
    $evalArgList = @($EvalArgs -split '\s+' | Where-Object { $_ })
    $thresholdArg = $Threshold.ToString([System.Globalization.CultureInfo]::InvariantCulture)

    Write-Host "Running: vally eval $EvalArgs --junit --threshold $thresholdArg --output-dir `"$OutputDir`""
    # Do NOT abort on a non-zero exit — the verdict below is authoritative. A
    # teardown timeout can make `vally` exit 1 after a passing verdict.
    & npm exec --no --prefix $SkillEvalPrefix -- vally eval @evalArgList --junit --threshold $thresholdArg --output-dir $OutputDir
    $vallyExit = $LASTEXITCODE

    $verdict = Get-VallyShardVerdict -ResultsDir $OutputDir -Threshold $Threshold
    foreach ($line in $verdict.Lines) { Write-Host "  $line" }

    if (-not $verdict.Found) {
        Write-Host "##vso[task.logissue type=error]Shard '$ShardName' produced no usable verdict (vally exit $vallyExit). Treating as failure."
        exit 1
    }

    if ($verdict.Passed) {
        if ($verdict.HadExecutionErrors) {
            Write-Host "##vso[task.logissue type=warning]Shard '$ShardName' passed the pass-rate threshold but Vally reported execution errors during the run — worth a look, not blocking."
        }
        if ($vallyExit -ne 0) {
            Write-Host "##vso[task.logissue type=warning]vally exited $vallyExit after a passing verdict (post-run executor shutdown timeout); shard '$ShardName' is PASSED per results.jsonl."
        }
        Write-Host "##[section]Shard '$ShardName' PASSED (verdict from results.jsonl)."
        exit 0
    }

    Write-Host "##vso[task.logissue type=error]Shard '$ShardName' FAILED — one or more evals are below the pass-rate threshold."
    exit 1
}
