BeforeAll {
  . $PSScriptRoot/assets.Tests.Helpers.ps1
  . $PSScriptRoot/assets.ps1

  # wipe the test runs 
  $testPath = Get-TestFolder
  
  if ((Test-Path $testPath)){
    Remove-Item -Recurse -Force $testPath
  }

  # wipe the assets repo
  $location = Resolve-AssetStore-Location
  if ((Test-Path $location)){
    Remove-Item -Recurse -Force $location
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

    It "Should recognize root in a test directory."{
      $files = @(
        "sdk/assets.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files

      $result = Evaluate-Target-Dir -TargetPath $testLocation
      $result | Should -Be @($false, $true)

      $result = Evaluate-Target-Dir -TargetPath (Join-Path $testLocation "sdk")
      $result | Should -Be @($true, $false)
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
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      
      try {
        Push-Location -Path (Join-Path $testLocation "sdk" "storage" "azure-storage-blob")
        $Result = Resolve-AssetsJson
        $recordingLocation = $Result.AssetsJsonLocation
        $recordingLocation | Should -Be (Join-Path $testLocation "sdk" "storage" "assets.json")
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
      $files = @(
        "pylintrc"
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files

      $Result = Resolve-AssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage" )
      $expectedValue = (Join-Path "./" "sdk" "storage" "assets.json")

      $recordingLocation = $Result.AssetsJsonRelativeLocation
      $recordingLocation | Should -Be $expectedValue
    }
  }
  
  Context "Resolve-Assets" {
    It "Should resolve the asset store location." {
      $files = @()
      $jsonContent = Get-Basic-AssetsJson
      Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files

      $result = Resolve-AssetStore-Location
      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".." ".assets")
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

  Context "Update-AssetsJson" {
    It "Should update a targeted recording.json w/ a new SHA and output without mangling the json file." {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @()
      $config = Resolve-AssetsJson -TargetPath $testLocation

      $config.AssetsJsonLocation
    }

    It "Should no-op if no change." {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @()
      $config = Resolve-AssetsJson -TargetPath $testLocation

      $config.AssetsJsonLocation
    }
  }

  Context "Resolve-CheckoutPaths" {
    It "Should correctly resolve a non-root checkout path format" {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $assetsContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsContent -Files $files

      $checkoutPaths = Resolve-CheckoutPaths -Config $assetsContent

      
    }

    It "Should correctly resolve a root checkout path" {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $assetsContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsContent -Files $files
    }
  }

  Context "Initialize-AssetsRepo" {
    It "Should create assets repo for standard sync." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $assetsContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsContent -Files $files

      $config = Resolve-AssetsJson -TargetPath $testLocation

      Write-Host $config

      Initialize-AssetsRepo -Config $config
      $assetLocation = Resolve-AssetRepo-Location -Config $config
      
      

    }

    It "Should recognize an initialized repository and no-op." {
      $files = @()
      $JsonContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $JsonContent -Files $files
      $config = Resolve-AssetsJson -TargetPath $testLocation

      Initialize-AssetsRepo -Config $config

      $parsedResult = Is-AssetsRepo-Initialized -Config $config
      $parsedResult | Should -Be $true
    }

    It "Should initialize language repo with a new assets.json at sdk/<service> if necessary" {
      # TODO
    }
  }

  Context "Push-AssetsRepo-Update" {
    It "Should push a new branch/commit to a non-existent target branch." {
      $recordingJson = [PSCustomObject]@{
        AssetsRepo = "Azure/azure-sdk-assets-integration"
        AssetsRepoPrefixPath = "python/recordings/"
        AssetsRepoId = ""
        AssetsRepoBranch = "scenario_new_push"
        SHA = "786b4f3d380d9c36c91f5f146ce4a7661ffee3b9"
      }

      $files = @(
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_timeout.json",
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_server_error.json",
        "sdk/tables/azure-data-tables/assets.json"
      )

      # prepare the test area
      $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IntegrationBranch $recordingJsonContent.AssetsRepoBranch
      $config = Resolve-AssetsJson (Join-Path $testFolder "sdk" "tables" "azure-data-tables")

      # initialize the assets repo and copy changes into it
      Initialize-AssetsRepo -Config $config
      $assetRepoFolder = Resolve-AssetRepo-Location -Config $config
      
      foreach($file in $files){
        $sourcePath = Join-Path $testFolder $file 
        $targetPath = Join-Path $assetRepoFolder $config.AssetsRepoPrefixPath $file

        Copy-Item -Path $sourcePath -Destination $targetPath
      }
      
      Push-AssetsRepo-Update -Config $Config
    }
    
    It "Should push a clean new commmit to the target branch" {

    }

    It "Should push a new commit on top of an existing one." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $assetsContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsContent -Files $files

      $config = Resolve-AssetsJson -TargetPath $testLocation

      Initialize-AssetsRepo -Config $config
    }
  }
}