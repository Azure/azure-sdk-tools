Import-Module Pester



Describe "Acceptance tests for .NET PR Matrix Generation" {
    BeforeAll {
        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo "Azure/azure-sdk-for-net" -Reference "331c07a1ab59ed0042972ca6d0df830df235280f"
    }

    # todo: parametrize this test to via file
    It "Should evaluate a basic package diff correctly" {
        # todo actually parameterize this instead of going after the first one specifically only
        $scenarios = Get-Content (Join-Path $PSScriptRoot net_scenarios.json) -Raw | ConvertFrom-Json
        $scenario = $scenarios[0]

        $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"

        Write-Host $outputProps
    }
}