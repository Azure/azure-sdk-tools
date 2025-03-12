Import-Module Pester

# Load scenarios before we enter the Describe block so they're available for -ForEach
$netScenarios = Get-Content (Join-Path $PSScriptRoot net_scenarios.json) | ConvertFrom-Json
$pythonScenarios = Get-Content (Join-Path $PSScriptRoot python_scenarios.json) | ConvertFrom-Json

Describe "Acceptance tests for .NET PR Matrix Generation" {
    BeforeAll {
        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo "Azure/azure-sdk-for-net" -Reference "331c07a1ab59ed0042972ca6d0df830df235280f"
    }

    It "Should evaluate .NET core diffs correctly - <name>" -ForEach $netScenarios {
        $scenario = $_
        $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
        $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
        $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
            | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
            | Sort-Object -Property Name

        ($detectedOutputs | ConvertTo-Json -Depth 100) | Should -Be ($expectedOutputs | ConvertTo-Json -Depth 100)
    }
}

Describe "Acceptance tests for Python PR Matrix Generation" {
    BeforeAll {
        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo "Azure/azure-sdk-for-python" -Reference "516977e3a0f9ba22d5611608f64b836fefffc37e"
    }

    It "Should evaluate python diffs correctly - <name>" -ForEach $pythonScenarios {
        $scenario = $_
        $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
        $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
        $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
            | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
            | Sort-Object -Property Name

        ($detectedOutputs | ConvertTo-Json -Depth 100) | Should -Be ($expectedOutputs | ConvertTo-Json -Depth 100)
    }
}