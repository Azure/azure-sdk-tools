Import-Module Pester

Describe "Package triggering paths" -Tag "UnitTest" {
    BeforeAll {
        $repoRoot = (Get-Item $TestDrive).FullName
        $scriptsPath = Join-Path $repoRoot "eng" "common" "scripts"
        $helpersPath = Join-Path $scriptsPath "Helpers"
        New-Item -ItemType Directory -Path $helpersPath -Force | Out-Null
        Copy-Item (Join-Path $PSScriptRoot ".." ".." "common" "scripts" "Package-Properties.ps1") $scriptsPath
        Copy-Item (Join-Path $PSScriptRoot ".." ".." "common" "scripts" "logging.ps1") $scriptsPath
        Copy-Item (Join-Path $PSScriptRoot ".." ".." "common" "scripts" "Helpers" "Package-Helpers.ps1") $helpersPath
        Copy-Item (Join-Path $PSScriptRoot ".." ".." "common" "scripts" "Helpers" "PSModule-Helpers.ps1") $helpersPath

        foreach ($path in @("sdk\dep-alpha", "sdk\dep-beta", "sdk\service\package")) {
            New-Item -ItemType Directory -Path (Join-Path $repoRoot $path) -Force | Out-Null
        }
        @'
extends:
  parameters:
    Artifacts:
      - name: test-package
        triggeringPaths:
          - /sdk/dep-*
'@ | Set-Content (Join-Path $repoRoot "sdk" "service" "ci.yml")

        . (Join-Path $scriptsPath "Package-Properties.ps1")
        function LoadFrom-Yaml {
            param([string] $YmlFile)
            return @{
                extends = @{
                    parameters = @{
                        Artifacts = @(
                            @{
                                name = "test-package"
                                triggeringPaths = @("/sdk/dep-*")
                            }
                        )
                    }
                }
            }
        }
        $previousGitHubActions = $env:GITHUB_ACTIONS
        $previousRepoRoot = Get-Variable RepoRoot -ErrorAction SilentlyContinue
        $env:GITHUB_ACTIONS = "true"
        $RepoRoot = $repoRoot
        $AdditionalValidationPackagesFromPackageSetFn = ""
        $script:testPackage = New-Object PackageProps -ArgumentList @(
            "test-package", "1.0.0", (Join-Path $repoRoot "sdk" "service" "package"), "service"
        )
        function Get-AllPackageInfoFromRepo {
            param([string] $ServiceDirectory)
            return $script:testPackage
        }
    }

    AfterAll {
        $env:GITHUB_ACTIONS = $previousGitHubActions
        if ($previousRepoRoot) {
            $global:RepoRoot = $previousRepoRoot.Value
        }
        else {
            Remove-Variable RepoRoot -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It "selects the package when the first matching dependency changes" {
        $diffPath = Join-Path $TestDrive "diff.json"
        @{
            ChangedFiles = @("sdk/dep-alpha/src/file.cs")
            DeletedFiles = @()
            ExcludePaths = @()
        } | ConvertTo-Json | Set-Content $diffPath

        $packages = @(Get-PrPkgProperties $diffPath)

        $packages.Count | Should Be 1
        $packages[0].Name | Should Be "test-package"
        ($packages[0].ArtifactDetails["triggeringPaths"] -contains "/sdk/dep-alpha") | Should Be $true
        ($packages[0].ArtifactDetails["triggeringPaths"] -contains "/sdk/dep-beta") | Should Be $true
    }

    It "selects the package when the second matching dependency changes" {
        $diffPath = Join-Path $TestDrive "diff.json"
        @{
            ChangedFiles = @("sdk/dep-beta/src/file.cs")
            DeletedFiles = @()
            ExcludePaths = @()
        } | ConvertTo-Json | Set-Content $diffPath

        $packages = @(Get-PrPkgProperties $diffPath)

        $packages.Count | Should Be 1
        $packages[0].Name | Should Be "test-package"
        ($packages[0].ArtifactDetails["triggeringPaths"] -contains "/sdk/dep-alpha") | Should Be $true
        ($packages[0].ArtifactDetails["triggeringPaths"] -contains "/sdk/dep-beta") | Should Be $true
    }
}
