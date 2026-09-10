Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../../common/scripts/Helpers/AzSdkTool-Helpers.ps1
}

Describe "Get-GitHubApiHeaders" -Tag "UnitTest", "AzSdkTool-Helpers" {
    It "prefers GitHub CLI authentication over GITHUB_TOKEN" {
        $originalToken = $env:GITHUB_TOKEN
        $lastExitCodeWasSet = Test-Path variable:global:LASTEXITCODE
        if ($lastExitCodeWasSet) {
            $originalLastExitCode = $global:LASTEXITCODE
        }
        $env:GITHUB_TOKEN = "environment-token"
        $global:LASTEXITCODE = 0
        function global:gh {
            return "cli-token"
        }

        try {
            $headers = Get-GitHubApiHeaders
        }
        finally {
            $env:GITHUB_TOKEN = $originalToken
            if ($lastExitCodeWasSet) {
                $global:LASTEXITCODE = $originalLastExitCode
            }
            else {
                Remove-Variable -Scope Global -Name LASTEXITCODE -ErrorAction SilentlyContinue
            }
            Remove-Item -Path function:global:gh
        }

        $headers.Authorization | Should -Be ("Bearer " + "cli-token")
    }

    It "uses GITHUB_TOKEN when GitHub CLI authentication fails" {
        $originalToken = $env:GITHUB_TOKEN
        $env:GITHUB_TOKEN = "environment-token"
        function global:gh {
            throw "GitHub CLI authentication is unavailable."
        }

        try {
            $headers = Get-GitHubApiHeaders
        }
        finally {
            $env:GITHUB_TOKEN = $originalToken
            Remove-Item -Path function:global:gh
        }

        $headers.Authorization | Should -Be ("Bearer " + "environment-token")
    }

    It "returns null when no authentication token is available" {
        $originalToken = $env:GITHUB_TOKEN
        Remove-Item -Path Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
        function global:gh {
            throw "GitHub CLI authentication is unavailable."
        }

        try {
            $headers = Get-GitHubApiHeaders
        }
        finally {
            $env:GITHUB_TOKEN = $originalToken
            Remove-Item -Path function:global:gh
        }

        $headers | Should -BeNullOrEmpty
    }
}

Describe "Install-Standalone-Tool" -Tag "UnitTest", "AzSdkTool-Helpers" {
    BeforeEach {
        $script:originalGitHubToken = $env:GITHUB_TOKEN
        function global:gh {
            throw "GitHub CLI authentication is unavailable."
        }
        Mock Get-Package-Meta {
            @{
                file_name = "tool.tar.gz"
                executable = "tool"
            }
        }
        Mock tar {}
    }

    AfterEach {
        $env:GITHUB_TOKEN = $script:originalGitHubToken
        Remove-Item -Path function:global:gh
    }

    It "uses authorization headers for release discovery when a token is available" {
        $env:GITHUB_TOKEN = "test-token"
        $global:AzSdkToolRestCall = $null
        Mock isNewVersion { $false }
        Mock Invoke-RestMethod {
            param($Uri, $Headers)
            $global:AzSdkToolRestCall = @{
                HasHeaders = $PSBoundParameters.ContainsKey("Headers")
                Headers = $Headers
            }
            @([pscustomobject]@{ tag_name = "tool_1.0.0" })
        }

        Install-Standalone-Tool -Version "*" -FileName "tool" -Package "tool" -Directory $TestDrive

        $global:AzSdkToolRestCall.HasHeaders | Should -BeTrue
        $global:AzSdkToolRestCall.Headers.Authorization | Should -Be ("Bearer " + "test-token")
    }

    It "does not supply headers for release discovery when no token is available" {
        $global:AzSdkToolRestCall = $null
        Remove-Item -Path Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
        Mock isNewVersion { $false }
        Mock Invoke-RestMethod {
            param($Uri, $Headers)
            $global:AzSdkToolRestCall = @{
                HasHeaders = $PSBoundParameters.ContainsKey("Headers")
                Headers = $Headers
            }
            @([pscustomobject]@{ tag_name = "tool_1.0.0" })
        }

        Install-Standalone-Tool -Version "*" -FileName "tool" -Package "tool" -Directory $TestDrive

        $global:AzSdkToolRestCall.HasHeaders | Should -BeFalse
    }

    It "uses authorization headers for downloads when a token is available" {
        $env:GITHUB_TOKEN = "test-token"
        $global:AzSdkToolWebCall = $null
        Mock isNewVersion { $true }
        Mock Invoke-WebRequest {
            param($Uri, $OutFile, $Headers)
            $global:AzSdkToolWebCall = @{
                HasHeaders = $PSBoundParameters.ContainsKey("Headers")
                Headers = $Headers
            }
            Set-Content -Path $OutFile -Value ""
        }

        Install-Standalone-Tool -Version "1.0.0" -FileName "tool" -Package "tool" -Directory $TestDrive

        $global:AzSdkToolWebCall.HasHeaders | Should -BeTrue
        $global:AzSdkToolWebCall.Headers.Authorization | Should -Be ("Bearer " + "test-token")
    }

    It "does not supply headers for downloads when no token is available" {
        $global:AzSdkToolWebCall = $null
        Remove-Item -Path Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
        Mock isNewVersion { $true }
        Mock Invoke-WebRequest {
            param($Uri, $OutFile, $Headers)
            $global:AzSdkToolWebCall = @{
                HasHeaders = $PSBoundParameters.ContainsKey("Headers")
                Headers = $Headers
            }
            Set-Content -Path $OutFile -Value ""
        }

        Install-Standalone-Tool -Version "1.0.0" -FileName "tool" -Package "tool" -Directory $TestDrive

        $global:AzSdkToolWebCall.HasHeaders | Should -BeFalse
    }
}
