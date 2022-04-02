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

  Context "Resolve-AssetsJson" {
    It "Should find basic assets.json" {
      $files = @(
        "a/b.json",
        "a/b/c.json"
      )

      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      
      $testLocation.GetType().Name | Should -Be "String"

      $Config = Resolve-AssetsJson -TargetPath $testLocation
      $recordingLocation = $Config.AssetsJsonLocation
      $recordingLocation | Should -Be (Join-Path $testLocation "assets.json")
    }

    It "Should should traverse upwards to find assets.json." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      
      $Result = Resolve-AssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage" "azure-storage-blob")
      $recordingLocation = $Result.AssetsJsonLocation
      $recordingLocation | Should -Be (Join-Path $testLocation "sdk" "storage" "assets.json")
    }

    It "Should be able to resolve the assets json based on CWD" {
      # $files = @(
      #   "sdk/storage/",
      #   "sdk/storage/assets.json",
      #   "sdk/storage/azure-storage-blob/awesome.json"
      # )
      # $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      
      # try {
      #   Push-Location -Path (Join-Path $testLocation "sdk" "storage" "azure-storage-blob")
      #   $Result = Resolve-AssetsJson
      #   $recordingLocation = $Result.AssetsJsonLocation
      #   $recordingLocation | Should -Be (Join-Path $testLocation "sdk" "storage" "assets.json")
      # }
      # finally {
      #   Pop-Location
      # }
    }


    It "Should should error when unable to find a recording json." {
      $files = @(
        "a/b.json",
        "a/b/c.json"
      )

      $testLocation = Describe-TestFolder -AssetsJsonContent "" -Files $files
      
      try {
        Push-Location -Path (Join-Path $testLocation "a" "b")
        { Resolve-AssetsJson -TargetPath $testLocation } | Should -Throw
      }
      finally {
        Pop-Location
      }
    }

    It "Should should calculate relative path from root of repo to the target assets.json." {
      $Result = Resolve-AssetsJson -TargetPath $PSScriptRoot
      $expectedValue = (Join-Path "./" "eng" "common" "asset-sync" "assets.json")

      $recordingLocation = $Result.AssetsJsonRelativeLocation
      $recordingLocation | Should -Be $expectedValue
    }
  }
  
  Context "Resolve-Assets" {
    It "Should resolve the asset store location." {
      $files = @()
      $jsonContent = Get-Basic-AssetsJson
      Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files

      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".." ".assets")
      $result = Resolve-AssetStore-Location

      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a standard assets repo." {
      $files = @()
      $jsonContent = Get-Basic-AssetsJson -RandomizeRepoId $false
      Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files
      $result = Resolve-AssetRepo-Location -Config $jsonContent
      $expectedHash = "D41D8CD98F00B204E9800998ECF8427E"
      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".." ".assets" "$expectedHash")

      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a custom repoId." {
      $jsonContent = Get-Basic-AssetsJson
      $jsonContent.AssetsRepoId = "custom"

      Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files
      $result = Resolve-AssetRepo-Location -Config $jsonContent
      $expectedHash = "D41D8CD98F00B204E9800998ECF8427E"

      $expectedLocation = Resolve-Path (Join-Path $PSScriptRoot ".." ".." ".." ".assets" "$expectedHash")
      
      $result | Should -Be $expectedLocation.ToString()
    }
  }

  Context "Assets Json Updates" {
    It "Should update a targeted recording.json w/ a new SHA and output without mangling the json file." {
      
    }
  }

  Context "Initialize-Assets-Repo" {
    It "Should create assets repo for standard sync." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files

      $config = Resolve-AssetsJson -TargetPath $testLocation

      Initialize-AssetsRepo -Config $config
    }

    It "Should recognize an initialized repository and no-op." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $JsonContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $JsonContent -Files $files
    }

    It "Should initialize language repo with a new assets.json at sdk/<service> if necessary" {
      # TODO
    }
  }


}