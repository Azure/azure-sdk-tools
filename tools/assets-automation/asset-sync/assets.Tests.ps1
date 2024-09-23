BeforeAll {
  . $PSScriptRoot/assets.Tests.Helpers.ps1
  . $PSScriptRoot/assets.ps1

  # clean up the temporary test repo structures
  $testPath = Get-TestFolder
  if (Test-Path $testPath){
    Remove-Item -Recurse -Force $testPath
  }

  # clean up local .assets folder
  $location = ResolveAssetStoreLocation
  if (Test-Path $location){
    Remove-Item -Recurse -Force $location
  }

  Set-StrictMode -Version 3
}

AfterAll {
  if(!($env:DISABLE_INTEGRATION_BRANCH_CLEANUP)){
    DeInitialize-Integration-Branches
  }
}

Describe "AssetsModuleTests" {
  Context "EvaluateDirectory" -Tag "Unit" {
    It "Should evaluate a root directory properly." {
      EvaluateDirectory -TargetPath (Join-Path $PSScriptRoot ".." "..") | Should -Be @($false, $true)
    }
    
    It "Should evaluate a recording directory properly." {
      EvaluateDirectory -TargetPath $PSScriptRoot | Should -Be @($true, $false)
    }
  
    It "Should evaluate an transitory directory properly." {
      EvaluateDirectory -TargetPath (Join-Path $PSScriptRoot "..") | Should -Be @($false, $false)
    }

    It "Should recognize root in a test directory."{
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @("sdk/assets.json")
      EvaluateDirectory -TargetPath $testLocation | Should -Be @($false, $true)
      EvaluateDirectory -TargetPath (Join-Path $testLocation "sdk") | Should -Be @($true, $false)
    }
  }

  Context "ResolveAssetsJson" -Tag "Unit" {
    It "Should find basic assets.json" {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @("a/b.json", "a/b/c.json")
      
      $testLocation.GetType().Name | Should -Be "String"
      (ResolveAssetsJson -TargetPath $testLocation).AssetsJsonLocation | Should -Be (Join-Path $testLocation "assets.json")
    }

    It "Should should traverse upwards to find assets.json." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      
      $Result = (ResolveAssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage" "azure-storage-blob")).AssetsJsonLocation
      $Result | Should -Be (Join-Path $testLocation "sdk" "storage" "assets.json")
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
        $jsonLocation = (ResolveAssetsJson).AssetsJsonLocation
        $jsonLocation | Should -Be (Join-Path $testLocation "sdk" "storage" "assets.json")
      }
      finally {
        Pop-Location
      }
    }

    It "Should should error when unable to find a recording json." {
      $testLocation = Describe-TestFolder -AssetsJsonContent "" -Files @("a/b.json", "a/b/c.json")
      
      try {
        Push-Location -Path (Join-Path $testLocation "a" "b")
        { ResolveAssetsJson -TargetPath $testLocation } | Should -Throw
      }
      finally {
        Pop-Location
      }
    }

    It "Should calculate relative path from root of repo to the target assets.json." {
      $files = @(
        "pylintrc"
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files

      $actualLocation = (ResolveAssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage" )).AssetsJsonRelativeLocation
      $expectedLocation = (Join-Path "sdk" "storage" "assets.json")

      $actualLocation | Should -Be $expectedLocation
    }

    It "Should resolve shortcut paths like '.'" {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files

      try {
        Push-Location -Path (Join-Path $testLocation "sdk" "storage" "azure-storage-blob")
        $actualLocation = (ResolveAssetsJson -TargetPath ".").AssetsJsonLocation
        $expectedLocation = (Join-Path $testLocation "sdk" "storage" "assets.json")
        $actualLocation | Should -Be $expectedLocation
      }
      finally {
        Pop-Location
      }
    }

    It "Should throw on missing properties." {
      $assetsJson = Get-Basic-AssetsJson | Select-Object -ExcludeProperty "SHA"
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsJson -Files @()
      { ResolveAssetsJson -TargetPath $testLocation } | Should -Throw
    }
  }
  
  Context "Resolve-Assets" -Tag "Unit" {
    It "Should resolve the asset store location." {
      $files = @()
      $jsonContent = Get-Basic-AssetsJson
      Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files

      $result = ResolveAssetStoreLocation
      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".assets")
      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a standard assets repo." {
      $files = @()
      $jsonContent = Get-Basic-AssetsJson -RandomizeRepoId $false
      $testPath = Describe-TestFolder -AssetsJsonContent $jsonContent -Files $files
      $config = ResolveAssetsJson -TargetPath $testPath
      $result = ResolveAssetRepoLocation -Config $config
      $expectedHash = "D41D8CD98F00B204E9800998ECF8427E"
      $expectedLocation = Resolve-Path(Join-Path $PSScriptRoot ".." ".." ".assets" "$expectedHash")

      $result | Should -Be $expectedLocation.ToString()
    }

    It "Should should resolve a custom repoId." {
      $jsonContent = Get-Basic-AssetsJson
      $jsonContent.AssetsRepoId = "custom"

      $testPath = Describe-TestFolder -AssetsJsonContent $jsonContent -Files @()
      $config = ResolveAssetsJson -TargetPath $testPath
      $result = ResolveAssetRepoLocation -Config $config
      $expectedHash = "D41D8CD98F00B204E9800998ECF8427E"

      $expectedLocation = Resolve-Path (Join-Path $PSScriptRoot ".." ".." ".assets" "$expectedHash")
      
      $result | Should -Be $expectedLocation.ToString()
    }
  }

  Context "UpdateAssetsJson" -Tag "Unit" {
    It "Should update a targeted recording.json w/ a new SHA and output without mangling the json file." {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @()
      $config = ResolveAssetsJson -TargetPath $testLocation

      $config.AssetsJsonLocation
    }

    It "Should no-op if no change." {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files @()
      $config = ResolveAssetsJson -TargetPath $testLocation

      $config.AssetsJsonLocation
    }
  }

  Context "ResolveCheckoutPaths" -Tag "Unit" {
    It "Should correctly resolve a non-root checkout path format." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/assets.json",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      $config = ResolveAssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage")

      ResolveCheckoutPaths -Config $config | Should -Be (Join-Path "recordings" "sdk" "storage").Replace("`\", "/")
    }

    It "Should correctly resolve a root checkout path." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $assetsContent = Get-Basic-AssetsJson
      $testLocation = Describe-TestFolder -AssetsJsonContent $assetsContent -Files $files
      $config = ResolveAssetsJson -TargetPath (Join-Path $testLocation "sdk" "storage")

      ResolveCheckoutPaths -Config $config | Should -Be "recordings/"
    }
  }

  Context "InitializeAssetsRepo" -Tag "Unit" {
    It "Should create assets repo for standard sync." {
      $files = @(
        "sdk/storage/",
        "sdk/storage/azure-storage-blob/awesome.json"
      )
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson) -Files $files
      $config = ResolveAssetsJson -TargetPath $testLocation

      InitializeAssetsRepo -Config $config
      IsAssetsRepoInitialized -Config $config | Should -Be "$true"
    }

    It "Should recognize an initialized repository and no-op." {
      $testLocation = Describe-TestFolder -AssetsJsonContent (Get-Basic-AssetsJson)
      $config = ResolveAssetsJson -TargetPath $testLocation

      InitializeAssetsRepo -Config $config
      IsAssetsRepoInitialized -Config $config | Should -Be $true
    }

    It "Should initialize language repo with a new assets.json at sdk/ if necessary" {
      # TODO, do we even need this? no, right?
    }
  }

  # each of the tests in this context is resolving one of the known cases when pushing changes to an auto commit branch
  # In order:
  #    - Auto Branch Doesn't Exist Yet, Go Off Main, push to new branch
  #    - Auto Branch Exists, we're on the latest commit, push new commit to branch
  #    - Auto Branch Exists, we're on a commit from the past, push new commit to branch
  Context "PushAssetsRepoUpdate" -Tag "Integration" {
    It "Should push a new branch/commit to a non-existent target branch." {
      $sourceBranch = "scenario_new_push"
      $recordingJson = [PSCustomObject]@{
        AssetsRepo = "Azure/azure-sdk-assets-integration"
        AssetsRepoPrefixPath = "python/recordings/"
        AssetsRepoId = ""
        AssetsRepoBranch = "$sourceBranch"
        SHA = "786b4f3d380d9c36c91f5f146ce4a7661ffee3b9"
      }

      $files = @(
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_timeout.json",
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_server_error.json",
        "sdk/tables/azure-data-tables/assets.json"
      )

      $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IntegrationBranch $recordingJson.AssetsRepoBranch
      $config = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables" "azure-data-tables")

      InitializeAssetsRepo -Config $config
      $assetRepoFolder = ResolveAssetRepoLocation -Config $config
      
      foreach($file in $files){
        $sourcePath = Join-Path $testFolder $file 
        $targetPath = Join-Path $assetRepoFolder $config.AssetsRepoPrefixPath $file

        $targetFolder = Split-Path $targetPath
        if(-not (Test-Path $targetFolder)){
          New-Item -Type Directory -Force -Path $targetFolder | Out-Null
        }

        Copy-Item  -Force -Path $sourcePath -Destination $targetPath
      }
      $config.AssetsRepoBranch | Should -not -Be $sourceBranch

      PushAssetsRepoUpdate -Config $Config

      try {
        Push-Location $assetRepoFolder
        $repoBranchSHA = git rev-parse origin/$($config.AssetsRepoBranch) --quiet 2>$null
      }
      finally {
        Pop-Location
      }

      # re-parse from the on-disk json
      $configReparsed = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables" "azure-data-tables")

      # re-parsed config from disk should
      #  -> match the updated sha
      #  -> match the SHA we get back from the repo
      $repoBranchSHA | Should -Be $config.SHA
      $configReparsed.SHA | Should -Be $config.SHA
    }
    
    It "Should push a clean new commmit to the target branch." {
      $sourceBranch = "scenario_clean_push"
      $recordingJson = [PSCustomObject]@{
        AssetsRepo = "Azure/azure-sdk-assets-integration"
        AssetsRepoPrefixPath = "python/recordings/"
        AssetsRepoId = ""
        AssetsRepoBranch = "$sourceBranch"
        SHA = "e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7"
      }

      $files = @(
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_timeout.json",
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_server_error.json",
        "sdk/tables/assets.json"
      )

      $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IntegrationBranch $recordingJson.AssetsRepoBranch
      $config = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables")
      $config.AssetsRepoBranch | Should -not -Be $sourceBranch
      
      InitializeAssetsRepo -Config $config
      $assetRepoFolder = ResolveAssetRepoLocation -Config $config

      foreach($file in $files){
        $sourcePath = Join-Path $testFolder $file 
        $targetPath = Join-Path $assetRepoFolder $config.AssetsRepoPrefixPath $file

        $targetFolder = Split-Path $targetPath
        if(-not (Test-Path $targetFolder)){
          New-Item -Type Directory -Force -Path $targetFolder | Out-Null
        }

        Copy-Item  -Force -Path $sourcePath -Destination $targetPath
      }
      $config.AssetsRepoBranch | Should -not -Be $sourceBranch

      PushAssetsRepoUpdate -Config $Config

      try {
        Push-Location $assetRepoFolder
        $repoBranchSHA = git rev-parse origin/$($config.AssetsRepoBranch) --quiet
      }
      finally {
        Pop-Location
      }

      # re-parse from the on-disk json
      $configReparsed = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables" "azure-data-tables")

      # re-parsed config from disk should
      #  -> match the updated sha
      #  -> match the SHA we get back from the repo
      $repoBranchSHA | Should -Be $config.SHA
      $configReparsed.SHA | Should -Be $config.SHA
    }

    It "Should push a new commit on top of an existing one. Doing a pre-fetch if necessary." {
      $sourceBranch = "scenario_conflict_push"
      $recordingJson = [PSCustomObject]@{
        AssetsRepo = "Azure/azure-sdk-assets-integration"
        AssetsRepoPrefixPath = "python/recordings/"
        AssetsRepoId = ""
        AssetsRepoBranch = "$sourceBranch"
        SHA = "e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7"
      }

      $files = @(
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_timeout.json",
        "sdk/tables/azure-data-tables/tests/recordings/test_retry.pyTestStorageRetrytest_retry_on_server_error.json",
        "sdk/tables/assets.json"
      )

      $testFolder = Describe-TestFolder -AssetsJsonContent $recordingJson -Files $files -IntegrationBranch $recordingJson.AssetsRepoBranch
      $config = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables")
      $config.AssetsRepoBranch | Should -not -Be $sourceBranch

      InitializeAssetsRepo -Config $config
      $assetRepoFolder = ResolveAssetRepoLocation -Config $config

      foreach($file in $files){
        $sourcePath = Join-Path $testFolder $file 
        $targetPath = Join-Path $assetRepoFolder $config.AssetsRepoPrefixPath $file

        $targetFolder = Split-Path $targetPath
        if(-not (Test-Path $targetFolder)){
          New-Item -Type Directory -Force -Path $targetFolder | Out-Null
        }

        Copy-Item  -Force -Path $sourcePath -Destination $targetPath
      }
      $config.AssetsRepoBranch | Should -not -Be $sourceBranch

      PushAssetsRepoUpdate -Config $Config

      try {
        Push-Location $assetRepoFolder
        $repoBranchSHA = git rev-parse origin/$($config.AssetsRepoBranch) --quiet
      }
      finally {
        Pop-Location
      }

      # re-parse from the on-disk json
      $configReparsed = ResolveAssetsJson (Join-Path $testFolder "sdk" "tables" "azure-data-tables")

      # re-parsed config from disk should
      #  -> match the updated sha
      #  -> match the SHA we get back from the repo
      $repoBranchSHA | Should -Be $config.SHA
      $configReparsed.SHA | Should -Be $config.SHA
    }
  }

  Context "ResetAssetsRepo" -Tag "Unit" {
    It "Should properly process an UserPrompt preventing changes with an accept." {
      Mock GetUserInput { param([PSCustomObject] $Config, [string] $UserPrompt) return 'y' }

    }

    It "Should properly process an UserPrompt preventing changes with a denial." {
      Mock GetUserInput { param([PSCustomObject] $Config, [string] $UserPrompt) return 'n' }

    }

    It "Should properly process a UserPrompt with no affirm or deny present." {
      Mock GetUserInput { param([PSCustomObject] $Config, [string] $UserPrompt) return '' }


    }

    It "Should noop when on the same SHA." {
      Mock GetUserInput { param([PSCustomObject] $Config, [string] $UserPrompt) return '' }

    }

    It "Should cleanly swap without any changes present." {
      Mock GetUserInput { param([PSCustomObject] $Config, [string] $UserPrompt) return '' }
    }
  }
}