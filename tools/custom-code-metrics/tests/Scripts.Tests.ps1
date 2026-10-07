BeforeAll {
    $script:PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    function az { param([Parameter(ValueFromRemainingArguments)][object[]]$Arguments) throw "Unexpected Azure call." }
    function node {
        [CmdletBinding(PositionalBinding = $false)]
        param([Alias("e")][string]$Code, [Parameter(ValueFromRemainingArguments)][object[]]$Arguments)
        throw "Unexpected Node call."
    }
    function npx { param([Parameter(ValueFromRemainingArguments)][object[]]$Arguments) throw "Unexpected npx call." }
    function git { param([Parameter(ValueFromRemainingArguments)][object[]]$Arguments) throw "Unexpected Git call." }
}

Describe "Relocated publication wrapper" {
    BeforeEach {
        $script:Snapshot = Join-Path $TestDrive "snapshot.json"
        Set-Content -LiteralPath $script:Snapshot -Value "{}"
        $script:PreviousToken = $env:AZURE_STORAGE_ACCESS_TOKEN
        $env:AZURE_STORAGE_ACCESS_TOKEN = "previous-test-token"
        Mock az { $global:LASTEXITCODE = 0; return "temporary-test-token" }
        Mock node { $global:LASTEXITCODE = 0 }
    }
    AfterEach {
        $env:AZURE_STORAGE_ACCESS_TOKEN = $script:PreviousToken
    }
    It "uses the relocated publisher from a different cwd and restores credentials" {
        Push-Location $TestDrive
        try {
            & (Join-Path $script:PackageRoot "Publish-Metrics.ps1") -SnapshotPath $script:Snapshot -StorageAccount "testaccount" -SubscriptionId "test-subscription"
            Should -Invoke node -Times 1 -Exactly -ParameterFilter {
                $Arguments[0] -eq (Join-Path $script:PackageRoot "publishing.mjs") -and
                $Arguments[1] -eq $script:Snapshot -and $Arguments[2] -eq "testaccount"
            }
            $env:AZURE_STORAGE_ACCESS_TOKEN | Should -Be "previous-test-token"
        }
        finally { Pop-Location }
    }
    It "does not run the publisher when token acquisition fails" {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Publish-Metrics.ps1") -SnapshotPath $script:Snapshot -StorageAccount "testaccount" -SubscriptionId "test-subscription" } | Should -Throw "*Could not acquire*"
        Should -Invoke node -Times 0 -Exactly
        $env:AZURE_STORAGE_ACCESS_TOKEN | Should -Be "previous-test-token"
    }
    It "surfaces publisher failure and restores credentials" {
        Mock node { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Publish-Metrics.ps1") -SnapshotPath $script:Snapshot -StorageAccount "testaccount" -SubscriptionId "test-subscription" } | Should -Throw "*Metrics publication failed*"
        $env:AZURE_STORAGE_ACCESS_TOKEN | Should -Be "previous-test-token"
    }
    It "rejects a missing input before requesting Azure credentials" {
        { & (Join-Path $script:PackageRoot "Publish-Metrics.ps1") -SnapshotPath (Join-Path $TestDrive "absent.json") -StorageAccount "testaccount" -SubscriptionId "test-subscription" } | Should -Throw
        Should -Invoke az -Times 0 -Exactly
    }
}

Describe "Dashboard deployment preflight" {
    BeforeEach {
        $script:PreviousDeploymentToken = $env:SWA_CLI_DEPLOYMENT_TOKEN
        $script:PreviousDebug = $env:SWA_CLI_DEBUG
        $env:SWA_CLI_DEPLOYMENT_TOKEN = "previous-test-token"
        $env:SWA_CLI_DEBUG = "silly"
        $script:InitialLocation = (Get-Location).Path
        Mock node { $global:LASTEXITCODE = 0 }
        Mock az { $global:LASTEXITCODE = 0; return "temporary-test-token" }
        Mock npx { $global:LASTEXITCODE = 0 }
    }
    AfterEach {
        $env:SWA_CLI_DEPLOYMENT_TOKEN = $script:PreviousDeploymentToken
        $env:SWA_CLI_DEBUG = $script:PreviousDebug
    }
    It "refuses private or failed reporting before credentials or deployment" {
        Mock node { $global:LASTEXITCODE = 1 } -ParameterFilter { $Arguments[0] -eq "--input-type=module" }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl "https://metrics.invalid/dotnet/index.json" } | Should -Throw "*Public reporting preflight failed*"
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
        (Get-Location).Path | Should -Be $script:InitialLocation
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
    }
    It "uses the package build and dist paths and restores environment and cwd" {
        & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl "https://metrics.invalid/dotnet/index.json"
        Should -Invoke node -Times 1 -Exactly -ParameterFilter { $Arguments[0] -eq ".\dashboard\build.mjs" }
        Should -Invoke npx -Times 1 -Exactly -ParameterFilter { $Arguments -contains ".\dashboard\dist" }
        (Get-Location).Path | Should -Be $script:InitialLocation
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
    }
    It "rejects credential-bearing or non-HTTPS indexes without external calls" {
        foreach ($url in @("http://metrics.invalid/index.json", "https://user:password@metrics.invalid/index.json", "https://metrics.invalid/index.json?secret=value")) {
            { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl $url } | Should -Throw "*public HTTPS*"
        }
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
    }
    It "surfaces a failed build without requesting Azure credentials" {
        Mock node { $global:LASTEXITCODE = 1 } -ParameterFilter { $Arguments[0] -eq ".\dashboard\build.mjs" }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl "https://metrics.invalid/dotnet/index.json" } | Should -Throw "*Dashboard build failed*"
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
    }
    It "restores credentials and cwd after deployment failure" {
        Mock npx { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl "https://metrics.invalid/dotnet/index.json" } | Should -Throw "*deployment failed*"
        (Get-Location).Path | Should -Be $script:InitialLocation
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
    }
    It "separates hosted and snapshot-preview parameter sets" {
        $sets = (Get-Command (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1")).ParameterSets
        $sets.Count | Should -Be 2
        ($sets | Where-Object Name -eq "Hosted").Parameters.Name | Should -Contain "IndexUrl"
        ($sets | Where-Object Name -eq "Hosted").Parameters.Name | Should -Not -Contain "SnapshotPath"
        ($sets | Where-Object Name -eq "Preview").Parameters.Name | Should -Contain "SnapshotPath"
        ($sets | Where-Object Name -eq "Preview").Parameters.Name | Should -Not -Contain "IndexUrl"
    }
    It "builds preview snapshots without fetching a feed and passes the deployment secret only through environment" {
        $paths = @((Join-Path $TestDrive "one.json"), (Join-Path $TestDrive "two.json"))
        foreach ($path in $paths) { Set-Content -LiteralPath $path -Value "{}" }
        Mock npx {
            $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "temporary-test-token"
            $env:SWA_CLI_DEBUG | Should -Be "log"
            $Arguments | Should -Not -Contain "temporary-test-token"
            $Arguments | Should -Contain "--no-use-keychain"
            $global:LASTEXITCODE = 0
        }
        & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $paths
        Should -Invoke node -Times 1 -Exactly -ParameterFilter {
            $Arguments.Count -eq 6 -and $Arguments[0] -eq ".\dashboard\build.mjs" -and
            $Arguments[1] -eq "--preview" -and $Arguments[2] -eq "--snapshot" -and $Arguments[4] -eq "--snapshot"
        }
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains "test-subscription" -and $Arguments -contains "properties.apiKey"
        }
        Should -Invoke npx -Times 1 -Exactly
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
        $env:SWA_CLI_DEBUG | Should -Be "silly"
        (Get-Location).Path | Should -Be $script:InitialLocation
    }
    It "rejects relative, missing, empty and mixed preview inputs before external calls" {
        $path = Join-Path $TestDrive "snapshot.json"
        Set-Content -LiteralPath $path -Value "{}"
        foreach ($inputPaths in @(@("relative.json"), @((Join-Path $TestDrive "missing.json")))) {
            { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $inputPaths } | Should -Throw "*absolute path*"
        }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath @() } | Should -Throw
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $path -IndexUrl "https://metrics.invalid/index.json" } | Should -Throw
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
    }
    It "does not request secrets or deploy when snapshot validation fails" {
        $path = Join-Path $TestDrive "invalid.json"
        Set-Content -LiteralPath $path -Value "{}"
        Mock node { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $path } | Should -Throw "*Dashboard build failed*"
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
    }
    It "rejects failed secret acquisition and never deploys" {
        $path = Join-Path $TestDrive "snapshot.json"
        Set-Content -LiteralPath $path -Value "{}"
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $path } | Should -Throw "*Could not acquire*"
        Should -Invoke npx -Times 0 -Exactly
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
        (Get-Location).Path | Should -Be $script:InitialLocation
    }
}

