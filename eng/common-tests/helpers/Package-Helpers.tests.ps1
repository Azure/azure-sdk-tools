Import-Module Pester

Describe "Matrix-Collation" {
    BeforeAll {
        . $PSScriptRoot/../../common/scripts/Helpers/Package-Helpers.ps1

        $ClientCoreMatrixConfig = @{
            Name = "clientcore_ci_test_base"
            Path = "sdk/clientcore/platform-matrix.json"
            Selection = "sparse"
            NonSparseParameters = "Agent"
            GenerateVMJobs = $true
        }

        $CoreMatrixConfig = @{
            Name = "Java_ci_test_base"
            Path = "eng/pipelines/templates/stages/platform-matrix.json"
            Selection = "sparse"
            NonSparseParameters = "Agent"
            GenerateVMJobs = $true
        }
    }

    It "Should properly group identical matrix inputs" {
        $Pkgs = @(
            @{
                Name = "package1"
                CIMatrixConfigs = @($CoreMatrixConfig)
            }
            @{
                Name = "package2"
                CIMatrixConfigs = @($ClientCoreMatrixConfig)
            },
            @{
                Name = "package3"
                CIMatrixConfigs = @($CoreMatrixConfig, $ClientCoreMatrixConfig)
            }
        )

        $groupingResults = Group-ByObjectKey -Items $Pkgs -GroupByProperty "CIMatrixConfigs"

        $groupingResults | Should -Not -BeNullOrEmpty
        $groupingResults.Keys | Should -HaveCount 2

        $group1 = $groupingResults.Keys | Select-Object -First 1
        $groupingResults[$group1] | Should -HaveCount 2
        $groupingResults[$group1][0].Name | Should -Be "package2"
        $groupingResults[$group1][1].Name | Should -Be "package3"

        $group2 = $groupingResults.Keys | Select-Object -Last 1
        $groupingResults[$group2] | Should -HaveCount 2
        $groupingResults[$group2][0].Name | Should -Be "package1"
        $groupingResults[$group2][1].Name | Should -Be "package3"
    }

    It "Should properly group items with no setting" {
        $Pkgs = @(
            @{
                Name = "package1"
                CIMatrixConfigs = @()
            }
            @{
                Name = "package2"
                CIMatrixConfigs = @()
            },
            @{
                Name = "package3"
                CIMatrixConfigs = @()
            }
        )

        $groupingResults = Group-ByObjectKey -Items $Pkgs -GroupByProperty "CIMatrixConfigs"

        $groupingResults | Should -Not -BeNullOrEmpty
        $groupingResults.Keys | Should -HaveCount 1
        $key = $groupingResults.Keys | Select-Object -First 1
        $groupingResults[$key] | Should -HaveCount 3
    }
}
