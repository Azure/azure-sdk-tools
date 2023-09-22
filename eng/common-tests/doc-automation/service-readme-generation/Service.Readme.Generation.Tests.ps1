<#
```
Invoke-Pester -Output Detailed $PSScriptRoot/Service-Readme-Generation-Tests.ps1
```
#>

Import-Module Pester
Set-StrictMode -Version Latest

BeforeAll {
    . $PSScriptRoot/../../../common/scripts/Helpers/Service-Level-Readme-Automation-Helpers.ps1
    . $PSScriptRoot/../../../common/scripts/Helpers/Metadata-Helpers.ps1
    . $PSScriptRoot/../../../common/scripts/common.ps1
    . $PSScriptRoot/Service.Readme.Generation.Helpers.ps1

    Mock Get-Date {return "2022-11-01"}

    function Backup-File($targetPath, $backupFolder) {
        if (!(Test-Path $targetPath)) {
            return $null
        }
        $fileName = (Split-Path $targetPath -leaf)
        $backupFile = "$backupFolder/temp-$fileName"
        $null = New-Item $backupFile -ItemType "file" -Force
        $null = Copy-Item $targetPath -Destination $backupFile
        return $backupFile
    }
    function Reset-File($targetPath, $backupFile) {
        if ($backupFile) {
            $null = Copy-Item $backupFile -Destination $targetPath
        }
    }

    if (!(Test-Path "$PSScriptRoot/outputs")) {
        New-Item "$PSScriptRoot/outputs/path-to-readme/preview" -ItemType Directory
        New-Item "$PSScriptRoot/outputs/path-to-readme/latest" -ItemType Directory
    }
}

AfterAll {
    Remove-Item "$PSScriptRoot/backup*" -Recurse
    Remove-Item "$PSScriptRoot/outputs" -Recurse
}

