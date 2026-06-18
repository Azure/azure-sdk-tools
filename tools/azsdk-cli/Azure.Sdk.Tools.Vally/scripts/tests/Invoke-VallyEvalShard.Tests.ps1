#Requires -Version 7.0
#Requires -Modules Pester

# Pester tests for the Vally eval shard verdict gating.
# Run from this directory:  Invoke-Pester
#
# We dot-source VallyEvalVerdict.ps1 (the pure helpers) directly, so no `vally`
# run is triggered. The runner Invoke-VallyEvalShard.ps1 intentionally has no
# "skip when dot-sourced" guard (the PowerShell@2 task dot-sources its scripts),
# so it must NOT be dot-sourced here.

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'VallyEvalVerdict.ps1')

    $script:root = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-shard-test-" + [Guid]::NewGuid())

    function New-RunSummary {
        param([string]$Shard, [string]$Jsonl)
        # Mimic Vally's nested per-run timestamp folder under the shard output dir.
        $dir = Join-Path $script:root $Shard '2026-06-18T04-27-19-656Z'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path (Join-Path $dir 'results.jsonl') -Value $Jsonl -Encoding utf8
        return (Join-Path $script:root $Shard)
    }
}

AfterAll {
    if (Test-Path $script:root) {
        Remove-Item -Path $script:root -Recurse -Force
    }
}

Describe 'Get-VallyShardVerdict' {

    It 'passes a scored eval above the threshold' {
        $dir = New-RunSummary 'above' @'
{"type":"run-summary","passed":true,"hadExecutionErrors":false,"evals":[{"name":"e","passed":true,"scoringApplied":true,"overallScore":0.971,"threshold":0.8,"stimuliRun":7,"stimuliTotal":7}]}
'@
        $v = Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8
        $v.Found | Should -BeTrue
        $v.Passed | Should -BeTrue
    }

    It 'passes a scored eval exactly on the threshold boundary' {
        $dir = New-RunSummary 'boundary' @'
{"type":"run-summary","passed":true,"hadExecutionErrors":false,"evals":[{"name":"e","passed":true,"scoringApplied":true,"overallScore":0.8,"threshold":0.8,"stimuliRun":5,"stimuliTotal":5}]}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Passed | Should -BeTrue
    }

    It 'passes when the verdict cleared the threshold but the run had execution errors' {
        # The 97.1% case: score over threshold, but Vally flagged execution errors
        # (which would flip its own `passed` to false). We gate on the pass rate.
        $dir = New-RunSummary 'execerrors' @'
{"type":"run-summary","passed":false,"hadExecutionErrors":true,"evals":[{"name":"e","passed":false,"scoringApplied":true,"overallScore":0.971,"threshold":0.8,"stimuliRun":7,"stimuliTotal":7}]}
'@
        $v = Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8
        $v.Passed | Should -BeTrue
        $v.HadExecutionErrors | Should -BeTrue
    }

    It 'fails a scored eval below the threshold' {
        $dir = New-RunSummary 'below' @'
{"type":"run-summary","passed":false,"hadExecutionErrors":false,"evals":[{"name":"e","passed":false,"scoringApplied":true,"overallScore":0.6,"threshold":0.8,"stimuliRun":5,"stimuliTotal":5}]}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Passed | Should -BeFalse
    }

    It 'fails a scored eval that cleared the threshold but ran zero stimuli (no vacuous pass)' {
        $dir = New-RunSummary 'norun' @'
{"type":"run-summary","passed":false,"hadExecutionErrors":false,"evals":[{"name":"e","passed":false,"scoringApplied":true,"overallScore":1.0,"threshold":0.8,"stimuliRun":0,"stimuliTotal":3}]}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Passed | Should -BeFalse
    }

    It 'honours a binary (unscored) eval verdict' {
        $dir = New-RunSummary 'binary' @'
{"type":"run-summary","passed":true,"hadExecutionErrors":false,"evals":[{"name":"e","passed":true,"scoringApplied":false,"stimuliRun":2,"stimuliTotal":2}]}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Passed | Should -BeTrue
    }

    It 'fails the shard when any eval in a multi-eval shard is below threshold' {
        $dir = New-RunSummary 'mixed' @'
{"type":"run-summary","passed":false,"hadExecutionErrors":false,"evals":[{"name":"ok","passed":true,"scoringApplied":true,"overallScore":1.0,"threshold":0.8,"stimuliRun":3,"stimuliTotal":3},{"name":"bad","passed":false,"scoringApplied":true,"overallScore":0.5,"threshold":0.8,"stimuliRun":4,"stimuliTotal":4}]}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Passed | Should -BeFalse
    }

    It 'reports not-found when there is no results.jsonl' {
        $dir = Join-Path $script:root 'empty'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $v = Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8
        $v.Found | Should -BeFalse
        $v.Passed | Should -BeFalse
    }

    It 'reports not-found when results.jsonl has no run-summary record' {
        $dir = New-RunSummary 'nosummary' @'
{"type":"trial","name":"x"}
'@
        (Get-VallyShardVerdict -ResultsDir $dir -Threshold 0.8).Found | Should -BeFalse
    }

    It 'uses the newest results.jsonl when several runs exist' {
        $shardDir = New-RunSummary 'multi' @'
{"type":"run-summary","passed":false,"hadExecutionErrors":false,"evals":[{"name":"e","passed":false,"scoringApplied":true,"overallScore":0.5,"threshold":0.8,"stimuliRun":4,"stimuliTotal":4}]}
'@
        Start-Sleep -Milliseconds 50
        $newer = Join-Path $shardDir '2026-06-18T05-00-00-000Z'
        New-Item -ItemType Directory -Path $newer -Force | Out-Null
        Set-Content -Path (Join-Path $newer 'results.jsonl') -Encoding utf8 -Value @'
{"type":"run-summary","passed":true,"hadExecutionErrors":false,"evals":[{"name":"e","passed":true,"scoringApplied":true,"overallScore":1.0,"threshold":0.8,"stimuliRun":4,"stimuliTotal":4}]}
'@
        (Get-VallyShardVerdict -ResultsDir $shardDir -Threshold 0.8).Passed | Should -BeTrue
    }
}
