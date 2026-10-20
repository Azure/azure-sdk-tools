Import-Module Pester

Describe 'Local reference documentation validation' -Tag 'UnitTest' {
    BeforeAll {
        $script:validationScript = Join-Path $PSScriptRoot '../../common/scripts/Validate-DocsMsPackages.ps1'

        function Validate-Unknown-LocalDocsMsPackage {
            param($PackageInfo, $ArtifactRoot, $OutputDirectory)
            throw 'The test must mock the local validator.'
        }

        function Set-TestPackageInfo {
            param(
                [string]$FileName = 'package.json',
                [hashtable]$Properties = @{ Name = 'azure-test'; Version = '1.2.3'; Group = 'com.azure' }
            )
            $Properties | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $script:packageInfoDirectory $FileName)
        }

        function Invoke-TestValidation {
            & $script:validationScript `
                -PackageInfoDirectory $script:packageInfoDirectory `
                -ArtifactRoot $script:artifactRoot `
                -OutputDirectory $script:outputDirectory
        }
    }

    BeforeEach {
        $script:artifactRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString() + ' packages')
        $script:packageInfoDirectory = Join-Path $script:artifactRoot 'PackageInfo'
        $script:outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString() + ' output')
        $null = New-Item -ItemType Directory -Path $script:packageInfoDirectory -Force

        Mock Validate-Unknown-LocalDocsMsPackage {
            return [pscustomobject]@{ Status = 'Passed'; Reason = '' }
        }
    }

    It 'passes the exact build metadata and resolved artifact paths to the local validator' {
        Set-TestPackageInfo -Properties @{
            Name = 'azure-test'
            Version = '1.2.3'
            DevVersion = '1.2.4-beta.1'
            Group = 'com.azure'
        }

        { Invoke-TestValidation } | Should -Not -Throw

        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 1 -Exactly -ParameterFilter {
            $PackageInfo.Name -eq 'azure-test' -and
            $PackageInfo.Version -eq '1.2.3' -and
            $PackageInfo.DevVersion -eq '1.2.4-beta.1' -and
            $ArtifactRoot -eq $script:artifactRoot -and
            $OutputDirectory -eq (Join-Path $script:outputDirectory 'package')
        }
    }

    It 'uses separate output folders for identical artifact names in different groups' {
        Set-TestPackageInfo -FileName 'first.json'
        Set-TestPackageInfo -FileName 'second.json' -Properties @{
            Name = 'azure-test'; Version = '1.2.3'; Group = 'com.other'
        }

        Invoke-TestValidation

        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 2 -Exactly
        Test-Path -LiteralPath (Join-Path $script:outputDirectory 'first') -PathType Container | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $script:outputDirectory 'second') -PathType Container | Should -BeTrue
    }

    It 'runs all packages and aggregates package-specific documentation failures' {
        Set-TestPackageInfo -FileName 'first.json'
        Set-TestPackageInfo -FileName 'second.json' -Properties @{
            Name = 'azure-other'; Version = '2.0.0'; Group = 'com.other'
        }
        Set-TestPackageInfo -FileName 'third.json' -Properties @{ Name = 'azure-valid'; Version = '3.0.0' }
        Mock Validate-Unknown-LocalDocsMsPackage {
            if ($PackageInfo.Name -eq 'azure-valid') {
                return [pscustomobject]@{ Status = 'Passed'; Reason = '' }
            }
            return [pscustomobject]@{ Status = 'Failed'; Reason = 'Malformed API comment.' }
        }

        { Invoke-TestValidation } | Should -Throw '*com.azure:azure-test*Malformed API comment*com.other:azure-other*Malformed API comment*'
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 3 -Exactly
    }

    It 'continues after a package infrastructure error and reports its cause' {
        Set-TestPackageInfo -FileName 'first.json'
        Set-TestPackageInfo -FileName 'second.json' -Properties @{ Name = 'azure-valid'; Version = '2.0.0' }
        Mock Validate-Unknown-LocalDocsMsPackage {
            if ($PackageInfo.Name -eq 'azure-test') {
                throw 'Required local source JAR is missing.'
            }
            return [pscustomobject]@{ Status = 'Passed'; Reason = '' }
        }

        { Invoke-TestValidation } | Should -Throw '*infrastructure error*com.azure:azure-test*Required local source JAR is missing*'
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 2 -Exactly
    }

    It 'honors skipPublishDocMs without requiring an artifact or version: <SkipValue>' -ForEach @(
        @{ SkipValue = $true }
        @{ SkipValue = 'true' }
        @{ SkipValue = 'True' }
    ) {
        Set-TestPackageInfo -Properties @{ Name = 'azure-test'; ArtifactDetails = @{ skipPublishDocMs = $SkipValue } }

        { Invoke-TestValidation } | Should -Not -Throw

        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
        Test-Path -LiteralPath $script:outputDirectory | Should -BeFalse
    }

    It 'does not treat false as a docs opt-out: <SkipValue>' -ForEach @(
        @{ SkipValue = $false }
        @{ SkipValue = 'false' }
    ) {
        Set-TestPackageInfo -Properties @{
            Name = 'azure-test'; Version = '1.2.3'; ArtifactDetails = @{ skipPublishDocMs = $SkipValue }
        }

        Invoke-TestValidation

        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 1 -Exactly
    }

    It 'allows optional Group and null ArtifactDetails' {
        Set-TestPackageInfo -Properties @{ Name = 'azure-test'; Version = '1.2.3'; ArtifactDetails = $null }

        { Invoke-TestValidation } | Should -Not -Throw
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 1 -Exactly
    }

    It 'accepts a language-specific skip with an explicit reason' {
        Set-TestPackageInfo
        Mock Validate-Unknown-LocalDocsMsPackage {
            return [pscustomobject]@{ Status = 'Skipped'; Reason = 'Not a publishable package.' }
        }

        { Invoke-TestValidation } | Should -Not -Throw
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 1 -Exactly
    }

    It 'succeeds for an existing empty PackageInfo directory without a language hook' {
        Mock Test-Path { return $false } -ParameterFilter { $Path -like 'Function:*' }

        { Invoke-TestValidation } | Should -Not -Throw
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
        Test-Path -LiteralPath $script:outputDirectory | Should -BeFalse
    }

    It 'succeeds for docs opt-outs even when the language hook is unavailable' {
        Set-TestPackageInfo -Properties @{ Name = 'azure-test'; ArtifactDetails = @{ skipPublishDocMs = $true } }
        Mock Test-Path { return $false } -ParameterFilter { $Path -like 'Function:*' }

        { Invoke-TestValidation } | Should -Not -Throw
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
    }

    It 'fails when a required directory is missing: <Missing>' -ForEach @(
        @{ Missing = 'ArtifactRoot' }
        @{ Missing = 'PackageInfoDirectory' }
    ) {
        if ($Missing -eq 'ArtifactRoot') {
            $script:artifactRoot = Join-Path $TestDrive 'missing-artifact'
        }
        else {
            $script:packageInfoDirectory = Join-Path $script:artifactRoot 'missing-metadata'
        }

        { Invoke-TestValidation } | Should -Throw '*infrastructure error*required directory*does not exist*'
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
    }

    It 'fails for an eligible package when no language hook is implemented' {
        Set-TestPackageInfo
        Mock Test-Path { return $false } -ParameterFilter { $Path -like 'Function:*' }

        { Invoke-TestValidation } | Should -Throw "*infrastructure error*Validate-Unknown-LocalDocsMsPackage*not implemented*"
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
    }

    It 'aggregates malformed JSON with later package failures' {
        Set-Content -LiteralPath (Join-Path $script:packageInfoDirectory 'first.json') -Value '{'
        Set-TestPackageInfo -FileName 'second.json'
        Mock Validate-Unknown-LocalDocsMsPackage {
            return [pscustomobject]@{ Status = 'Failed'; Reason = 'Invalid documentation.' }
        }

        { Invoke-TestValidation } | Should -Throw '*first.json*com.azure:azure-test*Invalid documentation*'
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 1 -Exactly
    }

    It 'rejects invalid metadata: <Label>' -ForEach @(
        @{ Label = 'null'; Json = 'null' }
        @{ Label = 'scalar'; Json = '"azure-test"' }
        @{ Label = 'array'; Json = '[{"Name":"azure-test","Version":"1.2.3"}]' }
        @{ Label = 'missing name'; Json = '{"Version":"1.2.3"}' }
        @{ Label = 'empty name'; Json = '{"Name":" ","Version":"1.2.3"}' }
        @{ Label = 'non-string name'; Json = '{"Name":123,"Version":"1.2.3"}' }
        @{ Label = 'missing version'; Json = '{"Name":"azure-test"}' }
        @{ Label = 'empty version'; Json = '{"Name":"azure-test","Version":""}' }
        @{ Label = 'non-string version'; Json = '{"Name":"azure-test","Version":123}' }
        @{ Label = 'non-object artifact details'; Json = '{"Name":"azure-test","Version":"1.2.3","ArtifactDetails":"skip"}' }
    ) {
        Set-Content -LiteralPath (Join-Path $script:packageInfoDirectory 'package.json') -Value $Json

        { Invoke-TestValidation } | Should -Throw '*infrastructure error*package.json*'
        Should -Invoke Validate-Unknown-LocalDocsMsPackage -Times 0 -Exactly
    }

    It 'rejects an invalid validator contract: <Label>' -ForEach @(
        @{ Label = 'null'; Result = $null }
        @{ Label = 'boolean'; Result = $true }
        @{ Label = 'missing reason'; Result = [pscustomobject]@{ Status = 'Passed' } }
        @{ Label = 'unknown status'; Result = [pscustomobject]@{ Status = 'Success'; Reason = '' } }
        @{ Label = 'wrong status case'; Result = [pscustomobject]@{ Status = 'passed'; Reason = '' } }
        @{ Label = 'non-string reason'; Result = [pscustomobject]@{ Status = 'Failed'; Reason = 123 } }
        @{ Label = 'unexplained skip'; Result = [pscustomobject]@{ Status = 'Skipped'; Reason = '' } }
        @{ Label = 'unexplained failure'; Result = [pscustomobject]@{ Status = 'Failed'; Reason = ' ' } }
        @{ Label = 'stray tool output'; Result = @('output', [pscustomobject]@{ Status = 'Passed'; Reason = '' }) }
    ) {
        Set-TestPackageInfo
        Mock Validate-Unknown-LocalDocsMsPackage { return $Result }

        { Invoke-TestValidation } | Should -Throw '*infrastructure error*Validator*must*'
    }

    It 'does not emit publishing result variables or modify package metadata' {
        Set-TestPackageInfo
        $metadataPath = Join-Path $script:packageInfoDirectory 'package.json'
        $before = Get-Content -LiteralPath $metadataPath -Raw
        $messages = @(Invoke-TestValidation 6>&1)

        Get-Content -LiteralPath $metadataPath -Raw | Should -BeExactly $before
        ($messages -join "`n") | Should -Not -Match 'task.setvariable|DocsMsPackagesAllValid'
        @(Get-ChildItem -LiteralPath $script:artifactRoot -Recurse -File).Count | Should -Be 1
    }

    It 'exits a standalone PowerShell process with <ExpectedExitCode> for <Status>' -ForEach @(
        @{ Status = 'Passed'; ExpectedExitCode = 0 }
        @{ Status = 'Failed'; ExpectedExitCode = 1 }
    ) {
        Set-TestPackageInfo
        $command = @"
function Validate-Unknown-LocalDocsMsPackage {
    param(`$PackageInfo, `$ArtifactRoot, `$OutputDirectory)
    return [pscustomobject]@{ Status = '$Status'; Reason = 'Generator result.' }
}
& '$($script:validationScript.Replace("'", "''"))' ``
    -PackageInfoDirectory '$($script:packageInfoDirectory.Replace("'", "''"))' ``
    -ArtifactRoot '$($script:artifactRoot.Replace("'", "''"))' ``
    -OutputDirectory '$($script:outputDirectory.Replace("'", "''"))'
"@
        $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))
        $PSNativeCommandUseErrorActionPreference = $false
        $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -EncodedCommand $encoded 2>&1

        $LASTEXITCODE | Should -Be $ExpectedExitCode -Because ($output -join "`n")
    }
}
