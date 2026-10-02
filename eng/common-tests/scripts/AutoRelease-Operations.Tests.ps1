Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../../common/scripts/AutoRelease-Operations.ps1
}

Describe "Get-GitHubAutoReleasePullRequestForCommit" -Tag "UnitTest", "AutoRelease-Operations" {
    It 'handles the non-enumerated REST array through the real GitHub helper' -TestCases @(
        @{ AssociationCount = 0 }, @{ AssociationCount = 1 }, @{ AssociationCount = 2 }
    ) {
        param($AssociationCount)
        Mock Invoke-RestMethod {
            if ($Uri -like '*/commits/abc/pulls') {
                $items = @()
                if ($AssociationCount -gt 0) { $items += [pscustomobject]@{ number = 101 } }
                if ($AssociationCount -gt 1) { $items += [pscustomobject]@{ number = 102 } }
                return ,$items
            }
            if ($Uri -match '/pulls/(101|102)$') {
                $number = [int]$Matches[1]
                return [pscustomobject]@{
                    number = $number
                    merged_at = '2024-01-01T00:00:00Z'
                    merge_commit_sha = $(if ($number -eq 101) { 'abc' } else { 'another-merge' })
                    base = [pscustomobject]@{ ref = 'main' }
                    labels = @([pscustomobject]@{ name = 'auto-release' })
                }
            }
            throw "Unexpected REST request: $Uri"
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -Be ($AssociationCount -gt 0)
        if ($AssociationCount -gt 0) { $result.PullRequestNumber | Should -Be 101 }
        else { $result.PullRequestNumber | Should -BeNullOrEmpty }
        Should -Invoke Invoke-RestMethod -Times ($AssociationCount + 1) -Exactly
    }

    It 'matches the exact authoritative merge commit among associated PRs' {
        Mock Get-GitHubPullRequestsForCommit {
            @(
                [pscustomobject]@{ number = 10; merged_at = '2024-04-01T00:00:00Z'; merge_commit_sha = 'abc'; base = [pscustomobject]@{ ref = 'main' } },
                [pscustomobject]@{ number = 20; merged_at = '2024-03-01T00:00:00Z'; merge_commit_sha = 'outdated-summary'; base = [pscustomobject]@{ ref = 'main' } }
            )
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = $PullRequestNumber
                merged_at = '2024-03-01T00:00:00Z'
                merge_commit_sha = $(if ($PullRequestNumber -eq 20) { 'abc' } else { 'another-merge' })
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeTrue
        $result.PullRequestNumber | Should -Be 20
        $result.PullRequest.merge_commit_sha | Should -Be 'abc'
        Should -Invoke Get-GitHubPullRequest -Times 2 -Exactly
    }

    It "returns eligible when a merged, labeled pull request is associated with the commit" {
        Mock Get-GitHubPullRequestsForCommit {
            @(
                [pscustomobject]@{ number = 101; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } }
            )
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number    = 101
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base      = [pscustomobject]@{ ref = 'main' }
                labels    = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeTrue
        $result.PullRequestNumber | Should -Be 101
        $result.PullRequest.number | Should -Be 101
        $result.SkipReason | Should -BeNullOrEmpty
    }

    It "is not eligible when no pull request is merged into the target branch" {
        Mock Get-GitHubPullRequestsForCommit {
            @(
                [pscustomobject]@{ number = 1; merged_at = $null; base = [pscustomobject]@{ ref = 'main' } },
                [pscustomobject]@{ number = 2; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'release/1.0' } }
            )
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = $PullRequestNumber
                merged_at = $(if ($PullRequestNumber -eq 2) { '2024-01-01T00:00:00Z' } else { $null })
                merge_commit_sha = 'abc'
                base = [pscustomobject]@{ ref = $(if ($PullRequestNumber -eq 2) { 'release/1.0' } else { 'main' }) }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.PullRequestNumber | Should -BeNullOrEmpty
        $result.SkipReason | Should -BeLike "*No merged pull request*"
        Should -Invoke Get-GitHubPullRequest -Times 2 -Exactly
    }

    It "rejects multiple exact matches without selecting by date or label" {
        Mock Get-GitHubPullRequestsForCommit {
            @(
                [pscustomobject]@{ number = 10; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } },
                [pscustomobject]@{ number = 20; merged_at = '2024-03-15T12:00:00Z'; base = [pscustomobject]@{ ref = 'main' } },
                [pscustomobject]@{ number = 15; merged_at = '2024-02-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } }
            )
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number    = $PullRequestNumber
                merged_at = '2024-03-15T12:00:00Z'
                merge_commit_sha = 'abc'
                base      = [pscustomobject]@{ ref = 'main' }
                labels    = @([pscustomobject]@{ name = $(if ($PullRequestNumber -eq 20) { 'auto-release' } else { 'other-label' }) })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.PullRequestNumber | Should -BeNullOrEmpty
        $result.SkipReason | Should -BeLike '*Multiple merged pull requests*'
        Should -Invoke Get-GitHubPullRequest -Times 3 -Exactly
    }

    It "is not eligible when the required label is missing" {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 55; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number    = 55
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base      = [pscustomobject]@{ ref = 'main' }
                labels    = @([pscustomobject]@{ name = 'other-label' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.PullRequestNumber | Should -Be 55
        $result.SkipReason | Should -BeLike "*does not have the required label*"
    }

    It "is not eligible when the authoritative pull request is not merged into the target branch" {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 77; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } })
        }
        # The commit -> pulls payload lagged; the authoritative fetch shows the PR is not actually merged.
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number    = 77
                merged_at = $null
                base      = [pscustomobject]@{ ref = 'main' }
                labels    = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.SkipReason | Should -BeLike "*No merged pull request*"
    }

    It "honors a custom target branch and required label" {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 88; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'release/2.0' } })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number    = 88
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base      = [pscustomobject]@{ ref = 'release/2.0' }
                labels    = @([pscustomobject]@{ name = 'ship-it' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -TargetBranch 'release/2.0' -RequiredLabel 'ship-it' -AuthToken 'token'

        $result.IsEligible | Should -BeTrue
        $result.PullRequestNumber | Should -Be 88
    }

    It 'rejects an associated PR whose actual merge commit is missing or different' -TestCases @(
        @{ MergeCommit = $null }, @{ MergeCommit = 'an-older-commit' }
    ) {
        param($MergeCommit)
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 101; merged_at = '2024-01-01T00:00:00Z'; base = [pscustomobject]@{ ref = 'main' } })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = 101
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = $MergeCommit
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.SkipReason | Should -BeLike '*matches the build*'
    }

    It 'does not query individual PRs when no association is returned' {
        Mock Get-GitHubPullRequestsForCommit { @() }
        Mock Get-GitHubPullRequest { throw 'Unexpected fetch' }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.SkipReason | Should -BeLike '*No merged pull request*'
        Should -Invoke Get-GitHubPullRequest -Times 0 -Exactly
    }

    It 'uses canonical merge state and target branch when the association summary lags' {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 101; merged_at = $null; base = [pscustomobject]@{ ref = 'release/1.0' } })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = 101
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeTrue
        $result.PullRequestNumber | Should -Be 101
    }

    It 'counts duplicate associations for the same PR only once' {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 101 }, [pscustomobject]@{ number = 101 })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = 101
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeTrue
        $result.PullRequestNumber | Should -Be 101
        Should -Invoke Get-GitHubPullRequest -Times 1 -Exactly
    }

    It 'does not fall back to a labeled PR when the exact match lacks the required label' {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 10 }, [pscustomobject]@{ number = 20 })
        }
        Mock Get-GitHubPullRequest {
            [pscustomobject]@{
                number = $PullRequestNumber
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = $(if ($PullRequestNumber -eq 20) { 'abc' } else { 'another-merge' })
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = $(if ($PullRequestNumber -eq 20) { 'other-label' } else { 'auto-release' }) })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.PullRequestNumber | Should -Be 20
        $result.SkipReason | Should -BeLike '*does not have the required label*'
    }

    It 'does not select a known match if another candidate cannot be verified' {
        Mock Get-GitHubPullRequestsForCommit {
            @([pscustomobject]@{ number = 10 }, [pscustomobject]@{ number = 20 })
        }
        Mock Get-GitHubPullRequest {
            if ($PullRequestNumber -eq 20) { return $null }
            return [pscustomobject]@{
                number = 10
                merged_at = '2024-01-01T00:00:00Z'
                merge_commit_sha = 'abc'
                base = [pscustomobject]@{ ref = 'main' }
                labels = @([pscustomobject]@{ name = 'auto-release' })
            }
        }

        $result = Get-GitHubAutoReleasePullRequestForCommit -RepoId 'Azure/azure-sdk-for-net' -CommitSha 'abc' -AuthToken 'token'

        $result.IsEligible | Should -BeFalse
        $result.PullRequestNumber | Should -BeNullOrEmpty
        $result.SkipReason | Should -BeLike '*Could not verify*20*'
    }
}

