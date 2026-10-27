BeforeAll {
    $script:syncPatchScript = Join-Path $PSScriptRoot '../../scripts/Create-SyncPatch.ps1'

    function Invoke-FixtureGit {
        param([string]$Repository, [string[]]$Arguments)
        $output = & git -C $Repository -c user.name=sync-test -c user.email=sync-test@example.com -c commit.gpgSign=false @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
        return ($output -join "`n").Trim()
    }

    function New-SyncFixture {
        param([string]$Root)
        $source = Join-Path $Root 'source'
        New-Item -ItemType Directory -Path "$source/.github/skills/azsdk-common-test", "$source/.github/skills/private-test" -Force | Out-Null
        Set-Content -LiteralPath "$source/.github/skills/azsdk-common-test/SKILL.md" -Value 'Original shared skill'
        Set-Content -LiteralPath "$source/.github/skills/private-test/SKILL.md" -Value 'Original private skill'
        Set-Content -LiteralPath "$source/README.md" -Value 'Original readme'
        $null = Invoke-FixtureGit $source @('init', '--initial-branch=main')
        $null = Invoke-FixtureGit $source @('add', '--', '.github/skills', 'README.md')
        $null = Invoke-FixtureGit $source @('commit', '-m', 'Initial fixture')
        $base = Invoke-FixtureGit $source @('rev-parse', 'HEAD')
        $tree = Invoke-FixtureGit $source @('rev-parse', 'HEAD^{tree}')
        $otherParent = Invoke-FixtureGit $source @('commit-tree', $tree, '-p', $base, '-m', 'Other parent')
        Set-Content -LiteralPath "$source/.github/skills/azsdk-common-test/SKILL.md" -Value 'Updated shared skill'
        Set-Content -LiteralPath "$source/.github/skills/private-test/SKILL.md" -Value 'Updated private skill'
        Set-Content -LiteralPath "$source/README.md" -Value 'Updated readme'
        $null = Invoke-FixtureGit $source @('add', '--', '.github/skills', 'README.md')
        $tree = Invoke-FixtureGit $source @('write-tree')
        $merge = Invoke-FixtureGit $source @('commit-tree', $tree, '-p', $base, '-p', $otherParent, '-m', 'Merge-only changes')
        $null = Invoke-FixtureGit $source @('checkout', '-b', 'feature', $merge)
        return [pscustomobject]@{ Source = $source; Base = $base; Head = $merge }
    }
}

Describe 'Create directory sync mail patches' -Tag 'UnitTest' {
    It 'applies merge-only changes without changing source refs or unrelated consumer files' {
        $fixture = New-SyncFixture (Join-Path $TestDrive 'merge-only')
        $refsBefore = Invoke-FixtureGit $fixture.Source @('show-ref')
        $oldPatch = Invoke-FixtureGit $fixture.Source @('format-patch', '--stdout', 'HEAD...main', '--', '.github/skills/azsdk-common-*')
        $oldPatch | Should -BeNullOrEmpty
        $patchDirectory = Join-Path $TestDrive 'mail-patches'
        Push-Location $fixture.Source
        try {
            & $syncPatchScript -DirectoryToSync '.github/skills' -FilePatternsJson '["azsdk-common-*"]' -TargetBranch main -OutputDirectory $patchDirectory | Out-Null
        } finally { Pop-Location }
        $patches = @(Get-ChildItem -LiteralPath $patchDirectory -File)
        $patches.Count | Should -Be 1
        (Get-Content -LiteralPath $patches[0].FullName -Raw) | Should -Match 'diff --git a/.github/skills/azsdk-common-test/SKILL.md'
        (Get-Content -LiteralPath $patches[0].FullName -Raw) | Should -Not -Match 'private-test|diff --git a/README.md'
        (Invoke-FixtureGit $fixture.Source @('show-ref')) | Should -BeExactly $refsBefore
        (Invoke-FixtureGit $fixture.Source @('rev-parse', 'HEAD')) | Should -BeExactly $fixture.Head
        (Invoke-FixtureGit $fixture.Source @('status', '--porcelain')) | Should -BeNullOrEmpty
        $consumer = Join-Path $TestDrive 'consumer'
        $null = Invoke-FixtureGit $fixture.Source @('clone', '--no-hardlinks', '--branch', 'main', $fixture.Source, $consumer)
        $null = Invoke-FixtureGit $consumer @('am', '-3', $patches[0].FullName)
        (Get-Content -LiteralPath "$consumer/.github/skills/azsdk-common-test/SKILL.md" -Raw).Trim() | Should -BeExactly 'Updated shared skill'
        (Get-Content -LiteralPath "$consumer/.github/skills/private-test/SKILL.md" -Raw).Trim() | Should -BeExactly 'Original private skill'
        (Get-Content -LiteralPath "$consumer/README.md" -Raw).Trim() | Should -BeExactly 'Original readme'
    }

    It 'supports default patterns and multiple patterns' -TestCases @(
        @{ Patterns = '[]'; Name = 'default' }
        @{ Patterns = '["azsdk-common-*", "private-*"]'; Name = 'multiple' }
    ) {
        param([string]$Patterns, [string]$Name)
        $fixture = New-SyncFixture (Join-Path $TestDrive $Name)
        $patchDirectory = Join-Path $TestDrive "$Name-patches"
        Push-Location $fixture.Source
        try {
            & $syncPatchScript -DirectoryToSync '.github/skills' -FilePatternsJson $Patterns -TargetBranch main -OutputDirectory $patchDirectory | Out-Null
        } finally { Pop-Location }
        $patches = @(Get-ChildItem -LiteralPath $patchDirectory -File)
        $patches.Count | Should -Be 1
        $patchText = Get-Content -LiteralPath $patches[0].FullName -Raw
        $patchText | Should -Match 'azsdk-common-test/SKILL.md'
        $patchText | Should -Match 'private-test/SKILL.md'
        $patchText | Should -Not -Match 'diff --git a/README.md'
    }

    It 'rejects an empty requested delta instead of publishing an empty patch' {
        $fixture = New-SyncFixture (Join-Path $TestDrive 'empty')
        Push-Location $fixture.Source
        try {
            { & $syncPatchScript -DirectoryToSync 'missing-directory' -TargetBranch main -OutputDirectory (Join-Path $TestDrive 'empty-patches') } | Should -Throw '*No changes match*'
        } finally { Pop-Location }
    }

    It 'wires the helper into the existing sparse checkout and patch artifact contract' {
        Import-Module powershell-yaml
        $root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
        $template = ConvertFrom-Yaml (Get-Content -LiteralPath "$root/eng/pipelines/templates/stages/archetype-sdk-tool-repo-sync.yml" -Raw)
        $steps = $template.stages[0].Values.jobs[0].steps
        $checkout = @($steps | Where-Object { $_ -is [Collections.IDictionary] -and $_.Contains('template') -and $_.template -like '*sparse-checkout.yml' })
        $checkout[0].parameters.Paths | Should -Contain 'eng/scripts'
        $creation = @($steps | Where-Object { $_ -is [Collections.IDictionary] -and $_.Contains('displayName') -and $_.displayName -eq 'Create Patch Files from Changes in PR' })
        $creation.Count | Should -Be 1
        $creation[0].inputs.filePath | Should -BeExactly '$(Build.SourcesDirectory)/eng/scripts/Create-SyncPatch.ps1'
        $creation[0].inputs.arguments | Should -Match 'FilePatternsJson|TargetBranch|OutputDirectory'
    }
}
