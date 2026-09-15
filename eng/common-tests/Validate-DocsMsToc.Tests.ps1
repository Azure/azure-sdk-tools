#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }
<#
Pester tests for Validate-DocsMsToc.ps1.

Run with:
  Invoke-Pester ./eng/common-tests/Validate-DocsMsToc.Tests.ps1
#>

BeforeAll {
    $script:ScriptPath  = Resolve-Path "$PSScriptRoot/../common/scripts/Validate-DocsMsToc.ps1"
    $script:FixturesDir = Resolve-Path "$PSScriptRoot/Validate-DocsMsToc.Fixtures"

    function Invoke-Validator {
        param(
            [string]   $TocFile,
            [string[]] $ExtraArgs = @()
        )
        $tocPath = Join-Path $script:FixturesDir $TocFile
        $output  = & pwsh -NoProfile -File $script:ScriptPath -TocPath $tocPath @ExtraArgs 2>&1
        return [PSCustomObject]@{
            ExitCode = $LASTEXITCODE
            Output   = ($output -join "`n")
        }
    }
}

Describe 'Validate-DocsMsToc' {

    Context 'happy path' {
        It 'passes a well-formed ToC' {
            $r = Invoke-Validator -TocFile 'good.yml'
            $r.ExitCode | Should -Be 0
            $r.Output   | Should -Match 'Validate-DocsMsToc: OK'
        }
    }

    Context 'structural-collapse detection (the 783700739 symptom)' {
        It 'fails a ToC whose only top-level node is Other' {
            $r = Invoke-Validator -TocFile 'bad-only-other.yml'
            $r.ExitCode | Should -Be 1
            $r.Output   | Should -Match 'TOC040|TOC041'
        }
    }

    Context 'children-shape detection (the 783700739 root cause)' {
        It 'fails a ToC whose children items are mappings, not scalars' {
            $r = Invoke-Validator -TocFile 'bad-children-mapping.yml'
            $r.ExitCode | Should -Be 1
            $r.Output   | Should -Match 'TOC010|TOC011'
        }
    }

    Context 'schema sanity' {
        It 'fails a ToC with an empty service name' {
            $r = Invoke-Validator -TocFile 'bad-empty-name.yml'
            $r.ExitCode | Should -Be 1
            $r.Output   | Should -Match 'TOC030'
        }
    }

    Context '-SoftFail mode' {
        It 'reports violations but exits 0 with -SoftFail' {
            $r = Invoke-Validator -TocFile 'bad-only-other.yml' -ExtraArgs @('-SoftFail')
            $r.ExitCode | Should -Be 0
            $r.Output   | Should -Match 'SoftFail'
        }
    }

    Context 'drift detection' {
        It 'fails when leaf-node count drops beyond threshold' {
            $tocPath  = Join-Path $script:FixturesDir 'bad-only-other.yml'
            $prevPath = Join-Path $script:FixturesDir 'good.yml'
            $output = & pwsh -NoProfile -File $script:ScriptPath `
                -TocPath $tocPath `
                -PreviousTocPath $prevPath `
                -MaxNodeCountDropPercent 10 2>&1
            $LASTEXITCODE | Should -Be 1
            ($output -join "`n") | Should -Match 'TOC050'
        }

        It 'passes when leaf-node count drop is within threshold' {
            $tocPath  = Join-Path $script:FixturesDir 'good.yml'
            $prevPath = Join-Path $script:FixturesDir 'good.yml'
            $output = & pwsh -NoProfile -File $script:ScriptPath `
                -TocPath $tocPath `
                -PreviousTocPath $prevPath `
                -MaxNodeCountDropPercent 10 2>&1
            $LASTEXITCODE | Should -Be 0
        }
    }
}
