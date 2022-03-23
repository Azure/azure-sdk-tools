BeforeAll {
  . $PSScriptRoot/assets.Tests.Helpers.ps1
  Import-Module -DisableNameChecking -Force $PSScriptRoot/assets.psm1

  # wipe the test runs 
  $testPath = Get-TestFolder
  
  if ((Test-Path $testPath)){
    Remove-Item -Recurse -Force $testPath
  }
}

Describe "AssetsModuleTests" {
  Context "Evaluate-Target-Dir" {
    It "Should evaluate a root directory properly" {
      $Value = Evaluate-Target-Dir -TargetPath (Join-Path $PSScriptRoot ".." ".." "..")
      $Value | Should -Be @($false, $true)
    }
    
    It "Should evaluate a recording directory properly" {
      $Value = Evaluate-Target-Dir -TargetPath $PSScriptRoot
      $Value | Should -Be @($true, $false)
    }
  
    It "Should evaluate an transitory directory properly" {
      $Value = Evaluate-Target-Dir -TargetPath (Join-Path $PSScriptRoot ".." ".." )
      $Value | Should -Be @($false, $false)
    }
  }

  Context "Resolve-RecordingJson" {
    It "Should find basic recording.json" {
      $files = @(
        "a/b.json",
        "a/b/c.json"
      )

      $testLocation = Describe-TestFolder -RecordingJsonContent (Get-Basic-RecordingJson) -Files $files
      
      $Result = Resolve-RecordingJson -TargetPath $testLocation
      $recordingLocation = $Result[0]
      $recordingLocation | Should -Be (Join-Path $testLocation "recording.json")
    }
  }
}