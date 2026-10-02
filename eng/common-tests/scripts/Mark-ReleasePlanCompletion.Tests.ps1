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
            param([string]$Path, [string]$ReleasePlanId = '100', [string]$SdkPullRequest = '')
            & $script:completionScript -PackageInfoFilePath $Path -AzsdkExePath $script:stubPath -ReleasePlanId $ReleasePlanId -SdkPullRequest $SdkPullRequest
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
        }
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath
    }

    AfterAll {
        Remove-Variable ReleaseStatusTestCalls, ReleaseStatusTestExitCode, LanguageDisplayName -Scope Global -ErrorAction SilentlyContinue
    }

    It 'passes the explicit ID, language, package and optional version without API metadata' {
        Invoke-CompletionTest $script:packageInfoPath

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--release-plan-id' | Should -Be '100'
        Get-CapturedArgument $call '--api-version' | Should -BeNullOrEmpty
        Get-CapturedArgument $call '--package-name' | Should -Be 'azure-test'
        Get-CapturedArgument $call '--language' | Should -Be 'Python'
        Get-CapturedArgument $call '--package-version' | Should -Be '1.2.3'
        Get-CapturedArgument $call '--sdk-release-type' | Should -Be 'stable'
    }

    It 'does not inherit an ID or API version from a previous package build' {
        $script:packageInfo.ReleasePlanId = 999
        $script:packageInfo.ApiVersion = '2026-07-01'
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0

        $global:ReleaseStatusTestCalls.Count | Should -Be 0
    }

    It 'rejects malformed explicit IDs without rounding them to another plan' -TestCases @(
        @{ Value = '-1' }, @{ Value = '100.6' }, @{ Value = '0.9' },
        @{ Value = '2147483648' }, @{ Value = 'not-an-id' }, @{ Value = ' 100' }
    ) {
        param($Value)
        { Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId $Value } | Should -Throw

        $global:ReleaseStatusTestCalls.Count | Should -Be 0
    }

    It 'accepts the largest valid ID without altering its value' {
        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId '2147483647'

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-plan-id' | Should -Be '2147483647'
    }

    It 'passes the triggering SDK PR for automatic ADO lookup without a manual ID' {
        $prUrl = 'https://github.com/Azure/azure-sdk-for-python/pull/100'
        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0 -SdkPullRequest $prUrl

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--sdk-pull-request' | Should -Be $prUrl
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-plan-id' | Should -BeNullOrEmpty
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--api-version' | Should -BeNullOrEmpty
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

    It 'passes each package separately with the triggering SDK PR instead of inherited IDs' {
        @{
            Name = 'azure-other'
            Version = '2.0.0'
            ReleasePlanId = 200
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $script:caseDirectory 'other.json')

        Invoke-CompletionTest $script:caseDirectory -ReleasePlanId 0 -SdkPullRequest 'https://github.com/Azure/azure-sdk-for-python/pull/100'

        $global:ReleaseStatusTestCalls.Count | Should -Be 2
        $identities = @($global:ReleaseStatusTestCalls | ForEach-Object {
            '{0}|{1}|{2}' -f (Get-CapturedArgument $_ '--package-name'), (Get-CapturedArgument $_ '--release-plan-id'), (Get-CapturedArgument $_ '--sdk-pull-request')
        })
        $identities | Should -Contain 'azure-test||https://github.com/Azure/azure-sdk-for-python/pull/100'
        $identities | Should -Contain 'azure-other||https://github.com/Azure/azure-sdk-for-python/pull/100'
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

    It 'declares and forwards both correlation inputs in the shared completion template' {
        $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../common/pipelines/templates/steps/mark-release-completion.yml') -Raw
        $template | Should -Match 'ReleasePlanId: 0'
        $template | Should -Match "SdkPullRequest: ''"
        $template | Should -Match '-ReleasePlanId.+parameters\.ReleasePlanId'
        $template | Should -Match '-SdkPullRequest.+parameters\.SdkPullRequest'
        $template | Should -Not -Match 'ApiVersion|api-version'
    }
}