# Test plan:
# 1. Tests on no service readme with single package. Expect to generate one with metadata and one table.
# 2. Tests on no service readme with multiple packages. Expect to generate one with metadata and one table and sorted by type(client, mgmt).
# 3. Tests on existing service readme with real content. Expect to update metadata only.
# 4. Tests on the generated service readme with different mgmt/client packages. Expect no updates.
# 5. Tests on the generated service readme with the same mgmt/client packages. Expect no updates.
Describe "generate-service-level-readme" -Tag "UnitTest" {
    # Passed cases
    It "Generate service readme if readme not exist" -TestCases @(
        @{ packageInfoFile = "$PSScriptRoot/inputs/package1.json"; serviceName = "service name 1"; moniker = "preview"}
        @{ packageInfoFile = "$PSScriptRoot/inputs/package2.json"; serviceName = "service name 2"; moniker = "latest"}
        @{ packageInfoFile = "$PSScriptRoot/inputs/package3.json"; serviceName = "service name 3"; moniker = "preview"}
    ) {
        $packageInfos = Get-Content $packageInfoFile -Raw | ConvertFrom-Json
        $normalizedServiceName = ServiceLevelReadmeNameStyle -serviceName $serviceName
        generate-service-level-readme -docRepoLocation "$PSScriptRoot/outputs/" -readmeBaseName $normalizedServiceName -pathPrefix "path-to-readme" -packageInfos $packageInfos `
            -serviceName $serviceName -moniker $moniker -author "github-alias" -msAuthor "msalias" -msService "ms-service"
        $indexReadme = "$PSScriptRoot/outputs/path-to-readme/$moniker/$normalizedServiceName-index.md"
        $serviceReadme = "$PSScriptRoot/outputs/path-to-readme/$moniker/$normalizedServiceName.md"
        (Get-Content $indexReadme) | Should -Be (Get-Content "$PSScriptRoot/inputs/expected/$moniker/$normalizedServiceName-index.md")
        (Get-Content $serviceReadme) | Should -Be (Get-Content "$PSScriptRoot/inputs/expected/$moniker/$normalizedServiceName.md")
    }
    # Failed cases
    It "No packages passed to generate service readme function" -TestCases @(
        @{ serviceName = "Not exist"; moniker = "preview"}
    ) {
        generate-service-level-readme -docRepoLocation "$PSScriptRoot/outputs/" -readmeBaseName "not-exist" -pathPrefix "path-to-readme" -packageInfos @() `
            -serviceName $serviceName -moniker $moniker -author "github-alias" -msAuthor "msalias" -msService "ms-service" 2>$null
        $indexReadme = "$PSScriptRoot/outputs/path-to-readme/preview/$normalizedServiceName-index.md"
        $serviceReadme = "$PSScriptRoot/outputs/path-to-readme/preview/$normalizedServiceName.md"
        (Test-Path $indexReadme) | Should -BeFalse
        (Test-Path $serviceReadme) | Should -BeFalse
    }

    # Passed cases
    It "Generate service readme when readme exsits" -TestCases @(
        @{ packageInfoFile = "$PSScriptRoot/inputs/package4.json"; serviceName = "service name 4"; moniker = "latest"}
        @{ packageInfoFile = "$PSScriptRoot/inputs/package4.json"; serviceName = "service name 5"; moniker = "preview"}
        @{ packageInfoFile = "$PSScriptRoot/inputs/package4.json"; serviceName = "service name 6"; moniker = "preview"}
    ) {
        $normalizedServiceName = ServiceLevelReadmeNameStyle -serviceName $serviceName
        $expectedIndexReadme = "$PSScriptRoot/inputs/expected/$moniker/$normalizedServiceName-index.md"
        $expectedServiceReadme = "$PSScriptRoot/inputs/expected/$moniker/$normalizedServiceName.md"
        $actualIndexReadme = "$PSScriptRoot/inputs/actual/$moniker/$normalizedServiceName-index.md"
        $actualServiceReadme = "$PSScriptRoot/inputs/actual/$moniker/$normalizedServiceName.md"
        Backup-File $actualIndexReadme "$PSScriptRoot/backup"
        Backup-File $actualServiceReadme "$PSScriptRoot/backup"
        $packageInfos = Get-Content $packageInfoFile -Raw | ConvertFrom-Json
        generate-service-level-readme -docRepoLocation "$PSScriptRoot/inputs/" -readmeBaseName $normalizedServiceName -pathPrefix "actual" -packageInfos $packageInfos `
            -serviceName $serviceName -moniker $moniker -author "github-alias" -msAuthor "msalias" -msService "ms-service"
        (Get-Content $actualIndexReadme) | Should -Be (Get-Content $expectedIndexReadme)
        (Get-Content $actualServiceReadme) | Should -Be (Get-Content $expectedServiceReadme)
        $backupIndexFile = "$PSScriptRoot/backup/temp-$normalizedServiceName-index.md"
        $backupServiceFile = "$PSScriptRoot/backup/temp-$normalizedServiceName.md"
        Reset-File $actualIndexReadme $backupIndexFile
        Reset-File $actualServiceReadme $backupServiceFile
    }

    # Failed cases
    It "No packages passed to service readme function" -TestCases @(
        @{ serviceName = "service name 7"; moniker = "latest"}
    ) {
        $normalizedServiceName = ServiceLevelReadmeNameStyle -serviceName $serviceName
        $serviceReadme = "$PSScriptRoot/inputs/actual/latest/$normalizedServiceName.md"
        $indexReadme = "$PSScriptRoot/inputs/actual/latest/$normalizedServiceName-index.md"
        Backup-File $serviceReadme "$PSScriptRoot/backup"
        $errorThrown = $false
        generate-service-level-readme -docRepoLocation "$PSScriptRoot/inputs" -readmeBaseName "not-exist" -pathPrefix "actual" -packageInfos @() `
            -serviceName $serviceName -moniker $moniker -author "github-alias" -msAuthor "msalias" -msService "ms-service" 2>$null
        
        $expectedServiceName = "$PSScriptRoot/inputs/actual/latest/$normalizedServiceName.md"
        (Test-Path $indexReadme) | Should -BeFalse
        (Get-Content $serviceReadme -Raw) | Should -Be (Get-Content $expectedServiceName -Raw)
        $backupServiceFile = "$PSScriptRoot/backup/temp-$normalizedServiceName.md"
        Reset-File $serviceReadme $backupServiceFile
    }
}
