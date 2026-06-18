#Requires -Version 7.0
#Requires -Modules Pester

# Pester tests for New-EvalSummary.ps1.
# Run from this directory:  Invoke-Pester

BeforeAll {
    $script:scriptPath = Join-Path $PSScriptRoot '..' 'New-EvalSummary.ps1'

    # Build a throwaway results tree mimicking downloaded shard artifacts:
    #   <root>/eval-result-<shardName>/junit.xml
    $script:root = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-summary-test-" + [Guid]::NewGuid())

    function New-Junit {
        param([string]$Shard, [string]$Xml)
        $dir = Join-Path $script:root "eval-result-$Shard"
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path (Join-Path $dir 'junit.xml') -Value $Xml -Encoding utf8
    }

    # area_github: 2 pass.
    New-Junit -Shard 'area_github' -Xml @'
<testsuites>
  <testsuite name="github">
    <testcase name="lists issues" time="1.2" />
    <testcase name="creates pr" time="0.8" />
  </testsuite>
</testsuites>
'@

    # area_typespec: 1 pass, 1 fail, 1 skip.
    New-Junit -Shard 'area_typespec' -Xml @'
<testsuites>
  <testsuite name="typespec">
    <testcase name="adds arm resource" time="2.0" />
    <testcase name="renames client" time="1.5"><failure message="assertion failed">expected tool call</failure></testcase>
    <testcase name="generation step 2" time="0.0"><skipped /></testcase>
  </testsuite>
</testsuites>
'@

    # area_multitrial: two stimuli, each run 5 times (one <testcase> per trial,
    # named '<stimulus> (trial N)'), gated at threshold 0.8. 'flaky pass' goes
    # 4/5 (>= 0.8 -> passes); 'flaky fail' goes 2/5 (< 0.8 -> fails). Proves the
    # summary collapses trials to stimuli and applies the gate, instead of
    # counting 10 raw testcases.
    New-Junit -Shard 'area_multitrial' -Xml @'
<testsuites>
  <testsuite name="multitrial">
    <properties><property name="threshold" value="0.8" /></properties>
    <testcase name="flaky pass (trial 1)" time="1.0" />
    <testcase name="flaky pass (trial 2)" time="1.0" />
    <testcase name="flaky pass (trial 3)" time="1.0" />
    <testcase name="flaky pass (trial 4)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky pass (trial 5)" time="1.0" />
    <testcase name="flaky fail (trial 1)" time="1.0" />
    <testcase name="flaky fail (trial 2)" time="1.0" />
    <testcase name="flaky fail (trial 3)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky fail (trial 4)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky fail (trial 5)" time="1.0"><failure message="x">nope</failure></testcase>
  </testsuite>
</testsuites>
'@
}

AfterAll {
    if (Test-Path $script:root) {
        Remove-Item -Path $script:root -Recurse -Force
    }
}

Describe 'New-EvalSummary.ps1' {
    BeforeEach {
        $script:outFile = Join-Path $script:root ("summary-" + [Guid]::NewGuid() + '.md')
    }

    It 'aggregates pass/fail/skip per shard' {
        $shards = & $script:scriptPath -ResultsRoot $script:root -OutputPath $script:outFile
        $shards['area_github'].total | Should -Be 2
        $shards['area_github'].failed | Should -Be 0
        $shards['area_typespec'].total | Should -Be 3
        $shards['area_typespec'].failed | Should -Be 1
        $shards['area_typespec'].skipped | Should -Be 1
    }

    It 'captures the failing scenario name' {
        $shards = & $script:scriptPath -ResultsRoot $script:root -OutputPath $script:outFile
        $shards['area_typespec'].failures | Should -Contain 'renames client (0/1 runs passed)'
    }

    It 'collapses per-trial testcases to one stimulus and applies the threshold' {
        $shards = & $script:scriptPath -ResultsRoot $script:root -OutputPath $script:outFile
        # 10 raw <testcase> trials -> 2 stimuli, not 10.
        $shards['area_multitrial'].total | Should -Be 2
        # 4/5 >= 0.8 passes; 2/5 < 0.8 fails.
        $shards['area_multitrial'].failed | Should -Be 1
        $shards['area_multitrial'].failures | Should -Contain 'flaky fail (2/5 runs passed)'
        $shards['area_multitrial'].failures | Should -Not -Contain 'flaky pass (4/5 runs passed)'
    }

    It 'writes a Markdown file with an overall FAILED header when any shard is red' {
        & $script:scriptPath -ResultsRoot $script:root -OutputPath $script:outFile | Out-Null
        $md = Get-Content -LiteralPath $script:outFile -Raw
        $md | Should -Match '## .* Vally eval results — FAILED'
        $md | Should -Match '\| area_github \| .* \| 2 \| 0 \| 0 \|'
        $md | Should -Match 'Failing scenarios'
        $md | Should -Match 'renames client'
    }

    It 'reports PASSED when no shard has failures' {
        $passRoot = Join-Path $script:root 'pass-only'
        $dir = Join-Path $passRoot 'eval-result-area_github'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path (Join-Path $dir 'junit.xml') -Encoding utf8 -Value @'
<testsuites><testsuite name="github"><testcase name="ok" time="0.1" /></testsuite></testsuites>
'@
        & $script:scriptPath -ResultsRoot $passRoot -OutputPath $script:outFile | Out-Null
        (Get-Content -LiteralPath $script:outFile -Raw) | Should -Match '## .* Vally eval results — PASSED'
    }

    It 'reports NO RESULTS (not PASSED) when an XML has zero testcases' {
        $emptyRoot = Join-Path $script:root 'empty-results'
        $dir = Join-Path $emptyRoot 'eval-result-area_empty'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path (Join-Path $dir 'junit.xml') -Encoding utf8 -Value @'
<testsuites><testsuite name="empty"></testsuite></testsuites>
'@
        & $script:scriptPath -ResultsRoot $emptyRoot -OutputPath $script:outFile | Out-Null
        $md = Get-Content -LiteralPath $script:outFile -Raw
        $md | Should -Match '## .* Vally eval results — NO RESULTS'
        $md | Should -Not -Match 'results — PASSED'
        $md | Should -Match 'No eval testcases were found'
    }

    It 'falls back to a meaningful shard name when not under eval-result-*' {
        $fbRoot = Join-Path $script:root 'fallback'
        $dir = Join-Path $fbRoot '_unit5' '2026-06-17T23-53-02-457Z'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-Content -Path (Join-Path $dir 'eval-results.junit.xml') -Encoding utf8 -Value @'
<testsuites><testsuite name="x"><testcase name="ok" time="0.1" /></testsuite></testsuites>
'@
        $shards = & $script:scriptPath -ResultsRoot $fbRoot -OutputPath $script:outFile
        $shards.Contains('_unit5') | Should -BeTrue
        $shards.Contains('unknown') | Should -BeFalse
    }
}
