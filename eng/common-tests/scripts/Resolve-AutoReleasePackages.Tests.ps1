Import-Module Pester

Describe "Resolve-AutoReleasePackages" -Tag "UnitTest", "Resolve-AutoReleasePackages" {
    BeforeAll {
        $script:scriptPath = (Resolve-Path "$PSScriptRoot/../../common/scripts/Resolve-AutoReleasePackages.ps1").Path

        # Stub every external dependency the script resolves via Get-Command so it never dot-sources
        # common.ps1 / AutoRelease-Operations.ps1 (which need a full language-repo environment). The stubs
        # live in the GLOBAL scope so the script under test - invoked via '&', which runs in a child scope -
        # can see them. They must reference $global:* (never $script:*) because inside a stub that runs while
        # the script under test is executing, $script: resolves to the CALLED script's scope, not the test's.
        function global:Set-PipelineVariable {
            param([string]$Name, $Value = '', [switch]$IsOutput, [switch]$IsSecret)
            $global:AutoReleaseEmittedVars[$Name] = "$Value"
        }
        function global:Get-GitHubAutoReleasePullRequestForCommit {
            param($RepoId, $CommitSha, $TargetBranch, $RequiredLabel, $AuthToken)
            if ($global:AutoReleaseThrowOnResolve) { throw 'boom resolving PR' }
            $global:AutoReleaseResolveArgs = [pscustomobject]@{
                RepoId = $RepoId; CommitSha = $CommitSha; TargetBranch = $TargetBranch; RequiredLabel = $RequiredLabel; AuthToken = $AuthToken
            }
            return $global:AutoReleaseStubRelease
        }
        function global:Get-GitHubPullRequestFiles {
            param($RepoId, $PullRequestNumber, $AuthToken)
            return $global:AutoReleaseStubFiles
        }
        function global:New-GitHubPullRequestDiffObject {
            param($PullRequestNumber, $PullRequestFiles, $ExcludePaths)
            return [pscustomobject]@{ ChangedFiles = @(); ChangedServices = @(); ExcludePaths = @(); DeletedFiles = @(); PRNumber = "$PullRequestNumber" }
        }
        function global:Get-PrPkgProperties {
            param([string]$InputDiffJson)
            $global:AutoReleaseGetPrPkgCalled = $true
            if ($global:AutoReleaseThrowOnGetPrPkg) { throw 'boom detecting packages' }
            return $global:AutoReleaseStubChangedPackages
        }

        function Invoke-ResolveScript {
            param([string]$Artifacts, [string]$CommitSha = 'abc123', [string]$RepoId = 'Azure/azure-sdk-for-net')
            & $script:scriptPath -CommitSha $CommitSha -RepoId $RepoId -Artifacts $Artifacts -AuthToken 'fake-token' | Out-Null
            return $global:AutoReleaseEmittedVars
        }
    }

    AfterAll {
        Remove-Item function:global:Set-PipelineVariable -ErrorAction SilentlyContinue
        Remove-Item function:global:Get-GitHubAutoReleasePullRequestForCommit -ErrorAction SilentlyContinue
        Remove-Item function:global:Get-GitHubPullRequestFiles -ErrorAction SilentlyContinue
        Remove-Item function:global:New-GitHubPullRequestDiffObject -ErrorAction SilentlyContinue
        Remove-Item function:global:Get-PrPkgProperties -ErrorAction SilentlyContinue
        Remove-Variable -Scope Global -ErrorAction SilentlyContinue -Name `
            AutoReleaseEmittedVars, AutoReleaseStubRelease, AutoReleaseStubFiles, AutoReleaseStubChangedPackages, `
            AutoReleaseGetPrPkgCalled, AutoReleaseResolveArgs, AutoReleaseThrowOnResolve, AutoReleaseThrowOnGetPrPkg
    }

    BeforeEach {
        $global:AutoReleaseEmittedVars = @{}
        $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = $null; IsEligible = $false; SkipReason = 'default'; PullRequest = $null }
        $global:AutoReleaseStubFiles = @()
        $global:AutoReleaseStubChangedPackages = @()
        $global:AutoReleaseGetPrPkgCalled = $false
        $global:AutoReleaseResolveArgs = $null
        $global:AutoReleaseThrowOnResolve = $false
        $global:AutoReleaseThrowOnGetPrPkg = $false
    }

    Context "when no eligible pull request is found" {
        It "emits fail-closed defaults and does not run package detection" {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = $null; IsEligible = $false; SkipReason = 'no PR'; PullRequest = $null }

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"Azure.Storage.Blobs","safeName":"AzureStorageBlobs"}]'

            $vars['AutoReleaseLabelPresent'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
            $vars['ReleaseArtifact_AzureStorageBlobs'] | Should -Be 'false'
            $global:AutoReleaseGetPrPkgCalled | Should -BeFalse
        }

        It "still emits the resolved PR number when the pull request is not eligible" {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = 321; IsEligible = $false; SkipReason = 'missing label'; PullRequest = $null }

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"pkg","safeName":"pkg"}]'

            $vars['AutoReleasePrNumber'] | Should -Be '321'
            $vars['AutoReleaseLabelPresent'] | Should -Be 'false'
        }

        It "forwards commit, repo, branch and label to the resolver with the documented defaults" {
            Invoke-ResolveScript -Artifacts '[]' -CommitSha 'deadbeef' -RepoId 'Azure/azure-sdk-for-java' | Out-Null

            $global:AutoReleaseResolveArgs.CommitSha | Should -Be 'deadbeef'
            $global:AutoReleaseResolveArgs.RepoId | Should -Be 'Azure/azure-sdk-for-java'
            $global:AutoReleaseResolveArgs.TargetBranch | Should -Be 'main'
            $global:AutoReleaseResolveArgs.RequiredLabel | Should -Be 'auto-release'
        }
    }

    Context "when an eligible pull request changes a declared package" {
        BeforeEach {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = 123; IsEligible = $true; SkipReason = ''; PullRequest = [pscustomobject]@{ number = 123 } }
        }

        It "marks the matching artifact releasable and emits it in the JSON payload" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'Azure.Storage.Blobs'; ArtifactName = 'Azure.Storage.Blobs'; Group = $null; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"Azure.Storage.Blobs","safeName":"AzureStorageBlobs"}]'

            $vars['AutoReleasePrNumber'] | Should -Be '123'
            $vars['AutoReleaseLabelPresent'] | Should -Be 'true'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'true'
            $vars['ReleaseArtifact_AzureStorageBlobs'] | Should -Be 'true'

            # A single match must still serialize as a JSON array, not a bare object.
            $vars['AutoReleaseArtifactsJson'] | Should -Match '^\['
            $payload = @($vars['AutoReleaseArtifactsJson'] | ConvertFrom-Json)
            $payload.Count | Should -Be 1
            $payload[0].name | Should -Be 'Azure.Storage.Blobs'
        }

        It "does not mark an unrelated artifact releasable" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'Azure.Storage.Blobs'; ArtifactName = 'Azure.Storage.Blobs'; Group = $null; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"Azure.Messaging.EventHubs","safeName":"AzureMessagingEventHubs"}]'

            $vars['AutoReleaseLabelPresent'] | Should -Be 'true'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
            $vars['ReleaseArtifact_AzureMessagingEventHubs'] | Should -Be 'false'
        }

        It "excludes packages that were only included for validation" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'Azure.Storage.Blobs'; ArtifactName = 'Azure.Storage.Blobs'; Group = $null; IncludedForValidation = $true }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"Azure.Storage.Blobs","safeName":"AzureStorageBlobs"}]'

            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['ReleaseArtifact_AzureStorageBlobs'] | Should -Be 'false'
        }

        It "matches a package by its ArtifactName when it differs from the package name" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'internal-name'; ArtifactName = 'public-artifact'; Group = $null; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"public-artifact","safeName":"publicartifact"}]'

            $vars['ReleaseArtifact_publicartifact'] | Should -Be 'true'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'true'
        }

        It "marks only the changed subset of multiple declared artifacts" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'Pkg.A'; ArtifactName = 'Pkg.A'; Group = $null; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"Pkg.A","safeName":"PkgA"},{"name":"Pkg.B","safeName":"PkgB"}]'

            $vars['ReleaseArtifact_PkgA'] | Should -Be 'true'
            $vars['ReleaseArtifact_PkgB'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'true'

            $payload = @($vars['AutoReleaseArtifactsJson'] | ConvertFrom-Json)
            $payload.Count | Should -Be 1
            $payload[0].name | Should -Be 'Pkg.A'
        }
    }

    Context "for group-scoped artifacts (e.g. Java)" {
        BeforeEach {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = 200; IsEligible = $true; SkipReason = ''; PullRequest = [pscustomobject]@{ number = 200 } }
        }

        It "matches when the package group and name match the artifact groupId and name" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'azure-storage-blob'; ArtifactName = 'azure-storage-blob'; Group = 'com.azure'; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"azure-storage-blob","safeName":"azurestorageblob","groupId":"com.azure"}]'

            $vars['ReleaseArtifact_azurestorageblob'] | Should -Be 'true'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'true'

            $payload = @($vars['AutoReleaseArtifactsJson'] | ConvertFrom-Json)
            $payload[0].groupId | Should -Be 'com.azure'
        }

        It "does not match when the package belongs to a different group" {
            $global:AutoReleaseStubChangedPackages = @(
                [pscustomobject]@{ Name = 'azure-storage-blob'; ArtifactName = 'azure-storage-blob'; Group = 'com.other'; IncludedForValidation = $false }
            )

            $vars = Invoke-ResolveScript -Artifacts '[{"name":"azure-storage-blob","safeName":"azurestorageblob","groupId":"com.azure"}]'

            $vars['ReleaseArtifact_azurestorageblob'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
        }
    }

    Context "when there is nothing to release" {
        It "marks the label present but releases nothing when the pipeline declares no artifacts" {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = 9; IsEligible = $true; SkipReason = ''; PullRequest = [pscustomobject]@{ number = 9 } }

            $vars = Invoke-ResolveScript -Artifacts '[]'

            $vars['AutoReleaseLabelPresent'] | Should -Be 'true'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
            $global:AutoReleaseGetPrPkgCalled | Should -BeFalse
        }

        It "treats invalid artifacts JSON as empty without failing" {
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = $null; IsEligible = $false; SkipReason = 'no PR'; PullRequest = $null }

            { Invoke-ResolveScript -Artifacts 'not-valid-json' } | Should -Not -Throw

            $vars = $global:AutoReleaseEmittedVars
            $vars['AutoReleaseLabelPresent'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
        }
    }

    Context "when resolution fails" {
        It "fails closed and does not throw when the resolver raises an error" {
            $global:AutoReleaseThrowOnResolve = $true

            { Invoke-ResolveScript -Artifacts '[{"name":"pkg","safeName":"pkg"}]' } | Should -Not -Throw

            $vars = $global:AutoReleaseEmittedVars
            $vars['AutoReleaseLabelPresent'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
            $vars['ReleaseArtifact_pkg'] | Should -Be 'false'
        }

        It "resets the label-present signal when a failure occurs after the PR is deemed eligible" {
            # The PR resolves as eligible (AutoReleaseLabelPresent is flipped to 'true') but package
            # detection then throws. The catch block must re-emit the fail-closed defaults so no partial
            # release decision leaks downstream.
            $global:AutoReleaseStubRelease = [pscustomobject]@{ PullRequestNumber = 500; IsEligible = $true; SkipReason = ''; PullRequest = [pscustomobject]@{ number = 500 } }
            $global:AutoReleaseThrowOnGetPrPkg = $true

            { Invoke-ResolveScript -Artifacts '[{"name":"pkg","safeName":"pkg"}]' } | Should -Not -Throw

            $vars = $global:AutoReleaseEmittedVars
            $vars['AutoReleaseLabelPresent'] | Should -Be 'false'
            $vars['HasAutoReleaseArtifacts'] | Should -Be 'false'
            $vars['AutoReleaseArtifactsJson'] | Should -Be '[]'
            $vars['ReleaseArtifact_pkg'] | Should -Be 'false'
            # The resolved PR number is informational and is intentionally preserved.
            $vars['AutoReleasePrNumber'] | Should -Be '500'
        }
    }
}
