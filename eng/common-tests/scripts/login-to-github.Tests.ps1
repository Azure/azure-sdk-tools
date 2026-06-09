Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../../common/scripts/login-to-github.ps1
}

Describe "Get-GitHubInstallationId" -Tag "UnitTest", "login-to-github" {
    It "returns the matching installation id for the requested owner" {
        Mock Invoke-RestMethod {
            @(
                @{
                    id = 42
                    account = @{ login = 'Azure' }
                    target_type = 'Organization'
                }
            )
        } -ParameterFilter {
            $Method -eq 'Get' -and $Uri -eq 'https://api.github.com/app/installations'
        }

        $id = Get-GitHubInstallationId -Jwt 'fake-jwt' -ApiBase 'https://api.github.com' -ApiVersion '2022-11-28' -InstallationTokenOwner 'Azure'

        $id | Should -Be 42
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly
    }

    It "throws when no installation matches the requested owner" {
        Mock Invoke-RestMethod {
            @(
                @{
                    id = $null
                    account = @{ login = 'DifferentOwner' }
                    target_type = 'Organization'
                }
            )
        } -ParameterFilter {
            $Method -eq 'Get' -and $Uri -eq 'https://api.github.com/app/installations'
        }

        { Get-GitHubInstallationId -Jwt 'fake-jwt' -ApiBase 'https://api.github.com' -ApiVersion '2022-11-28' -InstallationTokenOwner 'Azure' } | Should -Throw "No installations found for 'Azure' on this App."
    }
}
