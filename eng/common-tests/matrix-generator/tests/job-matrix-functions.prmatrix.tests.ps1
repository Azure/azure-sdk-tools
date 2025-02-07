Import-Module Pester

# todo, move this to package helpers tests versus job matrix functions

Describe "Matrix-Collation" {
    BeforeAll {
        . $PSScriptRoot/../../../common/scripts/job-matrix/job-matrix-functions.ps1
        . $PSScriptRoot/../../../common/scripts/Helpers/Package-Helpers.ps1

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
    }
}
