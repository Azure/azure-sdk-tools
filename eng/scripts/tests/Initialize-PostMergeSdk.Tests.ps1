BeforeAll {
    $script:entrypoint = Join-Path $PSScriptRoot '..\Initialize-PostMergeSdk.ps1'
    $script:nativeGit = (Get-Command git).Source

    function Invoke-FixtureGit {
        param([string[]] $Arguments)
        $output = @(& $script:nativeGit @Arguments)
        if ($LASTEXITCODE -ne 0) { throw "Local Git fixture failed: $($Arguments -join ' ')" }
        return ,$output
    }
}

Describe 'Trusted post-merge SDK checkout' {
    BeforeEach {
        $script:savedEnvironment = @{}
        foreach ($pair in @{
            BUILD_REPOSITORY_NAME = 'Azure/azure-sdk-tools'; BUILD_SOURCEBRANCH = 'refs/heads/main'
            SYSTEM_TEAMPROJECT = 'internal'; SYSTEM_COLLECTIONURI = 'https://dev.azure.com/azure-sdk/'
        }.GetEnumerator()) {
            $script:savedEnvironment[$pair.Key] = [Environment]::GetEnvironmentVariable($pair.Key)
            [Environment]::SetEnvironmentVariable($pair.Key, $pair.Value)
        }
        $global:SdkCheckoutCommands = [System.Collections.Generic.List[object]]::new()
        $global:SdkCheckoutFailure = ''
        $global:SdkCheckoutTree = "100644 blob abcd`tsdk/a/ci.yml"
        $global:SdkCheckoutOldExists = $false
        $global:SdkCheckoutHead = 'a' * 40
        Mock git {
            $global:SdkCheckoutCommands.Add(@($args))
            $global:LASTEXITCODE = 0
            if ($global:SdkCheckoutFailure -and $args -contains $global:SdkCheckoutFailure) {
                $global:LASTEXITCODE = 1
                return
            }
            if ($args -contains 'ls-tree') {
                if ($args[-1] -eq 'sdk/old/ci.yml' -and -not $global:SdkCheckoutOldExists) { return }
                if ($global:SdkCheckoutTree) { return $global:SdkCheckoutTree }
            }
            if ($args -contains 'rev-parse') { return $global:SdkCheckoutHead }
        }
        $script:arguments = @{
            Directory = Join-Path $TestDrive ("sdk-data-" + [Guid]::NewGuid().ToString('N'))
            RepositoryName = 'azure-sdk-for-net'
            MergeCommitSha = 'a' * 40
            DescriptorPaths = '[{"path":"sdk/a/ci.yml"}]'
        }
    }

    AfterEach {
        foreach ($name in $script:savedEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:savedEnvironment[$name])
        }
        Remove-Variable SdkCheckoutCommands, SdkCheckoutFailure, SdkCheckoutTree, SdkCheckoutOldExists, SdkCheckoutHead -Scope Global
        Remove-Variable SdkCheckoutNativeGit, SdkCheckoutLocalSource -Scope Global -ErrorAction SilentlyContinue
    }

    It 'disables hooks and verifies ancestry before sparse-checking out the exact merged SHA' {
        & $entrypoint @arguments
        $commands = $global:SdkCheckoutCommands.ToArray()
        @($commands | Where-Object { $_ -contains 'clone' }).Count | Should -Be 1
        @($commands | Where-Object { $_ -contains '--filter=tree:0' -and $_ -contains '--no-checkout' -and
            ($_ -join ' ').Contains('--branch main') -and $_ -contains 'https://github.com/Azure/azure-sdk-for-net.git' }).Count |
            Should -Be 1
        @($commands | Where-Object { $_ -notcontains 'core.hooksPath=/dev/null' }).Count | Should -Be 0
        $names = @($commands | ForEach-Object { $_ -join ' ' })
        [array]::FindIndex($names, [Predicate[string]] { param($value) $value.Contains('merge-base --is-ancestor') }) |
            Should -BeLessThan ([array]::FindIndex($names, [Predicate[string]] { param($value) $value.Contains('checkout --detach') }))
        @($commands | Where-Object { $_ -contains 'merge-base' -and $_ -contains 'refs/remotes/origin/main' }).Count | Should -Be 1
        @($commands | Where-Object { $_ -contains 'sparse-checkout' -and $_ -contains '/sdk/**/*.yml' }).Count | Should -Be 1
        @($commands | Where-Object { $_ -contains '--detach' -and $_ -contains ('a' * 40) }).Count | Should -Be 1
    }

    It 'blocks checkout when the SHA is outside default-branch history' {
        $global:SdkCheckoutFailure = 'merge-base'
        { & $entrypoint @arguments } | Should -Throw '*Git command failed*merge-base*'
        @($global:SdkCheckoutCommands.ToArray() | Where-Object { $_ -contains '--detach' }).Count | Should -Be 0
    }

    It 'rejects missing or symlinked tracked descriptors' -ForEach @('', "120000 blob abcd`tsdk/a/ci.yml") {
        $global:SdkCheckoutTree = $_
        { & $entrypoint @arguments } | Should -Throw '*regular tracked file*'
        @($global:SdkCheckoutCommands.ToArray() | Where-Object { $_ -contains '--detach' }).Count | Should -Be 0
    }

    It 'preserves a supported relocation whose old path is absent at the merged SHA' {
        $arguments.DescriptorPaths = '[{"path":"sdk/a/ci.yml","previousPath":"sdk/old/ci.yml"}]'
        & $entrypoint @arguments
        @($global:SdkCheckoutCommands.ToArray() | Where-Object { $_ -contains 'ls-tree' -and $_ -contains 'sdk/old/ci.yml' }).Count |
            Should -Be 1
    }

    It 'rejects a relocation if the old path is still tracked' {
        $arguments.DescriptorPaths = '[{"path":"sdk/a/ci.yml","previousPath":"sdk/old/ci.yml"}]'
        $global:SdkCheckoutOldExists = $true
        { & $entrypoint @arguments } | Should -Throw '*still exists*'
        @($global:SdkCheckoutCommands.ToArray() | Where-Object { $_ -contains '--detach' }).Count | Should -Be 0
    }

    It 'verifies the checked-out HEAD matches the requested SHA' {
        $global:SdkCheckoutHead = 'b' * 40
        { & $entrypoint @arguments } | Should -Throw '*HEAD does not match*'
    }

    It 'does not reuse a preexisting checkout' {
        $null = New-Item -ItemType Directory -Path $arguments.Directory
        { & $entrypoint @arguments } | Should -Throw '*already exists*'
        $global:SdkCheckoutCommands.Count | Should -Be 0
    }

    It 'rejects untrusted tools branches before any network or Git operation' {
        $env:BUILD_SOURCEBRANCH = 'refs/heads/untrusted'
        { & $entrypoint @arguments } | Should -Throw '*trusted tools default-branch*'
        $global:SdkCheckoutCommands.Count | Should -Be 0
    }

    It 'rejects unsupported repositories or malformed inputs before any network or Git operation' -ForEach @(
        @{ Key = 'RepositoryName'; Value = 'azure-sdk-for-cpp' },
        @{ Key = 'RepositoryName'; Value = 'azure-sdk-for-rust' },
        @{ Key = 'MergeCommitSha'; Value = 'main' },
        @{ Key = 'DescriptorPaths'; Value = '[]' },
        @{ Key = 'DescriptorPaths'; Value = '[{"path":"sdk/../a/ci.yml"}]' },
        @{ Key = 'DescriptorPaths'; Value = '[{"path":"sdk/a/ci.bad-variant.yml"}]' },
        @{ Key = 'DescriptorPaths'; Value = '[{"path":"sdk/a/ci.yml","previousPath":null}]' }
    ) {
        $arguments[$Key] = $Value
        { & $entrypoint @arguments } | Should -Throw
        $global:SdkCheckoutCommands.Count | Should -Be 0
    }

    It 'checks out an earlier merged SHA and a Unicode variant using real local Git without network' {
        $source = Join-Path $TestDrive 'local-source'
        $null = Invoke-FixtureGit @('init', '--initial-branch=main', $source)
        $null = New-Item -ItemType Directory -Path (Join-Path $source 'sdk\old') -Force
        'old descriptor' | Set-Content -LiteralPath (Join-Path $source 'sdk\old\ci.yml')
        $null = Invoke-FixtureGit @('-C', $source, 'add', '.')
        $null = Invoke-FixtureGit @('-C', $source, '-c', 'user.name=Checkout Test', '-c', 'user.email=test@example.invalid',
            'commit', '-m', 'Initial descriptor')
        $null = New-Item -ItemType Directory -Path (Join-Path $source 'sdk\new') -Force
        $null = Invoke-FixtureGit @('-C', $source, 'mv', 'sdk/old/ci.yml', 'sdk/new/ci.yml')
        'merged descriptor' | Set-Content -LiteralPath (Join-Path $source 'sdk\new\ci.yml')
        $variantName = "ci.$([char]0x03b1).yml"
        'variant descriptor' | Set-Content -LiteralPath (Join-Path $source "sdk\new\$variantName")
        'not in sparse scope' | Set-Content -LiteralPath (Join-Path $source 'README.md')
        $null = Invoke-FixtureGit @('-C', $source, 'add', '.')
        $null = Invoke-FixtureGit @('-C', $source, '-c', 'user.name=Checkout Test', '-c', 'user.email=test@example.invalid',
            'commit', '-m', 'Merged descriptors')
        $mergedSha = (Invoke-FixtureGit @('-C', $source, 'rev-parse', 'HEAD'))[0]
        'newer default-branch content' | Set-Content -LiteralPath (Join-Path $source 'sdk\new\ci.yml')
        $null = Invoke-FixtureGit @('-C', $source, 'add', '.')
        $null = Invoke-FixtureGit @('-C', $source, '-c', 'user.name=Checkout Test', '-c', 'user.email=test@example.invalid',
            'commit', '-m', 'Later change')

        $global:SdkCheckoutNativeGit = $script:nativeGit
        $global:SdkCheckoutLocalSource = $source
        Mock git {
            # Only replace the GitHub remote with the local fixture. Every Git operation is real.
            $gitArguments = @($args)
            $remoteIndex = [array]::IndexOf($gitArguments, 'https://github.com/Azure/azure-sdk-for-net.git')
            if ($remoteIndex -ge 0) { $gitArguments[$remoteIndex] = $global:SdkCheckoutLocalSource }
            & $global:SdkCheckoutNativeGit @gitArguments
            $global:LASTEXITCODE = $LASTEXITCODE
            if ($remoteIndex -ge 0 -and $gitArguments -contains 'clone' -and $LASTEXITCODE -eq 0) {
                $hook = Join-Path $gitArguments[-1] '.git' 'hooks' 'post-checkout'
                [System.IO.File]::WriteAllText($hook, "#!/bin/sh`nexit 99`n")
                if (-not $IsWindows) {
                    [System.IO.File]::SetUnixFileMode($hook, [System.IO.UnixFileMode] 'UserRead, UserWrite, UserExecute')
                }
            }
        }
        $arguments.MergeCommitSha = $mergedSha
        $arguments.DescriptorPaths = ConvertTo-Json -InputObject @(
            @{ path = 'sdk/new/ci.yml'; previousPath = 'sdk/old/ci.yml' },
            @{ path = "sdk/new/$variantName" }
        )
        & $entrypoint @arguments
        (Invoke-FixtureGit @('-C', $arguments.Directory, 'rev-parse', 'HEAD'))[0] | Should -Be $mergedSha
        (Get-Content -LiteralPath (Join-Path $arguments.Directory 'sdk\new\ci.yml') -Raw).Trim() | Should -Be 'merged descriptor'
        (Get-Content -LiteralPath (Join-Path $arguments.Directory "sdk\new\$variantName") -Raw).Trim() | Should -Be 'variant descriptor'
        Test-Path -LiteralPath (Join-Path $arguments.Directory 'sdk\old\ci.yml') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $arguments.Directory 'README.md') | Should -BeFalse
    }
}
