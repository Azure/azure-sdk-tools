# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe 'Mark-ReleasePlanCompletion' -Tag 'UnitTest' {
    BeforeAll {
        $script:completionScript = Join-Path $PSScriptRoot '../../common/scripts/Mark-ReleasePlanCompletion.ps1'

        $script:stubPath = Join-Path $TestDrive 'azsdk-stub.ps1'
        $stubLines = @(
            'param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CliArgs)',
            '$global:ReleaseStatusTestCalls += ,$CliArgs',
            '$global:LASTEXITCODE = $global:ReleaseStatusTestExitCode',
            'return "release-status-test-result"'
        )
        Set-Content -LiteralPath $script:stubPath -Value ($stubLines -join [Environment]::NewLine)

        function Invoke-CompletionTest {
            param([string]$Path)
            & $script:completionScript -PackageInfoFilePath $Path -AzsdkExePath $script:stubPath
        }

        function Get-CapturedArgument {
            param([string[]]$Call, [string]$Name)
            $index = [Array]::IndexOf($Call, $Name)
            if ($index -lt 0) { return $null }
            return $Call[$index + 1]
        }
    }

    BeforeEach {
        $global:ReleaseStatusTestCalls = @()
        $global:ReleaseStatusTestExitCode = 0
        $global:LanguageDisplayName = 'Python'
        $script:caseDirectory = Join-Path $TestDrive ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:caseDirectory | Out-Null
        $script:packageInfoPath = Join-Path $script:caseDirectory 'azure-test.json'
        $script:packageInfo = @{
            Name = 'azure-test'
            Version = '1.2.3'
            ReleasePlanId = 100
            ApiVersion = '2026-07-01'
        }
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath
    }

    AfterAll {
        Remove-Variable ReleaseStatusTestCalls, ReleaseStatusTestExitCode, LanguageDisplayName -Scope Global -ErrorAction SilentlyContinue
    }

    It 'passes per-package ID, language, name, API version and optional version' {
        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--release-plan-id' | Should -Be '100'
        Get-CapturedArgument $call '--api-version' | Should -Be '2026-07-01'
        Get-CapturedArgument $call '--package-name' | Should -Be 'azure-test'
        Get-CapturedArgument $call '--language' | Should -Be 'Python'
        Get-CapturedArgument $call '--package-version' | Should -Be '1.2.3'
        Get-CapturedArgument $call '--sdk-release-type' | Should -Be 'stable'
    }

    It 'does not guess a release plan when the ID is missing, empty or zero' -TestCases @(
        @{ Value = $null }, @{ Value = '' }, @{ Value = 0 }
    ) {
        param($Value)
        $script:packageInfo.ReleasePlanId = $Value
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 0
    }

    It 'does not invoke the CLI for a malformed ID' -TestCases @(
        @{ Value = -1 }, @{ Value = 'invalid' }, @{ Value = @(100, 200) }, @{ Value = '2147483648' }, @{ Value = 100.5 }
    ) {
        param($Value)
        $script:packageInfo.ReleasePlanId = $Value
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 0
    }

    It 'does not invoke the CLI for absent or unresolved multiple API versions' -TestCases @(
        @{ Value = $null }, @{ Value = '' }, @{ Value = @('2026-07-01', '2026-08-01') }, @{ Value = @('2026-07-01') }, @{ Value = 123 }
    ) {
        param($Value)
        $script:packageInfo.ApiVersion = $Value
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 0
    }

    It 'does not require optional package version metadata' {
        $script:packageInfo.Remove('Version')
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--package-version' | Should -BeNullOrEmpty
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--sdk-release-type' | Should -BeNullOrEmpty
    }

    It 'forwards the beta classification for prerelease versions' {
        $script:packageInfo.Version = '1.2.3b1'
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--sdk-release-type' | Should -Be 'beta'
    }

    It 'keeps correlation isolated for multiple packages, including a package with no plan' {
        @{
            Name = 'azure-other'
            Version = '2.0.0'
            ReleasePlanId = 200
            ApiVersion = '2026-08-01'
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $script:caseDirectory 'other.json')
        @{ Name = 'azure-unplanned'; Version = '1.0.1' } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $script:caseDirectory 'unplanned.json')

        Invoke-CompletionTest $script:caseDirectory

        $global:ReleaseStatusTestCalls.Count | Should -Be 2
        $identities = @($global:ReleaseStatusTestCalls | ForEach-Object {
            '{0}|{1}|{2}' -f (Get-CapturedArgument $_ '--package-name'), (Get-CapturedArgument $_ '--release-plan-id'), (Get-CapturedArgument $_ '--api-version')
        })
        $identities | Should -Contain 'azure-test|100|2026-07-01'
        $identities | Should -Contain 'azure-other|200|2026-08-01'
    }

    It 'continues after malformed JSON without attempting an uncorrelated update' {
        Set-Content -LiteralPath (Join-Path $script:caseDirectory '0-invalid.json') -Value 'not json'

        { Invoke-CompletionTest $script:caseDirectory } | Should -Not -Throw

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
    }

    It 'does not turn a status-command failure into a package-publication failure' {
        $global:ReleaseStatusTestExitCode = 1

        { Invoke-CompletionTest $script:packageInfoPath } | Should -Not -Throw

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
    }
}
