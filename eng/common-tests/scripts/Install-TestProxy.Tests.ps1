Import-Module Pester

Describe "Install-TestProxy" -Tag "UnitTest" {
    BeforeAll {
        $script:scriptPath = Join-Path $PSScriptRoot '..\..\common\scripts\Install-TestProxy.ps1'
        $script:originalLastExitCode = $global:LASTEXITCODE

        function dotnet {
            param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
            return $Arguments
        }
    }

    AfterAll {
        $global:LASTEXITCODE = $script:originalLastExitCode
    }

    BeforeEach {
        Mock Get-Content { return '1.0.0-dev.20260521.2' }
        Mock Test-Path { return $false }
        Mock dotnet { $global:LASTEXITCODE = 0 }
        Mock Write-Host {}
    }

    It "installs from only the Azure SDK feed before configuring the proxy" {
        & $script:scriptPath -TemplateRoot $TestDrive -BinariesDirectory $TestDrive

        Should -Invoke dotnet -Times 1 -Exactly -ParameterFilter {
            ($Arguments -join ' ') -eq "tool install azure.sdk.tools.testproxy --tool-path $TestDrive/test-proxy --source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json --version 1.0.0-dev.20260521.2"
        }
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
            $Object -eq "##vso[task.prependpath]$TestDrive/test-proxy"
        }
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
            $Object -eq '##vso[task.setvariable variable=PROXY_MANUAL_START]true'
        }
    }

    It "fails before publishing PATH or proxy configuration when installation fails" {
        Mock dotnet { $global:LASTEXITCODE = 17 }

        {
            & $script:scriptPath -TemplateRoot $TestDrive -BinariesDirectory $TestDrive
        } | Should -Throw '*Failed to install test-proxy version 1.0.0-dev.20260521.2 (dotnet exited with code 17).*'

        Should -Invoke Write-Host -Times 0 -Exactly -ParameterFilter {
            $Object -like '##vso*'
        }
    }
}
