Import-Module Pester

Set-StrictMode -Version 4

Describe "Shared pre-push validation" -Tag "UnitTest" {
    BeforeAll {
        $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
        $instructionPath = ".github/instructions/azsdk-common-pre-push.instructions.md"
        $skillPath = ".github/skills/azsdk-common-pre-push-check/SKILL.md"
        $instruction = Get-Content (Join-Path $repoRoot $instructionPath) -Raw
        $sync = Get-Content (Join-Path $repoRoot "eng/pipelines/sync-.github-skills.yml") -Raw
    }

    It "applies the pre-push gate to all files and references an existing shared skill" {
        $instruction | Should -Match '(?m)^applyTo: "\*\*"$'
        $instruction | Should -Match ([regex]::Escape($skillPath))
        Test-Path (Join-Path $repoRoot $skillPath) | Should -BeTrue
        $instruction | Should -Match "Before every push or agent tool that commits and pushes"
    }

    It "syncs the mandatory instruction alongside skills without replacing repository instructions" {
        $sync | Should -Match '(?s)- name: DirectoryToSync\s+type: string\s+default: \.github\s'
        $patterns = [regex]::Match($sync, '(?s)- name: FilePatterns\s+type: object\s+default:(.*?)\n- name: Repos').Groups[1].Value
        $patterns | Should -Not -BeNullOrEmpty
        $patterns | Should -Match "(?m)^\s+- 'skills/azsdk-common-\*'$"
        $patterns | Should -Match "(?m)^\s+- 'instructions/azsdk-common-pre-push\.instructions\.md'$"
        ([regex]::Matches($patterns, '(?m)^\s+- ')).Count | Should -Be 2
    }

    It "triggers the sync pipeline for instruction-only updates" {
        $trigger = [regex]::Match($sync, '(?s)\npr:\s.*?paths:\s+include:(.*?)\nextends:').Groups[1].Value
        $trigger | Should -Match ([regex]::Escape($instructionPath))
    }

    It "generates patches once for all paths so same-commit skill and instruction updates cannot overwrite each other" {
        $template = Get-Content (Join-Path $repoRoot "eng/pipelines/templates/stages/archetype-sdk-tool-repo-sync.yml") -Raw
        ([regex]::Matches($template, 'git format-patch')).Count | Should -Be 1
        $template | Should -Match '\$patchPaths = @\(\$filePatterns \| ForEach-Object'
        $template | Should -Match 'git format-patch[^\r\n]+-- @patchPaths'
    }

    It "protects the instruction in both shared-file enforcement mechanisms" {
        $protected = Get-Content (Join-Path $repoRoot ".github/workflows/protected-files.yml") -Raw
        $enforcer = Get-Content (Join-Path $repoRoot "eng/common/pipelines/templates/steps/eng-common-workflow-enforcer.yml") -Raw
        $protected | Should -Match ([regex]::Escape($instructionPath))
        $enforcer | Should -Match ([regex]::Escape($instructionPath))
    }
}
