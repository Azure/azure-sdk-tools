#Requires -Version 7.0
#Requires -Modules Pester

# Pester tests for Initialize-EvalGitFixtures.ps1 (discovery only — the -ListOnly
# dry-run path, which clones nothing).
# Run from this directory:  Invoke-Pester

BeforeAll {
    $script:scriptPath = Join-Path $PSScriptRoot '..' 'Initialize-EvalGitFixtures.ps1'

    # Throwaway eval tree so the tests do not depend on real eval content.
    $script:root = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-fixtures-test-" + [Guid]::NewGuid())
    New-Item -ItemType Directory -Path (Join-Path $root 'evals/tools') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'evals/workflow-scenarios/mock') -Force | Out-Null

    # A unit eval with NO git fixture.
    "tags:`n  area: github`nstimuli:`n  - name: x" |
        Set-Content (Join-Path $root 'evals/tools/prompt-to-tool-github.eval.yaml')

    # A workflow eval declaring the same azure-rest-api-specs fixture twice
    # (two stimuli) — should collapse to one unique fixture.
    $mock = @(
        'stimuli:'
        '  - name: a'
        '    environment:'
        '      git:'
        '        type: worktree'
        '        source: ../../../../../../artifacts/specs-cache/azure-rest-api-specs'
        '        ref: main'
        '  - name: b'
        '    environment:'
        '      git:'
        '        type: worktree'
        '        source: ../../../../../../artifacts/specs-cache/azure-rest-api-specs'
        '        ref: main'
    ) -join "`n"
    $mock | Set-Content (Join-Path $root 'evals/workflow-scenarios/mock/release-planner-workflows.eval.yaml')
}

AfterAll {
    if (Test-Path $script:root) {
        Remove-Item -Path $script:root -Recurse -Force
    }
}

Describe 'Initialize-EvalGitFixtures.ps1 -ListOnly' {
    It 'discovers the declared git fixture' {
        $fixtures = @(& $script:scriptPath -EvalRoot $script:root -ListOnly)
        $fixtures.Count | Should -Be 1
    }

    It 'deduplicates identical fixtures across stimuli' {
        $fixtures = @(& $script:scriptPath -EvalRoot $script:root -ListOnly)
        ($fixtures | Where-Object RepoName -eq 'azure-rest-api-specs').Count | Should -Be 1
    }

    It 'parses the repo name, ref, and resolves an absolute cache path' {
        $fixtures = @(& $script:scriptPath -EvalRoot $script:root -ListOnly)
        $f = $fixtures[0]
        $f.RepoName  | Should -Be 'azure-rest-api-specs'
        $f.Ref       | Should -Be 'main'
        $f.CachePath | Should -Match 'artifacts[\\/]specs-cache[\\/]azure-rest-api-specs$'
        # '..' segments must be collapsed (absolute, normalized).
        $f.CachePath | Should -Not -Match '\.\.'
        [System.IO.Path]::IsPathRooted($f.CachePath) | Should -BeTrue
    }

    It 'is a no-op when the scanned suite declares no git fixtures' {
        $fixtures = @(& $script:scriptPath -EvalRoot $script:root -ListOnly `
                -Pattern 'evals/tools/*.eval.yaml')
        $fixtures.Count | Should -Be 0
    }

    It 'defaults the ref to main when none is declared' {
        $noRef = @(
            'stimuli:'
            '  - name: a'
            '    environment:'
            '      git:'
            '        type: worktree'
            '        source: ../../../../../../artifacts/specs-cache/some-other-repo'
        ) -join "`n"
        $noRef | Set-Content (Join-Path $script:root 'evals/workflow-scenarios/mock/no-ref.eval.yaml')
        try {
            $fixtures = @(& $script:scriptPath -EvalRoot $script:root -ListOnly)
            $other = $fixtures | Where-Object RepoName -eq 'some-other-repo'
            $other            | Should -Not -BeNullOrEmpty
            $other.Ref        | Should -Be 'main'
        }
        finally {
            Remove-Item (Join-Path $script:root 'evals/workflow-scenarios/mock/no-ref.eval.yaml') -Force
        }
    }
}

# Folder-level invariant guard (runs against the REAL eval tree, not the
# throwaway one). Because Vally resolves `git.source` relative to each eval
# file's own directory (no repo-root/env anchor — see microsoft/vally#562),
# the only way a single repo-relative path stays correct is if every
# git-fixture eval sits at the same depth and points at the same cache root.
# These tests fail loudly if a new fixture file is dropped at the wrong level.
Describe 'Folder-level invariant for real git fixtures' {
    BeforeAll {
        $script:vallyRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
        $script:repoRoot  = (Resolve-Path (Join-Path $script:vallyRoot '..' '..' '..')).Path
        $script:expectedCacheRoot = [System.IO.Path]::GetFullPath(
            (Join-Path $script:repoRoot 'artifacts' 'specs-cache')).TrimEnd('\', '/')

        # Every real eval file that declares a repo-relative worktree source.
        $srcRegex = [regex]'(?m)^\s*source:\s*(?<v>\.\.\S+)'
        $script:realFixtures = foreach ($file in
            Get-ChildItem -Path (Join-Path $script:vallyRoot 'evals') -Recurse -Filter '*.eval.yaml') {
            $content = Get-Content -LiteralPath $file.FullName -Raw
            foreach ($m in $srcRegex.Matches($content)) {
                $source = $m.Groups['v'].Value
                $abs = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $source))
                [PSCustomObject]@{
                    File   = $file.FullName
                    Source = $source
                    Parent = (Split-Path -Path $abs -Parent).TrimEnd('\', '/')
                    Depth  = (($source -split '[\\/]') | Where-Object { $_ -eq '..' }).Count
                }
            }
        }
    }

    It 'every git-fixture source resolves to the canonical artifacts/specs-cache root' {
        foreach ($f in $script:realFixtures) {
            $f.Parent | Should -Be $script:expectedCacheRoot -Because "$($f.File) declares source '$($f.Source)'"
        }
    }

    It 'all git-fixture eval files sit at the same folder depth' {
        $depths = @($script:realFixtures.Depth | Sort-Object -Unique)
        $depths.Count | Should -BeLessOrEqual 1 -Because 'a uniform ../ depth keeps one relative path valid for every fixture file'
    }
}
