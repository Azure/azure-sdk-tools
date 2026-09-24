Import-Module Pester

Describe "ExcludePaths matching" -Tag "UnitTest", "Path-Helpers" {
    BeforeAll {
        . $PSScriptRoot/../../common/scripts/Package-Properties.ps1
    }

    It "matches an exact file" {
        Test-PathExcluded -Path "README.md" -ExcludePaths @("README.md") | Should -BeTrue
    }

    It "does not match a longer file name" {
        Test-PathExcluded -Path "README.md.template" -ExcludePaths @("README.md") | Should -BeFalse
    }

    It "matches a descendant of a trailing-slash directory" {
        Test-PathExcluded -Path "docs/guide.md" -ExcludePaths @("docs/") | Should -BeTrue
    }

    It "does not match a sibling with the same leading characters" {
        Test-PathExcluded -Path "docs-tools/build.ps1" -ExcludePaths @("docs/") | Should -BeFalse
    }

    It "matches exact files and directories case-insensitively" {
        Test-PathExcluded -Path "eng/Versioning/Version_Client.txt" -ExcludePaths @("ENG/versioning/version_client.txt") | Should -BeTrue
        Test-PathExcluded -Path "SDK/Cosmos/azure-cosmos/setup.py" -ExcludePaths @("sdk/cosmos/") | Should -BeTrue
    }

    It "does not interpret glob syntax" {
        Test-PathExcluded -Path "docs/guide.md" -ExcludePaths @("docs/*.md") | Should -BeFalse
    }

    It "applies multiple exclusions" {
        $result = Update-TargetedFilesForExclude `
            -TargetedFiles @("README.md", "docs/guide.md", "src/main.ps1") `
            -ExcludePaths @("README.md", "docs/")

        $result | Should -Be @("src/main.ps1")
    }

    It "preserves all targeted files for empty exclusions" {
        $targetedFiles = @("README.md", "docs/guide.md")

        $result = Update-TargetedFilesForExclude -TargetedFiles $targetedFiles -ExcludePaths @()

        $result | Should -Be $targetedFiles
    }

    It "applies exclusions after changed and deleted paths are combined" {
        $targetedFiles = @(
            "src/changed.ps1"
            "docs/deleted.md"
        )

        $result = Update-TargetedFilesForExclude -TargetedFiles $targetedFiles -ExcludePaths @("docs/")

        $result | Should -Be @("src/changed.ps1")
    }
}
