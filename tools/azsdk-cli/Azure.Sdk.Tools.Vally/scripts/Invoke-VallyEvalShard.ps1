#Requires -Version 7.0

<#
.SYNOPSIS
    Runs one Vally eval shard and gates the step on the eval *verdict*, not on
    the `vally` process exit code.

.DESCRIPTION
    `vally` can exit non-zero AFTER computing a passing verdict and writing every
    artifact — a teardown flake ("Timed out while shutting down executors.") must
    not turn a passing shard red. So this script ignores the exit code and reads
    the authoritative `run-summary` record from results.jsonl (see
    Get-VallyShardVerdict). A genuine failure (below threshold, no stimuli ran, or
    no results.jsonl) still exits 1.

    Verdict helpers live in VallyEvalVerdict.ps1 (dot-sourced below) so unit tests
    can exercise them without running `vally`. This runner always executes — do
    NOT add a "skip when dot-sourced" guard: the PowerShell@2 pipeline task invokes
    scripts by dot-sourcing them, so such a guard would skip the real run.

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
    [Parameter(Mandatory)][string]$EvalArgs,
    [Parameter(Mandatory)][string]$ShardName,
    [Parameter(Mandatory)][string]$OutputDir,
    [Parameter(Mandatory)][string]$SkillEvalPrefix,
    [double]$Threshold = 0.8
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'VallyEvalVerdict.ps1')

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
