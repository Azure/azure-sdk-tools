# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

Describe 'Mark-ReleasePlanCompletion' -Tag 'UnitTest' {
    BeforeAll {
        $script:completionScript = Join-Path $PSScriptRoot '../../common/scripts/Mark-ReleasePlanCompletion.ps1'
        $script:buildEnvironment = @{}
        foreach ($name in @('TF_BUILD', 'SYSTEM_COLLECTIONURI', 'SYSTEM_TEAMPROJECT', 'BUILD_BUILDID')) {
            $script:buildEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }

        $script:stubPath = Join-Path $TestDrive 'azsdk-stub.ps1'
        $stubLines = @(
            'param([Parameter(ValueFromRemainingArguments = $true)][string[]]$CliArgs)',
            '$global:ReleaseStatusTestCalls += ,$CliArgs',
            '$global:LASTEXITCODE = $global:ReleaseStatusTestExitCode',
            'return "release-status-test-result"'
        )
        Set-Content -LiteralPath $script:stubPath -Value ($stubLines -join [Environment]::NewLine)

        function Invoke-CompletionTest {
            param([string]$Path, [string]$ReleasePlanId = '100', [string]$SdkPullRequest = '', [string]$PipelineUrl = '')
            $pipelineArgs = @{}
            if ($PipelineUrl) { $pipelineArgs['PipelineUrl'] = $PipelineUrl }
            & $script:completionScript -PackageInfoFilePath $Path -AzsdkExePath $script:stubPath -ReleasePlanId $ReleasePlanId -SdkPullRequest $SdkPullRequest @pipelineArgs
        }

        function Get-CapturedArgument {
            param([string[]]$Call, [string]$Name)
            $index = [Array]::IndexOf($Call, $Name)
            if ($index -lt 0) { return $null }
            return $Call[$index + 1]
        }
    }

    BeforeEach {
        foreach ($name in $script:buildEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $null)
        }
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
        foreach ($name in $script:buildEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:buildEnvironment[$name])
        }
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

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--release-plan-id' | Should -BeNullOrEmpty
        Get-CapturedArgument $call '--api-version' | Should -BeNullOrEmpty
    }

    It 'uses the legacy package lookup with informational logging when neither correlation input is supplied' {
        $messages = @(Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0 3>&1 6>&1)

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--package-name' | Should -Be 'azure-test'
        Get-CapturedArgument $call '--language' | Should -Be 'Python'
        Get-CapturedArgument $call '--status' | Should -Be 'Released'
        Get-CapturedArgument $call '--package-version' | Should -Be '1.2.3'
        Get-CapturedArgument $call '--sdk-release-type' | Should -Be 'stable'
        Get-CapturedArgument $call '--release-plan-id' | Should -BeNullOrEmpty
        Get-CapturedArgument $call '--sdk-pull-request' | Should -BeNullOrEmpty
        @($messages | Where-Object { $_ -is [System.Management.Automation.WarningRecord] }).Count | Should -Be 0
        ($messages | Out-String) | Should -Match 'LEGACY_RELEASE_PLAN_LOOKUP'
    }

    It 'updates status without optional version fields for <Kind> metadata on the <Lookup> path' -TestCases @(
        foreach ($metadata in @(
            @{ Kind = 'missing'; Version = $null },
            @{ Kind = 'non-string'; Version = 123 },
            @{ Kind = 'unparseable'; Version = 'not-a-version' }
        )) {
            foreach ($lookup in @(
                @{ Lookup = 'manual'; ReleasePlanId = '100'; SdkPullRequest = '' },
                @{ Lookup = 'automatic'; ReleasePlanId = '0'; SdkPullRequest = 'https://github.com/Azure/azure-sdk-for-python/pull/100' },
                @{ Lookup = 'legacy'; ReleasePlanId = '0'; SdkPullRequest = '' }
            )) {
                $metadata + $lookup
            }
        }
    ) {
        param($Kind, $Version, $Lookup, $ReleasePlanId, $SdkPullRequest)
        $script:packageInfo.Version = $Version
        $script:packageInfo | ConvertTo-Json | Set-Content -LiteralPath $script:packageInfoPath

        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId $ReleasePlanId -SdkPullRequest $SdkPullRequest

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--status' | Should -Be 'Released'
        Get-CapturedArgument $call '--package-version' | Should -BeNullOrEmpty
        Get-CapturedArgument $call '--sdk-release-type' | Should -BeNullOrEmpty
        if ($ReleasePlanId -ne '0') {
            Get-CapturedArgument $call '--release-plan-id' | Should -Be $ReleasePlanId
        }
        [string](Get-CapturedArgument $call '--sdk-pull-request') | Should -Be $SdkPullRequest
    }

    It 'forwards the release pipeline URL for the <Lookup> path' -TestCases @(
        @{ Lookup = 'manual'; ReleasePlanId = '100'; SdkPullRequest = '' },
        @{ Lookup = 'automatic'; ReleasePlanId = '0'; SdkPullRequest = 'https://github.com/Azure/azure-sdk-for-python/pull/100' },
        @{ Lookup = 'legacy'; ReleasePlanId = '0'; SdkPullRequest = '' }
    ) {
        param($Lookup, $ReleasePlanId, $SdkPullRequest)
        $pipelineUrl = 'https://dev.azure.com/azure-sdk/internal/_build/results?buildId=12345'

        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId $ReleasePlanId -SdkPullRequest $SdkPullRequest -PipelineUrl $pipelineUrl

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-pipeline' | Should -Be $pipelineUrl
    }

    It 'passes the current run URL when invoked directly without template arguments' {
        $env:TF_BUILD = 'True'
        $env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
        $env:SYSTEM_TEAMPROJECT = 'sdk-project'
        $env:BUILD_BUILDID = '12345'

        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0

        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-pipeline' | Should -Be 'https://dev.azure.com/example/sdk-project/_build/results?buildId=12345'
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-plan-id' | Should -BeNullOrEmpty
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--sdk-pull-request' | Should -BeNullOrEmpty
    }

    It 'preserves an explicitly supplied run URL instead of replacing it with environment context' {
        $env:TF_BUILD = 'True'
        $env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
        $env:SYSTEM_TEAMPROJECT = 'sdk-project'
        $env:BUILD_BUILDID = '12345'

        Invoke-CompletionTest $script:packageInfoPath -PipelineUrl 'https://example.test/build/100'

        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-pipeline' | Should -Be 'https://example.test/build/100'
    }

    It 'does not construct a run URL with missing or invalid <Field> context' -TestCases @(
        @{ Field = 'TF_BUILD'; Value = '' },
        @{ Field = 'SYSTEM_COLLECTIONURI'; Value = '' },
        @{ Field = 'SYSTEM_TEAMPROJECT'; Value = '' },
        @{ Field = 'BUILD_BUILDID'; Value = '' },
        @{ Field = 'BUILD_BUILDID'; Value = '$(Build.BuildId)' }
    ) {
        param($Field, $Value)
        $env:TF_BUILD = 'True'
        $env:SYSTEM_COLLECTIONURI = 'https://dev.azure.com/example/'
        $env:SYSTEM_TEAMPROJECT = 'sdk-project'
        $env:BUILD_BUILDID = '12345'
        [Environment]::SetEnvironmentVariable($Field, $Value)

        Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0

        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--release-pipeline' | Should -BeNullOrEmpty
    }

    It 'forwards the SDK PR without a legacy warning when no plan ID is supplied' {
        $pr = 'https://github.com/Azure/azure-sdk-for-python/pull/100'
        $warnings = @(Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId 0 -SdkPullRequest $pr 3>&1 | Where-Object { $_ -is [System.Management.Automation.WarningRecord] })

        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        Get-CapturedArgument $global:ReleaseStatusTestCalls[0] '--sdk-pull-request' | Should -Be $pr
        ($warnings | ForEach-Object { $_.Message }) -join ' ' | Should -Not -Match 'LEGACY_RELEASE_PLAN_LOOKUP'
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

    It 'logs a failed status update as information without retrying or warning on the <Lookup> path' -TestCases @(
        @{ Lookup = 'manual'; ReleasePlanId = '100'; SdkPullRequest = '' },
        @{ Lookup = 'automatic'; ReleasePlanId = '0'; SdkPullRequest = 'https://github.com/Azure/azure-sdk-for-python/pull/100' },
        @{ Lookup = 'legacy'; ReleasePlanId = '0'; SdkPullRequest = '' }
    ) {
        param($Lookup, $ReleasePlanId, $SdkPullRequest)
        $global:ReleaseStatusTestExitCode = 1

        $messages = @(Invoke-CompletionTest $script:packageInfoPath -ReleasePlanId $ReleasePlanId -SdkPullRequest $SdkPullRequest 3>&1 6>&1)

        $LASTEXITCODE | Should -Be 0
        $global:ReleaseStatusTestCalls.Count | Should -Be 1
        @($messages | Where-Object { $_ -is [System.Management.Automation.WarningRecord] }).Count | Should -Be 0
        ($messages | Out-String) | Should -Match 'Exit code: 1'
        $call = $global:ReleaseStatusTestCalls[0]
        Get-CapturedArgument $call '--package-name' | Should -Be 'azure-test'
        Get-CapturedArgument $call '--language' | Should -Be 'Python'
        if ($ReleasePlanId -ne '0') {
            Get-CapturedArgument $call '--release-plan-id' | Should -Be $ReleasePlanId
        }
        if ($SdkPullRequest) {
            Get-CapturedArgument $call '--sdk-pull-request' | Should -Be $SdkPullRequest
        }
    }

    It 'declares and forwards both correlation inputs in the shared completion template' {
        $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../common/pipelines/templates/steps/mark-release-completion.yml') -Raw
        $template | Should -Match 'ReleasePlanId: 0'
        $template | Should -Match "SdkPullRequest: ''"
        $template | Should -Match '-ReleasePlanId.+parameters\.ReleasePlanId'
        $template | Should -Match '-SdkPullRequest.+parameters\.SdkPullRequest'
        $template | Should -Match ([regex]::Escape('-PipelineUrl "$(System.CollectionUri)$(System.TeamProject)/_build/results?buildId=$(Build.BuildId)"'))
        $template | Should -Not -Match 'ApiVersion|api-version'
    }
}

