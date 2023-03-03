Import-Module Pester

BeforeAll {
  . $PSScriptRoot/../../common/scripts/get-codeowners/get-codeowners-functions.ps1

    $ToolVersion = "1.0.0-dev.20230214.3"
    $ToolPath = (Join-Path ([System.IO.Path]::GetTempPath()) "codeowners-tool")
    $DevOpsFeed = "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json"
    $VsoVariable = ""

    function TestGetCodeowners([string]$targetPath, [string]$codeownersFileLocation, [bool]$includeNonUserAliases = $false, [string[]]$expectedOwners) {
      Write-Host "Test: Owners for path '$targetPath' in CODEOWNERS file at path '$codeownersFileLocation' should be '$expectedOwners'"
      
      $actualOwners = Get-Codeowners `
        -ToolVersion $ToolVersion `
        -ToolPath $ToolPath `
        -DevOpsFeed $DevOpsFeed `
        -VsoVariable $VsoVariable `
        -targetPath $targetPath `
        -codeownersFileLocation $codeownersFileLocation `
        -includeNonUserAliases $IncludeNonUserAliases
    
      $actualOwners.Count | Should -Be $expectedOwners.Count
      for ($i = 0; $i -lt $expectedOwners.Count; $i++) {
        $expectedOwners[$i] | Should -Be $actualOwners[$i]
      }
    }
}

Describe "Get Codeowners" -Tag "UnitTest" {
    It "Should get Codeowners" -TestCases @(
      @{
        codeownersPath = "$PSScriptRoot/../../../.github/CODEOWNERS"; 
        targetPath = "eng/common/scripts/get-codeowners/get-codeowners.ps1"; 
        expectedOwners = @("konrad-jamrozik", "weshaggard", "benbp")
      },
      @{
        codeownersPath = "$PSScriptRoot/../../../tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/test_CODEOWNERS"; 
        targetPath = "tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/InputDir/a.txt"; 
        expectedOwners = @("2star")
      }      
    ) {
      $resolvedCodeownersPath = (Resolve-Path $codeownersPath)
      TestGetCodeowners `
        -targetPath $targetPath `
        -codeownersFileLocation $resolvedCodeownersPath `
        -includeNonUserAliases $true `
        -expectedOwners $expectedOwners
    }
}