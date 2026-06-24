Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../../common/scripts/git-branch-push.ps1
}

Describe "Get-ComparableGitUrl" -Tag "UnitTest", "git-branch-push" {
    It "normalizes URLs with credentials to the same comparable form as credential-free URLs" {
        $withCredentials = 'https://user:pass@example.com/Org/Repo.git'
        $withoutCredentials = 'https://example.com/Org/Repo.git'

        $normalizedWithCredentials = Get-ComparableGitUrl -url $withCredentials
        $normalizedWithoutCredentials = Get-ComparableGitUrl -url $withoutCredentials

        $normalizedWithCredentials | Should -Be 'https://example.com/org/repo.git'
        $normalizedWithoutCredentials | Should -Be 'https://example.com/org/repo.git'
        $normalizedWithCredentials | Should -Be $normalizedWithoutCredentials
    }

    It "returns the original value for non-http(s) URLs" {
        $url = 'git@github.com:Azure/azure-sdk-tools.git'

        Get-ComparableGitUrl -url $url | Should -Be $url
    }
}