Describe 'Optional auto-release SDK PR in the shared completion template' -Tag 'UnitTest' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '../../common/scripts/common.ps1')
        $templatePath = Join-Path $PSScriptRoot '../../common/pipelines/templates/steps/mark-release-completion.yml'
        $script:completionTemplate = CompatibleConvertFrom-Yaml -Content (Get-Content -LiteralPath $templatePath -Raw)
        $script:optionalPrGuard = '${{ if eq(parameters.SdkPullRequest, '''') }}'
    }

    It 'keeps an empty parameter default and initializes only a missing runtime variable before use' {
        $completionTemplate.parameters.SdkPullRequest | Should -BeExactly ''
        $guardedSteps = @($completionTemplate.steps | Where-Object { $_.Contains($optionalPrGuard) })
        $guardedSteps.Count | Should -Be 1
        $initialize = $guardedSteps[0][$optionalPrGuard][0]
        $initialize.condition | Should -BeExactly "and(succeeded(), ne(variables['Skip.MarkReleaseCompletion'], 'true'), eq(variables['AutoReleaseSdkPullRequestUrl'], ''))"
        $initialize.pwsh | Should -Not -Match '\$\(AutoReleaseSdkPullRequestUrl\)'
        $completionTemplate.steps[-2].Contains($optionalPrGuard) | Should -BeTrue
        $completionTemplate.steps[-1].task | Should -BeExactly 'AzureCLI@2'
        $completionTemplate.steps[-1].inputs.arguments | Should -Match ([regex]::Escape('${{ coalesce(parameters.SdkPullRequest, ''$(AutoReleaseSdkPullRequestUrl)'') }}'))
    }

    It 'passes <Case> without an unresolved variable or replacing an existing PR' -TestCases @(
        @{ Case = 'manual missing variable'; Variable = $null; Explicit = ''; Expected = ''; Initialized = $true },
        @{ Case = 'manual empty variable'; Variable = ''; Explicit = ''; Expected = ''; Initialized = $true },
        @{ Case = 'automatic runtime output'; Variable = 'https://github.com/Azure/azure-sdk-for-python/pull/100'; Explicit = ''; Expected = 'https://github.com/Azure/azure-sdk-for-python/pull/100'; Initialized = $false },
        @{ Case = 'explicit PR with missing variable'; Variable = $null; Explicit = 'https://github.com/Azure/azure-sdk-for-python/pull/200'; Expected = 'https://github.com/Azure/azure-sdk-for-python/pull/200'; Initialized = $false },
        @{ Case = 'explicit PR overriding runtime output'; Variable = 'https://github.com/Azure/azure-sdk-for-python/pull/100'; Explicit = 'https://github.com/Azure/azure-sdk-for-python/pull/200'; Expected = 'https://github.com/Azure/azure-sdk-for-python/pull/200'; Initialized = $false }
    ) {
        param($Case, $Variable, $Explicit, $Expected, $Initialized)
        $guardedSteps = @($completionTemplate.steps | Where-Object { $_.Contains($optionalPrGuard) })
        $guardedSteps.Count | Should -Be 1
        $initialize = $guardedSteps[0][$optionalPrGuard][0]
        $arguments = $completionTemplate.steps[-1].inputs.arguments
        $arguments | Should -Match ([regex]::Escape('${{ coalesce(parameters.SdkPullRequest, ''$(AutoReleaseSdkPullRequestUrl)'') }}'))

        # Model the documented runtime condition, then execute the actual initializer.
        # This is a contract test, not an implementation of Azure's YAML engine.
        $variables = @{}
        if ($null -ne $Variable) { $variables.AutoReleaseSdkPullRequestUrl = $Variable }
        $shouldInitialize = $Explicit -eq '' -and [string]$variables['AutoReleaseSdkPullRequestUrl'] -eq ''
        $shouldInitialize | Should -Be $Initialized
        if ($shouldInitialize) {
            $messages = & ([scriptblock]::Create($initialize.pwsh)) 6>&1
            ($messages | Out-String).Trim() | Should -BeExactly '##vso[task.setvariable variable=AutoReleaseSdkPullRequestUrl]'
            $variables.AutoReleaseSdkPullRequestUrl = ''
        }
        $resolved = if ($Explicit -ne '') { $Explicit } else { $variables['AutoReleaseSdkPullRequestUrl'] }
        $resolved | Should -BeExactly $Expected
        $resolved | Should -Not -Match '\$\(AutoReleaseSdkPullRequestUrl\)'
    }
}
