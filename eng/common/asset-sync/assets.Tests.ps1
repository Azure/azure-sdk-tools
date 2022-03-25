BeforeAll {
  . $PSScriptRoot/assets.Tests.Helpers.ps1
  . $PSScriptRoot/assets.ps1

  # wipe the test runs 
  $testPath = Get-TestFolder
  
  if ((Test-Path $testPath)){
    Remove-Item -Recurse -Force $testPath
  }
}

Describe "AssetsModuleTests" {
  Context "Evaluate-Target-Dir" {
    It "Should evaluate a root directory properly." {
      $Value = Evaluate-Target-Dir -TargetPath (Join-Path $PSScriptRoot ".." ".." "..")
      $Value | Should -Be @($false, $true)
    }
    
    It "Should evaluate a recording directory properly." {
      $Value = Evaluate-Target-Dir -TargetPath $PSScriptRoot
      $Value | Should -Be @($true, $false)
    }
  
    It "Should evaluate an transitory directory properly." {
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
      
      $testLocation.GetType().Name | Should -Be "String"

      $Result = Resolve-RecordingJson -TargetPath $testLocation
      $recordingLocation = $Result.RecordingJsonLocation
      $recordingLocation | Should -Be (Join-Path $testLocation "recording.json")
    }

    It "Should should traverse upwards to find recording.json." {
      $files = @(
        "a/b.json",
        "a/b/c.json"
      )

      $testLocation = Describe-TestFolder -RecordingJsonContent (Get-Basic-RecordingJson) -Files $files
      
      try {
        Push-Location -Path (Join-Path $testLocation "a" "b")
        $Result = Resolve-RecordingJson -TargetPath $testLocation
        $recordingLocation = $Result.RecordingJsonLocation
        $recordingLocation | Should -Be (Join-Path $testLocation "recording.json")
      }
      finally {
        Pop-Location
      }
    }

    It "Should should error when unable to find a recording json." {
      $files = @(
        "a/b.json",
        "a/b/c.json"
      )

      $testLocation = Describe-TestFolder -RecordingJsonContent "" -Files $files
      
      try {
        Push-Location -Path (Join-Path $testLocation "a" "b")
        { Resolve-RecordingJson -TargetPath $testLocation } | Should -Throw
      }
      finally {
        Pop-Location
      }
    }

    It "Should should not attempt to calculate relative path where not possible." {

      $testLocation = Describe-TestFolder -RecordingJsonContent (Get-Basic-RecordingJson) -Files $files
      $Result = Resolve-RecordingJson -TargetPath $testLocation
      $expectedValue = ""

      $recordingLocation = $Result.RecordingJsonRelativeLocation
      $recordingLocation | Should -Be $expectedValue
    }

    It "Should should calculate relative path from root of repo to the target recording.json." {
      $Result = Resolve-RecordingJson -TargetPath $PSScriptRoot
      $expectedValue = (Join-Path "./" "eng" "common" "asset-sync" "recording.json")

      $recordingLocation = $Result.RecordingJsonRelativeLocation
      $recordingLocation | Should -Be $expectedValue
    }
  }
  
  Context "Resolve-Assets" {
    It "Should resolve the asset store location." {
      $files = @()
      $jsonContent = Get-Basic-RecordingJson
      Describe-TestFolder -RecordingJsonContent $jsonContent -Files $files

      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".." ".assets")
      $result = Resolve-AssetStore-Location

      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a standard assets repo." {
      $files = @()
      $jsonContent = Get-Basic-RecordingJson -RandomizeRepoId $false
      Describe-TestFolder -RecordingJsonContent $jsonContent -Files $files
      $result = Resolve-AssetRepo-Location -Config $jsonContent
      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".." ".assets" "Azure.azure-sdk-for-python-assets")

      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a custom repoId." {
      $files = @()
      $jsonContent = Get-Basic-RecordingJson
      $jsonContent.AssetsRepoId = "custom"

      Describe-TestFolder -RecordingJsonContent $jsonContent -Files $files
      $result = Resolve-AssetRepo-Location -Config $jsonContent
      $expectedLocation = Resolve-Path (Join-Path $PSScriptRoot ".." ".." ".." ".assets" "custom")
      
      $result | Should -Be $expectedLocation.ToString()
    }
  }

  Context "Initialize-Assets-Repo" {
    It "Should initialize an empty directory." {
      $JsonContent = Get-Basic-RecordingJson
      $testLocation = Describe-TestFolder -RecordingJsonContent $JsonContent -Files $files
    }

    It "Should recognize an initialized repository." {
      
    }
  }


}