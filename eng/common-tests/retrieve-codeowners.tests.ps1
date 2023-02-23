Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/get-codeowners-functions.ps1

    $ToolVersion = "1.0.0-dev.20230214.3"
    $ToolPath = (Join-Path ([System.IO.Path]::GetTempPath()) "codeowners-tool-path")
    $DevOpsFeed = "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json"
    $VsoVariable = ""

    function TestGetCodeowners2([string]$targetPath, [string]$codeownersFileLocation, [bool]$includeNonUserAliases = $false, [string[]]$expectReturn) {
      Write-Host "Test: find owners matching '$targetPath' ..."
      
      $actualReturn = Get-Codeowners `
        -ToolVersion $ToolVersion `
        -ToolPath $ToolPath `
        -DevOpsFeed $DevOpsFeed `
        -VsoVariable $VsoVariable `
        -targetPath $targetPath `
        -codeownersFileLocation $codeownersFileLocation `
        -includeNonUserAliases $IncludeNonUserAliases
    
      if ($actualReturn.Count -ne $expectReturn.Count) {
        Write-Error "The length of actual result is not as expected. Expected length: $($expectReturn.Count), Actual length: $($actualReturn.Count)."
        exit 1
      }
      for ($i = 0; $i -lt $expectReturn.Count; $i++) {
        if ($expectReturn[$i] -ne $actualReturn[$i]) {
          Write-Error "Expect result $expectReturn[$i] is different than actual result $actualReturn[$i]."
          exit 1
        }
      }
    }
}

Describe "Retrieve Codeowners" -Tag "UnitTest" {
    It "Should retrieve Codeowners" -TestCases @(
      @{ 
        $codeownersPath = "$PSScriptRoot/../../.github/CODEOWNERS"; 
        $targetPath = "eng/common/scripts/get-codeowners.ps1"; 
        $expectedOwners = @("konrad-jamrozik", "weshaggard", "benbp")
      }
    ) {
      $azSdkToolsCodeowners = (Resolve-Path "$PSScriptRoot/../../.github/CODEOWNERS")
      TestGetCodeowners2 -targetPath "eng/common/scripts/get-codeowners.ps1" -codeownersFileLocation $azSdkToolsCodeowners -includeNonUserAliases $true -expectReturn @("konrad-jamrozik", "weshaggard", "benbp")
      
      $LASTEXITCODE | Should -Be 0

      $testCodeowners = (Resolve-Path "$PSScriptRoot/../../tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/glob_path_CODEOWNERS")
      TestGetCodeowners2 -targetPath "tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/InputDir/a.txt" -codeownersFileLocation $testCodeowners -includeNonUserAliases $true -expectReturn @("2star")

      $LASTEXITCODE | Should -Be 0
    }
}

# Describe "Retrieve Codeowners" -Tag "UnitTest" {
#   It "Should retrieve Codeowners" -TestCases @(@{}
#   ) {
#     $azSdkToolsCodeowners = (Resolve-Path "$PSScriptRoot/../../.github/CODEOWNERS")
#     TestGetCodeowners2 -targetPath "eng/common/scripts/get-codeowners.ps1" -codeownersFileLocation $azSdkToolsCodeowners -includeNonUserAliases $true -expectReturn @("konrad-jamrozik", "weshaggard", "benbp")
    
#     $LASTEXITCODE | Should -Be 0

#     $testCodeowners = (Resolve-Path "$PSScriptRoot/../../tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/glob_path_CODEOWNERS")
#     TestGetCodeowners2 -targetPath "tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/InputDir/a.txt" -codeownersFileLocation $testCodeowners -includeNonUserAliases $true -expectReturn @("2star")

#     $LASTEXITCODE | Should -Be 0
#   }
# }