Describe "New-GitHubPullRequestDiffObject" -Tag "UnitTest", "AutoRelease-Operations" {
    It "separates changed and deleted files and records the PR number as a string" {
        $files = @(
            [pscustomobject]@{ filename = 'sdk/storage/azure-storage-blob/foo.cs'; status = 'modified' },
            [pscustomobject]@{ filename = 'sdk/storage/azure-storage-blob/bar.cs'; status = 'added' },
            [pscustomobject]@{ filename = 'sdk/storage/azure-storage-blob/old.cs'; status = 'removed' }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 42 -PullRequestFiles $files

        $diff.PRNumber | Should -BeExactly '42'
        $diff.ChangedFiles | Should -Contain 'sdk/storage/azure-storage-blob/foo.cs'
        $diff.ChangedFiles | Should -Contain 'sdk/storage/azure-storage-blob/bar.cs'
        $diff.ChangedFiles | Should -Not -Contain 'sdk/storage/azure-storage-blob/old.cs'
        $diff.DeletedFiles | Should -Contain 'sdk/storage/azure-storage-blob/old.cs'
    }

    It "treats a renamed file as a change plus a deletion of the previous path" {
        $files = @(
            [pscustomobject]@{ filename = 'sdk/core/new-name.cs'; status = 'renamed'; previous_filename = 'sdk/core/old-name.cs' }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 7 -PullRequestFiles $files

        $diff.ChangedFiles | Should -Contain 'sdk/core/new-name.cs'
        $diff.DeletedFiles | Should -Contain 'sdk/core/old-name.cs'
    }

    It "normalizes backslashes to forward slashes" {
        $files = @(
            [pscustomobject]@{ filename = 'sdk\keyvault\secrets\file.cs'; status = 'modified' }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 1 -PullRequestFiles $files

        $diff.ChangedFiles | Should -Contain 'sdk/keyvault/secrets/file.cs'
    }

    It "derives changed services from sdk/<service>/ paths only" {
        $files = @(
            [pscustomobject]@{ filename = 'sdk/storage/pkg/a.cs'; status = 'modified' },
            [pscustomobject]@{ filename = 'sdk/keyvault/pkg/b.cs'; status = 'modified' },
            [pscustomobject]@{ filename = 'eng/common/scripts/x.ps1'; status = 'modified' }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 9 -PullRequestFiles $files

        $diff.ChangedServices | Should -Contain 'storage'
        $diff.ChangedServices | Should -Contain 'keyvault'
        $diff.ChangedServices.Count | Should -Be 2
    }

    It "removes duplicate paths and sorts them" {
        $files = @(
            [pscustomobject]@{ filename = 'sdk/b/2.cs'; status = 'modified' },
            [pscustomobject]@{ filename = 'sdk/a/1.cs'; status = 'modified' },
            [pscustomobject]@{ filename = 'sdk/a/1.cs'; status = 'modified' }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 3 -PullRequestFiles $files

        $diff.ChangedFiles.Count | Should -Be 2
        $diff.ChangedFiles[0] | Should -Be 'sdk/a/1.cs'
        $diff.ChangedFiles[1] | Should -Be 'sdk/b/2.cs'
    }

    It "records provided exclude paths and handles an empty file set" {
        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 5 -PullRequestFiles @() -ExcludePaths @('sdk/foo/')

        $diff.ChangedFiles.Count | Should -Be 0
        $diff.DeletedFiles.Count | Should -Be 0
        $diff.ChangedServices.Count | Should -Be 0
        $diff.ExcludePaths | Should -Contain 'sdk/foo/'
        $diff.PRNumber | Should -BeExactly '5'
    }

    It "excludes service-root files when requested" {
        $files = @(
            [pscustomobject]@{ filename = "sdk/storage/ci.yml"; status = "modified" },
            [pscustomobject]@{ filename = "sdk/storage/README.md"; status = "removed" },
            [pscustomobject]@{ filename = "sdk\storage\swagger_to_sdk_config.json"; status = "renamed"; previous_filename = "sdk\storage\ci.storage.yml" },
            [pscustomobject]@{ filename = "sdk/storage/azure-storage-blob/pom.xml"; status = "modified" },
            [pscustomobject]@{ filename = "eng/common/file.txt"; status = "modified" }
        )

        $diff = New-GitHubPullRequestDiffObject `
            -PullRequestNumber 123 `
            -PullRequestFiles $files `
            -ExcludeServiceRootFiles

        $diff.ChangedFiles | Should -Be @(
            "eng/common/file.txt"
            "sdk/storage/azure-storage-blob/pom.xml"
        )
        $diff.DeletedFiles | Should -BeNullOrEmpty
        $diff.ChangedServices | Should -Be @("storage")
    }

    It "retains service-root files by default" {
        $files = @(
            [pscustomobject]@{ filename = "sdk/storage/ci.yml"; status = "modified" }
        )

        $diff = New-GitHubPullRequestDiffObject -PullRequestNumber 123 -PullRequestFiles $files

        $diff.ChangedFiles | Should -Be @("sdk/storage/ci.yml")
        $diff.ChangedServices | Should -Be @("storage")
    }
}