Describe "Relocated policy-safe infrastructure helper" {
    BeforeEach {
        Mock az {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains "deployment") {
                return '{"properties":{"outputs":{"storageAccount":{"value":"testaccount"},"reportsArePublic":{"value":false}}}}'
            }
            return "{}"
        }
        Mock Write-Warning {}
    }
    It "uses the relocated Bicep and private defaults when provisioning without access grants" {
        $result = & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "test@example.invalid" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -ProvisionResourcesOnly
        $result.storageAccount | Should -Be "testaccount"
        $result.reportsArePublic | Should -BeFalse
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains (Join-Path $script:PackageRoot "infra" "main.bicep") -and
            $Arguments -contains "deployRoleAssignments=false" -and $Arguments -contains "publicReports=false"
        }
        Should -Invoke Write-Warning -Times 2 -Exactly
    }
    It "does not hide resource provisioning failures" {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "test@example.invalid" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -ProvisionResourcesOnly } | Should -Throw "*Azure command failed*"
        Should -Invoke az -Times 1 -Exactly
    }
}

Describe "Nightly revision and SDK guards" {
    BeforeEach {
        $script:Repo = Join-Path $TestDrive "net"
        $null = New-Item -ItemType Directory -Path $script:Repo -Force
        Set-Content -LiteralPath (Join-Path $script:Repo "global.json") -Value '{"sdk":{"version":"10.0.401"}}'
        $script:Commit = "a" * 40
        Mock git {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains "rev-parse") { return ("a" * 40) }
        }
        Mock Write-Host {}
    }
    It "verifies both exact clean commits and emits the checked-out global.json SDK" {
        & (Join-Path $script:PackageRoot "Initialize-NightlyMetrics.ps1") -RepoRoot $script:Repo -ExpectedNetCommit $script:Commit -ExpectedToolsCommit $script:Commit
        Should -Invoke git -Times 2 -Exactly -ParameterFilter { $Arguments -contains "rev-parse" }
        Should -Invoke git -Times 2 -Exactly -ParameterFilter { $Arguments -contains "--untracked-files=no" }
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter { $Object -eq "##vso[task.setvariable variable=MetricsDotNetSdkVersion]10.0.401" }
    }
    It "rejects mismatched tools or language revisions" {
        foreach ($parameter in @("ExpectedToolsCommit", "ExpectedNetCommit")) {
            $arguments = @{ RepoRoot = $script:Repo; ExpectedNetCommit = $script:Commit; ExpectedToolsCommit = $script:Commit }
            $arguments[$parameter] = "b" * 40
            { & (Join-Path $script:PackageRoot "Initialize-NightlyMetrics.ps1") @arguments } | Should -Throw "*resolved revision*"
        }
    }
    It "rejects dirty checkouts and Git failures" {
        Mock git {
            $global:LASTEXITCODE = 0
            if ($Arguments -contains "rev-parse") { return ("a" * 40) }
            return " M tracked-file"
        }
        { & (Join-Path $script:PackageRoot "Initialize-NightlyMetrics.ps1") -RepoRoot $script:Repo -ExpectedNetCommit $script:Commit -ExpectedToolsCommit $script:Commit } | Should -Throw "*clean tracked checkout*"
        Mock git { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Initialize-NightlyMetrics.ps1") -RepoRoot $script:Repo -ExpectedNetCommit $script:Commit -ExpectedToolsCommit $script:Commit } | Should -Throw "*resolved revision*"
    }
    It "rejects a wildcard or missing SDK rather than installing an arbitrary version" {
        foreach ($json in @('{"sdk":{"version":"10.x"}}', '{"sdk":{}}')) {
            Set-Content -LiteralPath (Join-Path $script:Repo "global.json") -Value $json
            { & (Join-Path $script:PackageRoot "Initialize-NightlyMetrics.ps1") -RepoRoot $script:Repo -ExpectedNetCommit $script:Commit -ExpectedToolsCommit $script:Commit } | Should -Throw
        }
    }
}

