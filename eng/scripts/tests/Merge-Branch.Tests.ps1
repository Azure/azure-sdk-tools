# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

BeforeAll {
    $mergeScript = Join-Path $PSScriptRoot "../Merge-Branch.ps1"

    function Invoke-TestGit {
        param([string[]]$Arguments)

        $output = & git @Arguments 2>&1
        if ($LASTEXITCODE) {
            throw "git $Arguments failed: $output"
        }
        return $output
    }

    function Write-TestFile {
        param([string]$Path, [string]$Content)

        New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
        Set-Content -LiteralPath $Path -Value $Content
    }

    function Save-TestCommit {
        param([string]$Message)

        Invoke-TestGit @("add", "-A")
        Invoke-TestGit @("commit", "--quiet", "-m", $Message)
    }
}

Describe "Merge-Branch" -Tag "UnitTest" {
    BeforeEach {
        $repo = Join-Path $TestDrive "repository with spaces $([guid]::NewGuid())"
        New-Item -ItemType Directory -Path $repo -Force | Out-Null
        Push-Location $repo
        Invoke-TestGit @("init", "--quiet", "--initial-branch=target")
        Invoke-TestGit @("config", "user.name", "Merge test")
        Invoke-TestGit @("config", "user.email", "merge-test@example.invalid")
        Invoke-TestGit @("config", "commit.gpgSign", "false")
        Invoke-TestGit @("config", "core.autocrlf", "false")

        Write-TestFile "eng/legacy file [old].js" "original JavaScript"
        Write-TestFile "eng/deleted.txt" "unchanged on target"
        Write-TestFile "eng/updated.txt" "base"
        Write-TestFile "specification/private/deleted.txt" "base"
        Write-TestFile "specification/private/keep.txt" "base"
        Write-TestFile "specification/common-types/deleted.txt" "base"
        Write-TestFile "manual/conflict.txt" "base"
        Save-TestCommit "Base"

        Invoke-TestGit @("switch", "--quiet", "-c", "source")
        Remove-Item -LiteralPath "eng/legacy file [old].js", "eng/deleted.txt", "specification/common-types/deleted.txt"
        Write-TestFile "eng/replacement.ts" "replacement TypeScript"
        Write-TestFile "eng/updated.txt" "source"
        Write-TestFile "specification/private/deleted.txt" "source"
        Write-TestFile "specification/private/keep.txt" "source"
        Write-TestFile "manual/conflict.txt" "source"
        Save-TestCommit "Source changes"

        Invoke-TestGit @("switch", "--quiet", "target")
        Write-TestFile "eng/legacy file [old].js" "target JavaScript"
        Write-TestFile "eng/updated.txt" "target"
        Write-TestFile "eng/target-only.txt" "stale target file"
        Remove-Item -LiteralPath "specification/private/deleted.txt"
        Write-TestFile "specification/private/keep.txt" "target"
        Write-TestFile "specification/common-types/deleted.txt" "target"
        Write-TestFile "manual/conflict.txt" "target"
        Save-TestCommit "Target changes"

        Write-TestFile "eng/untracked.txt" "local untracked file"
    }

    AfterEach {
        Pop-Location
    }

    It "restores source deletions and target deletions without staging manual conflicts or untracked files" {
        & $mergeScript -SourceBranch source -Theirs "**" -Ours "specification" `
            -Merge "specification/common-types", "manual"
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--cached", "--name-status", "source", "--", "eng") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--cached", "--name-status", "HEAD", "--", "specification/private") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-status", "--", "eng", "specification/private") | Should -BeNullOrEmpty
        Test-Path -LiteralPath "eng/legacy file [old].js" | Should -BeFalse
        Test-Path -LiteralPath "eng/deleted.txt" | Should -BeFalse
        Test-Path -LiteralPath "eng/target-only.txt" | Should -BeFalse
        Test-Path -LiteralPath "specification/private/deleted.txt" | Should -BeFalse
        Get-Content "specification/private/keep.txt" | Should -Be "target"
        Invoke-TestGit @("diff", "--name-only", "--diff-filter=U") |
            Should -Be @("manual/conflict.txt", "specification/common-types/deleted.txt")
        Get-Content "eng/untracked.txt" | Should -Be "local untracked file"
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }

    It "honors source deletions in the final merge while preserving private files" {
        & $mergeScript -SourceBranch source -Theirs "**" -Ours "specification" `
            -Merge "specification/common-types", "manual" -AcceptTheirsForFinalMerge $true
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--cached", "--name-status", "source", "--", "eng", "manual", "specification/common-types") |
            Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--cached", "--name-status", "HEAD", "--", "specification/private") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-status") | Should -BeNullOrEmpty
        Invoke-TestGit @("ls-files", "--unmerged") | Should -BeNullOrEmpty
        Test-Path -LiteralPath "eng/legacy file [old].js" | Should -BeFalse
        Test-Path -LiteralPath "specification/common-types/deleted.txt" | Should -BeFalse
        Test-Path -LiteralPath "specification/private/deleted.txt" | Should -BeFalse
        Get-Content "eng/untracked.txt" | Should -Be "local untracked file"
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }

    It "does not treat an empty final-merge selection as the entire repository" {
        & $mergeScript -SourceBranch source -Theirs "eng" -Ours "specification" `
            -AcceptTheirsForFinalMerge $true
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--cached", "--name-status", "source", "--", "eng") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--cached", "--name-status", "HEAD", "--", "specification") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-only", "--diff-filter=U") | Should -Be "manual/conflict.txt"
        Test-Path -LiteralPath "specification/private/deleted.txt" | Should -BeFalse
        Get-Content "specification/private/keep.txt" | Should -Be "target"
    }

    It "can resolve only the final merge selection" {
        & $mergeScript -SourceBranch source -Merge "specification/common-types" -AcceptTheirsForFinalMerge $true
        $LASTEXITCODE | Should -Be 0

        Test-Path -LiteralPath "specification/common-types/deleted.txt" | Should -BeFalse
        Invoke-TestGit @("diff", "--cached", "--name-status", "source", "--", "specification/common-types") |
            Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-only", "--diff-filter=U") |
            Should -Contain "manual/conflict.txt"
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }

    It "can restore theirs without exclusion lists" {
        & $mergeScript -SourceBranch source -Theirs "**"
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--cached", "--name-status", "source") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-status") | Should -BeNullOrEmpty
        Test-Path -LiteralPath "eng/legacy file [old].js" | Should -BeFalse
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }

    It "can restore ours without other policies" {
        & $mergeScript -SourceBranch source -Ours "**"
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--cached", "--name-status", "HEAD") | Should -BeNullOrEmpty
        Invoke-TestGit @("diff", "--name-status") | Should -BeNullOrEmpty
        Test-Path -LiteralPath "specification/private/deleted.txt" | Should -BeFalse
        Test-Path -LiteralPath "eng/replacement.ts" | Should -BeFalse
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }

    It "leaves conflicts unresolved when no paths are selected" {
        & $mergeScript -SourceBranch source
        $LASTEXITCODE | Should -Be 0

        Invoke-TestGit @("diff", "--name-only", "--diff-filter=U") |
            Should -Be @(
                "eng/legacy file [old].js",
                "eng/updated.txt",
                "manual/conflict.txt",
                "specification/common-types/deleted.txt",
                "specification/private/deleted.txt",
                "specification/private/keep.txt"
            )
        Invoke-TestGit @("ls-files", "--", "eng/untracked.txt") | Should -BeNullOrEmpty
    }
}
