Import-Module Pester

# Load scenarios before we enter the Describe block so they're available for -ForEach
$script:netScenarios = Get-Content (Join-Path $PSScriptRoot net_scenarios.json) | ConvertFrom-Json -AsHashtable
$script:pythonScenarios = Get-Content (Join-Path $PSScriptRoot python_scenarios.json) | ConvertFrom-Json -AsHashtable
$script:jsScenarios = Get-Content (Join-Path $PSScriptRoot js_scenarios.json) | ConvertFrom-Json -AsHashtable
$script:goScenarios = Get-Content (Join-Path $PSScriptRoot go_scenarios.json) | ConvertFrom-Json -AsHashtable
$script:javaScenarios = Get-Content (Join-Path $PSScriptRoot java_scenarios.json) | ConvertFrom-Json -AsHashtable

# Set this to $true to update expected outputs in the scenario files
$script:updateScenarios = $false

# due to relative slowness of these tests, we're limiting them to linux only for now.
Describe ".NET Get-PrPkgProperties Tests" -Skip:($IsWindows -or $IsMacOS) -Tag "IntegrationTest" {
    BeforeAll {
        $NET_REPO = "Azure/azure-sdk-for-net"
        $NET_REPO_REF = "5594fe195625e2816ed54d556d8948a8f60d862c"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $NET_REPO -Reference $NET_REPO_REF
    }

    It -Name { "Should evaluate targeted .NET packages correctly - <name>" } -ForEach $netScenarios {
        $scenario = $_
        $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
        $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
        $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
            | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json -AsHashtable }
            | Sort-Object -Property Name

        if ($updateScenarios) {
            $_.expected_package_output = $detectedOutputs
        }
        Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
    }

    AfterEach { 
        if ($updateScenarios) { 
            $netScenarios | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $PSScriptRoot net_scenarios.json)
        }
        
    }
}

Describe "Python Get-PrPkgProperties Tests" -Skip:($IsWindows -or $IsMacOS) -Tag "IntegrationTest" {
    BeforeAll {
        $PYTHON_REPO_REF = "7656cf20f78b7653522040e372a37ff03338b1a2"
        $PYTHON_REPO = "Azure/azure-sdk-for-python"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $PYTHON_REPO -Reference $PYTHON_REPO_REF
    }

    It -Name { "Should evaluate targeted python packages correctly - <name>" } -ForEach $pythonScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $PSItem

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json -AsHashtable }
                | Sort-Object -Property Name

            if ($updateScenarios) {
                $_.expected_package_output = $detectedOutputs
            }
            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }

    AfterAll {
        if ($updateScenarios) {
            $pythonScenarios | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $PSScriptRoot python_scenarios.json)
        }
    }
}

Describe "JS Get-PrPkgProperties Tests" -Skip:($IsWindows -or $IsMacOS) -Tag "IntegrationTest" {
    BeforeAll {
        $JS_REPO_REF = "e2598ca60018edc7b0c3a5b3a28ae7fb40b85894"
        $JS_REPO = "Azure/azure-sdk-for-js"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $JS_REPO -Reference $JS_REPO_REF
    }

    It -Name { "Should evaluate targeted js packages correctly - <name>" } -ForEach $jsScenarios {
        Write-Host "Operating against repo: $RepoRoot"
        $scenario = $PSItem

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ | ConvertFrom-Json -AsHashtable }
                | Sort-Object -Property Name

            
            if ($updateScenarios) {
                $_.expected_package_output = $detectedOutputs
            }
            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }

    AfterAll {
        if ($updateScenarios) {
            $jsScenarios | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $PSScriptRoot js_scenarios.json)
        }
    }
}

Describe "Go Get-PrPkgProperties Tests" -Skip:($IsWindows -or $IsMacOS) -Tag "IntegrationTest" {
    BeforeAll {
        $GO_REPO_REF = "f7328681bcccd0bebad6e8ea8b9c8a5c753368d2"
        $GO_REPO = "Azure/azure-sdk-for-go"

        . $PSScriptRoot/pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $GO_REPO -Reference $GO_REPO_REF
    }

    It "Should evaluate targeted go packages correctly - <name>" -ForEach $goScenarios {
        $scenario = $_
        Write-Host "Operating against repo: $RepoRoot"

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object { Get-Content -Raw $_ |  ConvertFrom-Json -AsHashtable  }
                | Sort-Object -Property Name

            if ($updateScenarios) {
                $_.expected_package_output = $detectedOutputs
            }
            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }

    AfterAll {
        if ($updateScenarios) {
            $goScenarios | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $PSScriptRoot go_scenarios.json)
        }
    }
}

Describe "Java Get-PrPkgProperties Tests" -Skip:($IsWindows -or $IsMacOS) -Tag "IntegrationTest" {
    BeforeAll {
        $JAVA_REPO_REF = "296f1fad306a49601ec61280eb4af1f33934ccde"
        $JAVA_REPO = "Azure/azure-sdk-for-java"

        . $PSScriptRoot\pr-matrix-generation-acceptance.helpers.ps1
        $RepoRoot = Get-Repo -Repo $JAVA_REPO -Reference $JAVA_REPO_REF
    }

    It "Should evaluate targeted java packages correctly - <name>" -ForEach $javaScenarios {
        $scenario = $_
        Write-Host "Operating against repo: $RepoRoot"

        if (-not $scenario.diff) {
            Write-Host "Skipping scenario with no diff"
            return
        }
        else {
            $outputProps = Invoke-PackageProps -InputDiff $scenario.diff -Repo "$RepoRoot"
            $expectedOutputs = $scenario.expected_package_output | Sort-Object -Property Name
            $detectedOutputs = Get-ChildItem -Path $outputProps -Recurse -Filter "*.json" -Exclude "pr-diff.json" `
                | ForEach-Object {
                    Get-Content -Raw $_ | ConvertFrom-Json -AsHashtable
                } | Sort-Object -Property Name

            if ($updateScenarios) {
                $_.expected_package_output = $detectedOutputs
            }
            Compare-PackageResults -Actual $detectedOutputs -Expected $expectedOutputs
        }
    }

    AfterAll {
        if ($updateScenarios) {
            $javaScenarios | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $PSScriptRoot java_scenarios.json)
        }
    }
}