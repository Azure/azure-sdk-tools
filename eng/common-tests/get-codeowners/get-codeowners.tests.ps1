Import-Module Pester

BeforeAll {
  . $PSScriptRoot/../../common/scripts/get-codeowners.lib.ps1

    function TestGetCodeowners(
      [string] $TargetPath, 
      [string] $CodeownersFileLocation, 
      [string[]] $ExpectedOwners
    ) 
    {
      Write-Host "Test: Owners for path '$TargetPath' in CODEOWNERS file at path '$CodeownersFileLocation' should be '$ExpectedOwners'"
      
      $actualOwners = Get-Codeowners `
        -TargetPath $TargetPath `
        -CodeownersFileLocation $CodeownersFileLocation `
    
      $actualOwners.Count | Should -Be $ExpectedOwners.Count
      for ($i = 0; $i -lt $ExpectedOwners.Count; $i++) {
        $ExpectedOwners[$i] | Should -Be $actualOwners[$i]
      }
    }
}

Describe "Get Codeowners" -Tag "UnitTest" {
    It "Should get Codeowners" -TestCases @(
      @{
        # The $PSScriptRoot is assumed to be azure-sdk-tools/eng/common-tests/get-codeowners/get-codeowners.tests.ps1
        codeownersPath = "$PSScriptRoot/../../../.github/CODEOWNERS"; 
        targetPath = "eng/common/scripts/get-codeowners/get-codeowners.ps1"; 
        expectedOwners = @("konrad-jamrozik", "weshaggard", "benbp")
      },
      @{
        # The $PSScriptRoot is assumed to be azure-sdk-tools/eng/common-tests/get-codeowners/get-codeowners.tests.ps1
        CodeownersPath = "$PSScriptRoot/../../../tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/test_CODEOWNERS"; 
        targetPath = "tools/code-owners-parser/Azure.Sdk.Tools.RetrieveCodeOwners.Tests/TestData/InputDir/a.txt"; 
        expectedOwners = @("2star")
      }      
    ) {
      $resolvedCodeownersPath = (Resolve-Path $codeownersPath)
      TestGetCodeowners `
        -TargetPath $targetPath `
        -CodeownersFileLocation $resolvedCodeownersPath `
        -ExpectedOwners $expectedOwners
    }
}