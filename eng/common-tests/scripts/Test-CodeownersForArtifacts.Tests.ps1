Import-Module Pester

Describe "Test-CodeownersForArtifacts" -Tag "UnitTest", "Test-CodeownersForArtifacts" {
    BeforeAll {
        $script:scriptPath = (Resolve-Path "$PSScriptRoot/../../common/scripts/Test-CodeownersForArtifacts.ps1").Path

        # Dot-source the script under test so its helper functions are callable. An empty
        # package-info directory makes the script's main loop a no-op, so nothing invokes the CLI.
        $script:emptyPackageInfo = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
        New-Item -ItemType Directory -Path $script:emptyPackageInfo -Force | Out-Null

        . $script:scriptPath `
            -AzsdkPath 'azsdk' `
            -PackageInfoDirectory $script:emptyPackageInfo `
            -SdkTypes @('client') `
            -Repo 'Azure/azure-sdk-for-net' | Out-Null

        Remove-Item -Recurse -Force $script:emptyPackageInfo -ErrorAction SilentlyContinue

        $script:checkPackageJson = @'
{
  "directory_path": "sdk/template/Azure.Template",
  "issues": [
    {
      "code": "insufficient_owners",
      "message": "resolved package entry has 1 unique owner(s); at least 2 are required.",
      "next_step": "Add <github-aliases-to-add> to the owners list"
    },
    {
      "code": "insufficient_service_owners",
      "message": "service label EngSys has 1 unique owner(s); at least 2 are required.",
      "next_step": "Add <github-aliases-to-add> to the service owners list"
    }
  ],
  "ExitCode": 1
}
'@
    }

    Context "getCheckPackageResponse" {
        It "parses the check-package payload" {
            $response = getCheckPackageResponse -OutputText $script:checkPackageJson

            $response | Should -Not -BeNullOrEmpty
            $response.directory_path | Should -BeExactly 'sdk/template/Azure.Template'
            @($response.issues).Count | Should -Be 2
        }

        It "returns null when the output is not JSON" {
            getCheckPackageResponse -OutputText 'not json at all' | Should -BeNullOrEmpty
        }

        It "returns null for empty output" {
            getCheckPackageResponse -OutputText '' | Should -BeNullOrEmpty
        }
    }

    Context "getCheckPackageIssues" {
        It "maps message and next_step onto the reported issues" {
            $response = getCheckPackageResponse -OutputText $script:checkPackageJson

            # Assign before wrapping, the way the script consumes this function.
            $issues = getCheckPackageIssues -CheckPackageResponse $response
            $issues = @($issues)

            $issues.Count | Should -Be 2
            $issues[0].Message | Should -BeExactly 'resolved package entry has 1 unique owner(s); at least 2 are required.'
            $issues[0].Prompt | Should -BeExactly 'Add <github-aliases-to-add> to the owners list'
        }

        It "returns an empty array when the response could not be parsed" {
            $issues = getCheckPackageIssues -CheckPackageResponse $null

            @($issues).Count | Should -Be 0
        }
    }

    Context "getCheckPackageResponseError" {
        It "returns null when the response omits response_error" {
            $response = getCheckPackageResponse -OutputText $script:checkPackageJson

            getCheckPackageResponseError -CheckPackageResponse $response | Should -BeNullOrEmpty
        }

        It "returns the error when the response carries one" {
            $response = getCheckPackageResponse -OutputText '{"response_error":"boom"}'

            getCheckPackageResponseError -CheckPackageResponse $response | Should -BeExactly 'boom'
        }

        It "returns null when the response could not be parsed" {
            getCheckPackageResponseError -CheckPackageResponse $null | Should -BeNullOrEmpty
        }
    }

    Context "check-package invocation" {
        BeforeAll {
            $script:workDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
            $packageInfoDir = Join-Path $script:workDir 'packageinfo'
            New-Item -ItemType Directory -Path $packageInfoDir -Force | Out-Null

            @{
                Name = 'Azure.Template'
                DirectoryPath = 'sdk/template/Azure.Template'
                SdkType = 'client'
                ReleaseStatus = '2026-09-10'
                ArtifactDetails = @{ name = 'Azure.Template' }
            } | ConvertTo-Json | Set-Content -Path (Join-Path $packageInfoDir 'Azure.Template.json')

            # A real child process is required here: the CLI writes a failing command's JSON
            # response to stderr and a succeeding one to stdout, and only a native process
            # reproduces that split.
            Set-Content -Path (Join-Path $script:workDir 'failure.txt') -Value $script:checkPackageJson
            Set-Content -Path (Join-Path $script:workDir 'success.txt') `
                -Value '{ "directory_path": "sdk/template/Azure.Template", "issues": [] }'

            if ($IsWindows) {
                $script:failingCli = Join-Path $script:workDir 'failing-azsdk.cmd'
                Set-Content -Path $script:failingCli -Value @(
                    '@echo off'
                    'type "%~dp0failure.txt" 1>&2'
                    'exit /b 1'
                )

                $script:passingCli = Join-Path $script:workDir 'passing-azsdk.cmd'
                Set-Content -Path $script:passingCli -Value @(
                    '@echo off'
                    'type "%~dp0success.txt"'
                    'exit /b 0'
                )
            }
            else {
                $script:failingCli = Join-Path $script:workDir 'failing-azsdk'
                Set-Content -Path $script:failingCli -Value @(
                    '#!/bin/sh'
                    'cat "$(dirname "$0")/failure.txt" >&2'
                    'exit 1'
                )
                chmod +x $script:failingCli

                $script:passingCli = Join-Path $script:workDir 'passing-azsdk'
                Set-Content -Path $script:passingCli -Value @(
                    '#!/bin/sh'
                    'cat "$(dirname "$0")/success.txt"'
                    'exit 0'
                )
                chmod +x $script:passingCli
            }

            $pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue)?.Source
            if (!$pwshPath) {
                $pwshPath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
            }

            # Run in a child process so the script's exit code and preference variables are contained.
            $script:failOutput = (& $pwshPath -NoProfile -File $script:scriptPath `
                -AzsdkPath $script:failingCli `
                -PackageInfoDirectory $packageInfoDir `
                -SdkTypes 'client' `
                -Repo 'Azure/azure-sdk-for-net') -join [Environment]::NewLine
            $script:failExitCode = $LASTEXITCODE

            $script:passOutput = (& $pwshPath -NoProfile -File $script:scriptPath `
                -AzsdkPath $script:passingCli `
                -PackageInfoDirectory $packageInfoDir `
                -SdkTypes 'client' `
                -Repo 'Azure/azure-sdk-for-net') -join [Environment]::NewLine
            $script:passExitCode = $LASTEXITCODE
        }

        AfterAll {
            Remove-Item -Recurse -Force $script:workDir -ErrorAction SilentlyContinue
        }

        It "reports the issue details from a failing command" {
            $script:failOutput | Should -Match 'Error: resolved package entry has 1 unique owner'
            $script:failOutput | Should -Match 'Error: service label EngSys has 1 unique owner'
            $script:failOutput | Should -Match 'Use this prompt template to fix:'
        }

        It "parses the response the CLI writes to stderr when it fails" {
            $script:failOutput | Should -Not -Match 'Unable to parse check-package output'
        }

        It "fails the build when a package has issues" {
            $script:failExitCode | Should -Be 1
        }

        It "accepts the response the CLI writes to stdout when it succeeds" {
            $script:passOutput | Should -Match 'Codeowners validation succeeded for package'
            $script:passExitCode | Should -Be 0
        }
    }
}
