BeforeAll {
    $script:PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    function az { param([Parameter(ValueFromRemainingArguments)][object[]]$Arguments) throw "Unexpected Azure call." }
    function node {
        [CmdletBinding(PositionalBinding = $false)]
        param([Alias("e")][string]$Code, [Parameter(ValueFromRemainingArguments)][object[]]$Arguments)
        throw "Unexpected Node call."
    }
    function npx { param([Parameter(ValueFromRemainingArguments)][object[]]$Arguments) throw "Unexpected npx call." }
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
    It "previews publication without acquiring a token or launching the publisher" {
        & (Join-Path $script:PackageRoot "Publish-Metrics.ps1") -SnapshotPath $script:Snapshot -StorageAccount "testaccount" -SubscriptionId "test-subscription" -WhatIf
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke node -Times 0 -Exactly
        $env:AZURE_STORAGE_ACCESS_TOKEN | Should -Be "previous-test-token"
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
    It "previews hosted or seeded replacement without builds, feed requests, secrets or deployment" {
        $path = Join-Path $TestDrive "snapshot.json"
        Set-Content -LiteralPath $path -Value "{}"
        & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -SnapshotPath $path -WhatIf
        & (Join-Path $script:PackageRoot "Deploy-Dashboard.ps1") -SubscriptionId "test-subscription" -IndexUrl "https://metrics.invalid/index.json" -WhatIf
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke npx -Times 0 -Exactly
        $env:SWA_CLI_DEPLOYMENT_TOKEN | Should -Be "previous-test-token"
        $env:SWA_CLI_DEBUG | Should -Be "silly"
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
        $result = & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "testalias@microsoft.com" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -ProvisionResourcesOnly
        $result.storageAccount | Should -Be "testaccount"
        $result.reportsArePublic | Should -BeFalse
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains (Join-Path $script:PackageRoot "infra" "main.bicep") -and
            $Arguments -contains "deployRoleAssignments=false" -and $Arguments -contains "publicReports=false"
        }
        Should -Invoke Write-Warning -Times 2 -Exactly
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains "group" -and $Arguments -contains "Owners=testalias@microsoft.com" -and
            $Arguments -contains "Purpose=Azure SDK custom code metrics" -and
            $Arguments -contains "Environment=EngineeringSystem" -and
            -not ($Arguments -contains "Owner=testalias@microsoft.com")
        }
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains "deployment" -and $Arguments -contains "environment=EngineeringSystem" -and
            -not (($Arguments -join " ") -match "storageAccountName=")
        }
    }
    It "does not hide resource provisioning failures" {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "testalias" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -ProvisionResourcesOnly } | Should -Throw "*Azure command failed*"
        Should -Invoke az -Times 1 -Exactly
    }
    It "previews infrastructure and roles without creating a resource group or deploying resources" {
        & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "testalias" -Purpose "Persistent dashboard proposal" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -WhatIf
        Should -Invoke az -Times 0 -Exactly
        Should -Invoke Write-Warning -Times 0 -Exactly
    }
    It "rejects malformed owners and empty purpose without making Azure calls" {
        foreach ($owner in @("team@example.invalid", "first,second", "first;second", "first second", "", "@microsoft.com")) {
            { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner $owner -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw
        }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "testalias" -Purpose " " -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw
        Should -Invoke az -Times 0 -Exactly
    }
    It "passes an explicit tracking purpose to the group and template without cleanup-exemption arguments" {
        & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "test-alias" -Purpose "Metrics dashboard" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001"
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains "group" -and $Arguments -contains "Owners=test-alias" -and $Arguments -contains "Purpose=Metrics dashboard"
        }
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments -contains "deployment" -and $Arguments -contains "owner=test-alias" -and $Arguments -contains "purpose=Metrics dashboard"
        }
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { ($Arguments -join " ") -match "DoNotDelete|DeleteAfter|allowlist" }
        $template = Get-Content -LiteralPath (Join-Path $script:PackageRoot "infra" "main.bicep") -Raw
        $template | Should -Match "Owners: owner"
        $template | Should -Match "Purpose: purpose"
        $template | Should -Not -Match "\bOwner: owner"
    }
    It "deploys only new metrics resources into an existing group without group creation or tag writes" {
        Mock az {
            $global:LASTEXITCODE = 0
            if ($Arguments[0] -eq "group") { return '{"id":"/subscriptions/engineering-subscription/resourceGroups/typespec","location":"westus","tags":null}' }
            if ($Arguments[0] -eq "resource") { return '[{"name":"existing-project-site","type":"Microsoft.Web/staticSites"}]' }
            return '{"properties":{"outputs":{"storageAccount":{"value":"azsdkcustommetrics"},"accessConfigured":{"value":true},"reportsArePublic":{"value":false}}}}'
        }
        $result = & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Location "westus2" -Owner "jolov" -StorageAccountName "azsdkcustommetrics" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001"
        $result.storageAccount | Should -Be "azsdkcustommetrics"
        $result.accessConfigured | Should -BeTrue
        $result.reportsArePublic | Should -BeFalse
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { $Arguments[0] -eq "group" -and $Arguments[1] -ne "show" }
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { $Arguments -contains "--tags" }
        Should -Invoke az -Times 1 -Exactly -ParameterFilter {
            $Arguments[0] -eq "deployment" -and $Arguments -contains "engineering-subscription" -and
            $Arguments -contains "typespec" -and $Arguments -contains "Incremental" -and
            $Arguments -contains "location=westus2" -and $Arguments -contains "environment=EngineeringSystem" -and
            $Arguments -contains "storageAccountName=azsdkcustommetrics" -and
            $Arguments -contains "owner=jolov" -and $Arguments -contains "deployRoleAssignments=true" -and
            $Arguments -contains "publicReports=false"
        }
    }
    It "fails for an absent or mismatched existing group before a deployment or write" {
        Mock az { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Owner "jolov" -StorageAccountName "azsdkcustommetrics" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw "*Azure command failed*"
        Mock az { $global:LASTEXITCODE = 0; return '{"id":"/subscriptions/wrong/resourceGroups/typespec"}' }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Owner "jolov" -StorageAccountName "azsdkcustommetrics" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw "*does not match*"
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { $Arguments[0] -ne "group" -or $Arguments[1] -ne "show" }
    }
    It "refuses collisions with any dedicated resource name without modifying existing resources" {
        foreach ($name in @("azsdk-custom-code-metrics", "azsdkcustommetrics", "id-azsdk-custom-code-metrics")) {
            Mock az {
                $global:LASTEXITCODE = 0
                if ($Arguments[0] -eq "group") { return '{"id":"/subscriptions/engineering-subscription/resourceGroups/typespec"}' }
                return (ConvertTo-Json -InputObject @(@{ name = $name }) -Compress)
            }
            { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Owner "jolov" -StorageAccountName "azsdkcustommetrics" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw "*Refusing to overwrite*"
        }
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { $Arguments[0] -eq "deployment" }
        Should -Invoke az -Times 0 -Exactly -ParameterFilter { $Arguments[0] -eq "group" -and $Arguments[1] -ne "show" }
    }
    It "previews existing-group deployment without discovery, resource writes or metadata changes" {
        & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Owner "jolov" -StorageAccountName "azsdkcustommetrics" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" -WhatIf
        Should -Invoke az -Times 0 -Exactly
    }
    It "rejects invalid friendly storage names or blank environment before any Azure call" {
        foreach ($name in @("ab", "UPPERCASE", "has-hyphen", "name with space", ("a" * 25))) {
            { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "jolov" -StorageAccountName $name -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw
        }
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "test-subscription" -Owner "jolov" -Environment " " -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw
        Should -Invoke az -Times 0 -Exactly
    }
    It "preserves the unique storage default and limits role scopes to the two new private containers" {
        $template = Get-Content -LiteralPath (Join-Path $script:PackageRoot "infra" "main.bicep") -Raw
        $template | Should -Match "param storageAccountName string = 'azsdkcm"
        $template | Should -Match "Environment: environment"
        $template | Should -Match "allowSharedKeyAccess: false"
        $template | Should -Match "param publicReports bool = false"
        [regex]::Matches($template, "scope: (reports|archive)").Count | Should -Be 4
        $template | Should -Not -Match "scope: (resourceGroup|subscription)|DoNotDelete|DeleteAfter"
    }
    It "requires an explicit storage name in existing-group mode to check every requested resource collision" {
        { & (Join-Path $script:PackageRoot "Deploy-Infrastructure.ps1") -SubscriptionId "engineering-subscription" -ResourceGroup "typespec" -UseExistingResourceGroup -Owner "jolov" -BootstrapPrincipalId "00000000-0000-0000-0000-000000000001" } | Should -Throw "*requires an explicit storage account name*"
        Should -Invoke az -Times 0 -Exactly
    }
}

Describe "Shared per-repository measurement command" {
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
        $snapshot = & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output
        Test-Path -LiteralPath $snapshot | Should -BeTrue
        Should -Invoke node -Times 1 -Exactly -ParameterFilter {
            $Arguments[0] -eq (Join-Path $script:PackageRoot "schema.mjs") -and $Arguments[1] -eq "check-copy" -and
            $Arguments[2] -eq (Join-Path $script:Repo "eng" "scripts" "CustomCodeMetrics.schema.json")
        }
        Should -Invoke Invoke-Pester -Times 1 -Exactly -ParameterFilter { $Path -eq (Join-Path $script:Repo "eng" "scripts" "tests" "Collect-CustomCodeMetrics.Tests.ps1") }
        Should -Invoke Write-Host -Times 0 -Exactly
    }
    It "does not test or collect when the schema mirror check fails" {
        Mock node { $global:LASTEXITCODE = 1 }
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*schema mirror*"
        Should -Invoke Invoke-Pester -Times 0 -Exactly
    }
    It "does not produce observations when language tests fail" {
        Mock Invoke-Pester { return [PSCustomObject]@{ FailedCount = 1; TotalCount = 65 } }
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*collector tests failed*"
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "does not produce observations when language tests are missing" {
        Mock Invoke-Pester { return [PSCustomObject]@{ FailedCount = 0; TotalCount = 0 } }
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*collector tests failed*"
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "rejects absent collector output rather than publishing an empty success" {
        Set-Content -LiteralPath (Join-Path $script:Repo "eng" "scripts" "Collect-CustomCodeMetrics.ps1") -Value 'param([string]$RepoRoot, [string]$OutputDirectory)'
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*complete snapshot*"
    }
    It "resolves relative output against the caller rather than the .NET checkout" {
        Push-Location $TestDrive
        try {
            $directory = [guid]::NewGuid().ToString()
            $snapshot = & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $directory
            $snapshot | Should -Be (Join-Path $TestDrive $directory "snapshot.json")
            (Get-Location).Path | Should -Be $TestDrive
        }
        finally { Pop-Location }
    }
    It "supports explicit case-insensitive dotnet with the same adapter as the default" {
        $snapshot = & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -Language DOTNET -RepoRoot $script:Repo -OutputDirectory $script:Output
        Test-Path -LiteralPath $snapshot -PathType Leaf | Should -BeTrue
        Should -Invoke node -Times 1 -Exactly
        Should -Invoke Invoke-Pester -Times 1 -Exactly
    }
    It "rejects every unimplemented language before checking a checkout or producing data" {
        foreach ($language in @("java", "js", "python", "go", "rust", "cpp")) {
            { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -Language $language -RepoRoot (Join-Path $TestDrive "absent") -OutputDirectory $script:Output } | Should -Throw "*'$language' is not implemented*"
        }
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke Invoke-Pester -Times 0 -Exactly
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "rejects unknown or empty language names without invoking an adapter" {
        foreach ($language in @("unknown", "all", "", "net")) {
            { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -Language $language -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw
        }
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke Invoke-Pester -Times 0 -Exactly
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "propagates a collector exception and restores the caller location" {
        Set-Content -LiteralPath (Join-Path $script:Repo "eng" "scripts" "Collect-CustomCodeMetrics.ps1") -Value 'param([string]$RepoRoot, [string]$OutputDirectory); throw "Collector evaluation failed."'
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*Collector evaluation failed*"
        Test-Path -LiteralPath $script:Output | Should -BeFalse
    }
    It "rejects multiple or nonexistent output paths rather than returning a success-shaped value" {
        $collector = Join-Path $script:Repo "eng" "scripts" "Collect-CustomCodeMetrics.ps1"
        Set-Content -LiteralPath $collector -Value 'param([string]$RepoRoot, [string]$OutputDirectory); return @("one.json", "two.json")'
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*exactly one complete snapshot*"
        Set-Content -LiteralPath $collector -Value 'param([string]$RepoRoot, [string]$OutputDirectory); return (Join-Path $OutputDirectory "missing.json")'
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot $script:Repo -OutputDirectory $script:Output } | Should -Throw "*exactly one complete snapshot*"
    }
    It "rejects a missing checkout without executing validation or collection" {
        { & (Join-Path $script:PackageRoot "Collect-Metrics.ps1") -RepoRoot (Join-Path $TestDrive "absent") -OutputDirectory $script:Output } | Should -Throw
        Should -Invoke node -Times 0 -Exactly
        Should -Invoke Invoke-Pester -Times 0 -Exactly
    }
}