Describe "Language-aware .NET collection adapter" {
    BeforeEach {
        $script:Repo = Join-Path $TestDrive "net"
        $scripts = Join-Path $script:Repo "eng" "scripts"
        $null = New-Item -ItemType Directory -Path $scripts -Force
        Set-Content -LiteralPath (Join-Path $scripts "Collect-CustomCodeMetrics.ps1") -Value @'
param([string]$RepoRoot, [string]$OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$path = Join-Path $OutputDirectory "snapshot.json"
Set-Content -LiteralPath $path -Value "{}"
return $path
'@
        $script:Output = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        $script:InitialLocation = (Get-Location).Path
        Mock node { $global:LASTEXITCODE = 0 }
        Mock Invoke-Pester {
            (Get-Location).Path | Should -Be (Split-Path (Split-Path (Split-Path (Split-Path $Path))))
            return [PSCustomObject]@{ FailedCount = 0; TotalCount = 65 }
        }
        Mock Write-Host {}
    }
    AfterEach {
        (Get-Location).Path | Should -Be $script:InitialLocation
    }
    It "checks the exact schema mirror, runs language tests and returns the real collector output" {
        $snapshot = & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output
        Test-Path -LiteralPath $snapshot | Should -BeTrue
        Should -Invoke node -Times 1 -Exactly -ParameterFilter {
            $Arguments[0] -eq (Join-Path $script:PackageRoot "schema.mjs") -and $Arguments[1] -eq "check-copy" -and
            $Arguments[2] -eq (Join-Path $script:Repo "eng" "scripts" "CustomCodeMetrics.schema.json")
        }
        Should -Invoke Invoke-Pester -Times 1 -Exactly -ParameterFilter { $Path -eq (Join-Path $script:Repo "eng" "scripts" "tests" "Collect-CustomCodeMetrics.Tests.ps1") }
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter { $Object -eq "##vso[task.setvariable variable=MetricsSnapshotPath]$snapshot" }
    }
    It "does not test or collect when the schema mirror check fails" {
        Mock node { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*schema mirror*"
        Should -Invoke Invoke-Pester -Times 0 -Exactly
    }
    It "does not produce observations when language tests fail" {
        Mock Invoke-Pester { return [PSCustomObject]@{ FailedCount = 1; TotalCount = 65 } }
        { & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*collector tests failed*"
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "does not produce observations when language tests are missing" {
        Mock Invoke-Pester { return [PSCustomObject]@{ FailedCount = 0; TotalCount = 0 } }
        { & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*collector tests failed*"
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "rejects absent collector output rather than publishing an empty success" {
        Set-Content -LiteralPath (Join-Path $script:Repo "eng" "scripts" "Collect-CustomCodeMetrics.ps1") -Value 'param([string]$RepoRoot, [string]$OutputDirectory)'
        { & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*complete snapshot*"
    }
    It "resolves relative output against the caller rather than the .NET checkout" {
        Push-Location $TestDrive
        try {
            $directory = [guid]::NewGuid().ToString()
            $snapshot = & (Join-Path $script:PackageRoot "Collect-DotNetMetrics.ps1") -RepoRoot $script:Repo -OutputDirectory $directory
            $snapshot | Should -Be (Join-Path $TestDrive $directory "snapshot.json")
            (Get-Location).Path | Should -Be $TestDrive
        }
        finally { Pop-Location }
    }
}
