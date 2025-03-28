Import-Module Pester

# Load scenarios before we enter the Describe block so they're available for -ForEach
$netScenarios = Get-Content (Join-Path $PSScriptRoot net_scenarios.json) | ConvertFrom-Json
$pythonScenarios = Get-Content (Join-Path $PSScriptRoot python_scenarios.json) | ConvertFrom-Json
$jsScenarios = Get-Content (Join-Path $PSScriptRoot js_scenarios.json) | ConvertFrom-Json
$goScenarios = Get-Content (Join-Path $PSScriptRoot go_scenarios.json) | ConvertFrom-Json
$javaScenarios = Get-Content (Join-Path $PSScriptRoot java_scenarios.json) | ConvertFrom-Json

Describe "Acceptance tests for .NET PR Matrix Generation" -Tag "IntegrationTest" {
    BeforeAll {
        $NET_REPO = "Azure/azure-sdk-for-net"
        $NET_REPO_REF = "331c07a1ab59ed0042972ca6d0df830df235280f"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $NET_REPO -Reference $NET_REPO_REF
    }

    It "Should evaluate targeted .NET packages correctly - $($_.name)" -ForEach $netScenarios {
        $scenario = $_
        $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
        $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
        $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
            | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
            | Sort-Object -Property Name

        ($detectedOutputs | ConvertTo-Json -Depth 100) | Should -Be ($expectedOutputs | ConvertTo-Json -Depth 100)
    }
}

Describe "Acceptance tests for Python PR Matrix Generation" -Tag "IntegrationTest" {
    BeforeAll {
        $PYTHON_REPO_REF = "7656cf20f78b7653522040e372a37ff03338b1a2"
        $PYTHON_REPO = "Azure/azure-sdk-for-python"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $PYTHON_REPO -Reference $PYTHON_REPO_REF
    }

    It "Should evaluate targeted pyton packages correctly - $($_.name)" -ForEach $pythonScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $_

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
                | Sort-Object -Property Name

            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }
}

Describe "Acceptance tests for JS PR Matrix Generation" -Tag "IntegrationTest" {
    BeforeAll {
        $JS_REPO_REF = "e2598ca60018edc7b0c3a5b3a28ae7fb40b85894"
        $JS_REPO = "Azure/azure-sdk-for-js"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $JS_REPO -Reference $JS_REPO_REF
    }

    It "Should evaluate targeted js packages correctly - $($_.name)" -ForEach $jsScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $_

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
                | Sort-Object -Property Name

            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }
}

Describe "Acceptance tests for Go PR Matrix Generation" -Tag "IntegrationTest" {
    BeforeAll {
        $GO_REPO_REF = "f7328681bcccd0bebad6e8ea8b9c8a5c753368d2"
        $GO_REPO = "Azure/azure-sdk-for-go"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $GO_REPO -Reference $GO_REPO_REF
    }

    It "Should evaluate targeted go packages correctly - $($_.name)" -ForEach $goScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $_

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
                | Sort-Object -Property Name

            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }
}

Describe "Acceptance tests for Java PR Matrix Generation" -Tag "IntegrationTest" {
    BeforeAll {
        $JAVA_REPO_REF = "296f1fad306a49601ec61280eb4af1f33934ccde"
        $JAVA_REPO = "Azure/azure-sdk-for-java"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $JAVA_REPO -Reference $JAVA_REPO_REF
    }

    It "Should evaluate targeted java packages correctly - $($_.name)" -ForEach $javaScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $_

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json }
                | Sort-Object -Property Name

            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }
}