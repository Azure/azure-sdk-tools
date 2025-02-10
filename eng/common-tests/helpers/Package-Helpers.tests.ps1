Import-Module Pester


Describe "ObjectKey Discovery" {
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

        $CoreMatrixConfigWithWeirdBoolean = @{
            Name = "Java_ci_test_base"
            Path = "eng/pipelines/templates/stages/platform-matrix.json"
            Selection = "sparse"
            NonSparseParameters = "Agent"
            GenerateVMJobs = "True"
        }
    }

    It "Should evaluate the same object to the same hashcode" {
        $value1 = Get-ObjectKey -Object $CoreMatrixConfig
        $value2 = Get-ObjectKey -Object $CoreMatrixConfig

        $value1 | Should -BeLikeExactly $value2
    }

    It "Should evaluate different objects with the same value to the same hashcode" {
        $CopiedCoreMatrixConfig = @{
            Name = "Java_ci_test_base"
            Path = "eng/pipelines/templates/stages/platform-matrix.json"
            Selection = "sparse"
            NonSparseParameters = "Agent"
            GenerateVMJobs = $true
        }

        $value1 = Get-ObjectKey -Object $CoreMatrixConfig
        $value2 = Get-ObjectKey -Object $CopiedCoreMatrixConfig

        $value1 | Should -BeLikeExactly $value2
    }

    It "Should properly evaluate different objects with similar values, but convert booleans" {
        $value1 = Get-ObjectKey -Object $CoreMatrixConfig
        $value2 = Get-ObjectKey -Object $CoreMatrixConfigWithWeirdBoolean

        $value1 | Should -BeLikeExactly $value2
    }
}

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

    It -Skip "Should properly group identical matrix inputs" {
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

    It -Skip "Should properly group items with no setting" {
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

    It "Should group differently ordered CIMatrixConfigs together properly" {
        $Pkgs = @(
            @{
                ArtifactName = "azure-core"
                CIMatrixConfigs = @(
                    @{
                        NonSparseParameters= "Agent"
                        Path = "eng/pipelines/templates/stages/platform-matrix.json"
                        Name = "Java_ci_test_base"
                        GenerateVMJobs = $true
                        Selection = "sparse"
                    },
                    @{
                        Path = "sdk/core/version-overrides-matrix.json"
                        Name = "version_overrides_tests"
                        GenerateVMJobs = $true
                        Selection = "all"
                    }
                )
            },
            @{
                ArtifactName = "azure-identity"
                CIMatrixConfigs = @(@{
                    Name = "Java_ci_test_base"
                    Path = "eng/pipelines/templates/stages/platform-matrix.json"
                    Selection = "sparse"
                    NonSparseParameters = "Agent"
                    GenerateVMJobs = "True"
                })
            },
            @{
                ArtifactName = "core"
                CIMatrixConfigs = @(
                    {
                        NonSparseParameters = "Agent",
                        Path = "sdk/clientcore/platform-matrix.json",
                        Name = "clientcore_ci_test_base",
                        GenerateVMJobs = $True,
                        Selection = "sparse"
                    }
                )
            }
        )

        $groupingResults = Group-ByObjectKey -Items $Pkgs -GroupByProperty "CIMatrixConfigs"

        $groupingResults | Should -Not -BeNullOrEmpty

        # expected grouping
            # azure-core, azure-identity in Java_ci_test_base
            # azure-core in version_overrides_tests
            # core in clientcore_ci_test_base
        $groupingResults.Keys | Should -HaveCount 3
    }
}
