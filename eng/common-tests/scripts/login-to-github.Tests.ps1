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

    It "throws when the matching installation has no id" {
        Mock Invoke-RestMethod {
            @(
                @{
                    id = $null
                    account = @{ login = 'Azure' }
                    target_type = 'Organization'
                }
            )
        } -ParameterFilter {
            $Method -eq 'Get' -and $Uri -eq 'https://api.github.com/app/installations'
        }

        { Get-GitHubInstallationId -Jwt 'fake-jwt' -ApiBase 'https://api.github.com' -ApiVersion '2022-11-28' -InstallationTokenOwner 'Azure' } | Should -Throw "No installations with a valid id found for 'Azure' on this App."
    }
}

Describe "New-GitHubInstallationToken" -Tag "UnitTest", "login-to-github" {
    BeforeAll {
        $script:tokenParameters = @{
            Jwt = 'fake-jwt'
            InstallationId = '42'
            ApiBase = 'https://api.github.com'
            ApiVersion = '2022-11-28'
        }
        $script:copilotTokenRequest = @{
            repositories = @('azure-sdk-tools')
            permissions = @{
                organization_copilot_requests = 'read'
                metadata = 'read'
            }
        }
    }

    It "preserves the default request without a body" {
        Mock Invoke-RestMethod { @{ token = 'ghs_test-token' } }

        New-GitHubInstallationToken @tokenParameters | Should -Be 'ghs_test-token'

        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'Post' -and $Uri -eq 'https://api.github.com/app/installations/42/access_tokens' -and
            -not $Body -and -not $ContentType -and $TimeoutSec -eq 30 -and $MaximumRetryCount -eq 3
        }
    }

    It "requests only the specified repositories and permissions" {
        Mock Invoke-RestMethod {
            @{
                token = 'ghs_scoped-token'
                permissions = @{ organization_copilot_requests = 'read'; metadata = 'read' }
            }
        }

        New-GitHubInstallationToken @tokenParameters -TokenRequest $copilotTokenRequest | Should -Be 'ghs_scoped-token'

        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $requestBody = ConvertFrom-Json -InputObject $Body -AsHashtable
            $ContentType -eq 'application/json' -and $requestBody.Count -eq 2 -and
            $requestBody.repositories.Count -eq 1 -and $requestBody.repositories[0] -eq 'azure-sdk-tools' -and
            $requestBody.permissions.Count -eq 2 -and
            $requestBody.permissions.organization_copilot_requests -eq 'read' -and
            $requestBody.permissions.metadata -eq 'read'
        }
    }

    It "rejects a response without a token" {
        Mock Invoke-RestMethod { @{} }

        { New-GitHubInstallationToken @tokenParameters } | Should -Throw 'Failed to obtain installation access token for installation 42.'
    }

    It "rejects a token missing the requested Copilot permission" {
        Mock Invoke-RestMethod { @{ token = 'ghs_wrong-token'; permissions = @{ metadata = 'read' } } }

        { New-GitHubInstallationToken @tokenParameters -TokenRequest $copilotTokenRequest } |
            Should -Throw "Installation token did not grant requested permission 'organization_copilot_requests': 'read'."
    }
}

Describe "Invoke-LoginToGitHub scoped tokens" -Tag "UnitTest", "login-to-github" {
    BeforeEach {
        $script:originalProjectId = $env:SYSTEM_TEAMPROJECTID
        $script:originalGitHubActions = $env:GITHUB_ACTIONS
        $script:originalToken = $env:COPILOT_GITHUB_TOKEN
        $script:originalGhToken = $env:GH_TOKEN
        $env:SYSTEM_TEAMPROJECTID = 'test-project'
        $env:GITHUB_ACTIONS = 'false'

        Mock New-GitHubAppJwt { 'fake-jwt' }
        Mock Get-GitHubInstallationId { '42' }
        Mock New-GitHubInstallationToken { 'ghs_scoped-token' }
        Mock gh {}
        Mock Write-Host {}
    }

    AfterEach {
        $env:SYSTEM_TEAMPROJECTID = $script:originalProjectId
        $env:GITHUB_ACTIONS = $script:originalGitHubActions
        $env:COPILOT_GITHUB_TOKEN = $script:originalToken
        $env:GH_TOKEN = $script:originalGhToken
    }

    It "uses the existing secret export with the requested variable name" {
        $TokenRequest = '{"repositories":["azure-sdk-tools"],"permissions":{"organization_copilot_requests":"read","metadata":"read"}}'

        Invoke-LoginToGitHub -TokenRequest $TokenRequest -VariableNamePrefix 'COPILOT_GITHUB_TOKEN'

        $env:COPILOT_GITHUB_TOKEN | Should -Be 'ghs_scoped-token'
        Should -Invoke New-GitHubInstallationToken -Times 1 -Exactly -ParameterFilter {
            $TokenRequest.repositories[0] -eq 'azure-sdk-tools' -and
            $TokenRequest.permissions.organization_copilot_requests -eq 'read'
        }
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
            $Object -eq '##vso[task.setvariable variable=COPILOT_GITHUB_TOKEN;issecret=true]ghs_scoped-token'
        }
    }

    It "retains the default token scope when no request is provided" {
        $TokenRequest = ''

        Invoke-LoginToGitHub -TokenRequest $TokenRequest -VariableNamePrefix 'COPILOT_GITHUB_TOKEN'

        Should -Invoke New-GitHubInstallationToken -Times 1 -Exactly -ParameterFilter { $TokenRequest.Count -eq 0 }
    }

    It "rejects a non-object request before signing a JWT" {
        $TokenRequest = '[]'

        { Invoke-LoginToGitHub -TokenRequest $TokenRequest } | Should -Throw 'TokenRequest must be a JSON object.'

        Should -Invoke New-GitHubAppJwt -Times 0 -Exactly
    }
}
