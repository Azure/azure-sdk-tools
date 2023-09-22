# Helper functions used by the CLI Integration Tests

# Test whether or not a given executable is in the path. This is used to ensure TestProxy.exe
# has been installed and is on the path.
Function Test-Exe-In-Path {
  Param([string] $ExeToLookFor)
  if ($null -eq (Get-Command $ExeToLookFor -ErrorAction SilentlyContinue)) {
    Write-Host "Unable to find $ExeToLookFor in your PATH"
    return $false
  }
  else {
    Write-Host "Found $ExeToLookFor in path"
    return $true
  }
}

# Get the CloneURL. If the GIT_TOKEN environment variable has been set
# then the token is part of the git URL.
Function Get-CloneUrl {
  Param([string] $AssetsRepo)
  $gitToken = $env:GIT_TOKEN
  if (-not ([string]::IsNullOrWhitespace($gitToken))) {
    return "https://$($gitToken)@github.com/$($AssetsRepo)"
  }
  else {
    return "https://github.com/$($AssetsRepo)"
  }
}

# Returns the TEMP directory
Function Get-TestFolder {
  return [System.IO.Path]::GetTempPath()
}

# Gets the path that will be used by the test to do Git actions. A
# temporary, unique directory is created of the form <TEMP>/<GUID>
# unless permanent storage is disabled at which point the temporary
# directory is TestDrive:<GUID>
Function Get-TestPath {
  Param([string] $TestGuid)

  $disablePermanentStorage = $env:USE_TESTDRIVE
  $localTempPath = Get-TestFolder

  if ($disablePermanentStorage) {
    $testPath = "TestDrive:\$TestGuid\"

    New-Item -Type Directory -Force -Path $testPath | Out-Null
    return $testPath
  }
  else {
    $testPath = (Join-Path $localTempPath "$($TestGuid)")

    New-Item -Type Directory -Force -Path $testPath | Out-Null
    return $testPath
  }
}

# This is only used by the push tests. It basically creates a tag that
# we can automatically push to instead of one that requires a PR and
# has to be manually merged. This is necessary for automation.
Function Initialize-Integration-Tag {
  param(
    [PSCustomObject] $Assets,
    [string] $AdjustedAssetsRepoTag
  )

  $tempPath = "TestDrive:\$([Guid]::NewGuid())\"
  New-Item -Type Directory -Force -Path $tempPath | Out-Null
  try {
    Push-Location $tempPath
    $gitCloneUrl = Get-CloneUrl $Assets.AssetsRepo
    Write-Host "git clone $($gitCloneUrl) ."
    git clone $gitCloneUrl . | Out-Null
    Write-Host "git ls-remote --heads $($gitCloneUrl) $($Assets.TagPrefix)"
    $stdOut = git ls-remote --heads $($gitCloneUrl) $($Assets.TagPrefix) | Out-String
    # If the command response is empty there's nothing to do and we can just return
    if ([string]::IsNullOrWhitespace($stdOut)) {
      return
    }

    # If the response isn't empty, we need to create the test tag
    # 1. Checkout the current TagPrefix
    Write-Host "git checkout $($Assets.TagPrefix)"
    git checkout $($Assets.TagPrefix) | Out-Null
    # 2. Create the AdjustedAssetsRepoTag from the original TagPrefix. The reason being
    #    is that pushing will be automatic.
    Write-Host "git tag $($AdjustedAssetsRepoTag)"
    git tag $($AdjustedAssetsRepoTag) | Out-Null
    # 3. Push the contents of the TagPrefix into the AdjustedAssetsRepoTag
    Write-Host "git push origin $($AdjustedAssetsRepoTag)"
    git push origin $($AdjustedAssetsRepoTag) | Out-Null
  }
  catch {
    Write-Error $_
  }
  finally {
    Pop-Location
    Remove-Item -Force -Recurse $tempPath | Out-Null
  }
  return
}

# Clean up the tag that was pushed as part of the integration tests.
Function Remove-Integration-Tag {
  param(
    [PSCustomObject] $Assets
  )

  # This can happen if the test failed prior to the push happening
  if ($null -eq $Assets) {
    Write-Host "Remove-Integration-Tag - Assets was null, nothing to clean up."
    return
  }
  # If cleanup is disabled, for diagnostics purposes, just return. This is here in case
  # a test calls this directly
  if (!([string]::IsNullOrWhitespace($env:DISABLE_INTEGRATION_BRANCH_CLEANUP))) {
    return
  }

  $tempPath = "TestDrive:\$([Guid]::NewGuid())\"
  New-Item -Type Directory -Force -Path $tempPath | Out-Null

  try {
    Push-Location $tempPath
    $gitCloneUrl = Get-CloneUrl $Assets.AssetsRepo
    Write-Host "git clone  --filter=blob:none $($gitCloneUrl) ."
    git clone $($gitCloneUrl) .
    Write-Host "git push origin --delete $($Assets.Tag)"
    git push origin --delete $($Assets.Tag)
  }
  catch {
    Write-Error $_
  }
  finally {
    Pop-Location
    Remove-Item -Force -Recurse $tempPath
  }
}

# Used to define any set of file constructs we want. This enables us to
# roll a target environment to point various GitStore functionalities at.
# Creates folder under the temp directory (or TestDrive if permanent storage
# has been disabled) that will be used for testing CLI commands.
Function Describe-TestFolder {
  param(
    [PSCustomObject] $AssetsJsonContent,
    [string[]] $Files = @(),
    [bool] $IsPushTest = $false
  )

  $testGuid = [Guid]::NewGuid()
  $testPath = Get-TestPath $testGuid
  # Initialize-Integration-Tag only needs to be called when running a push test
  if ($IsPushTest) {
    $adjustedAssetsRepoTag = "test_$($testGuid)_$($AssetsJsonContent.TagPrefix)"
    Initialize-Integration-Tag -Assets $AssetsJsonContent -AdjustedAssetsRepoTag $adjustedAssetsRepoTag
    $AssetsJsonContent.TagPrefix = $adjustedAssetsRepoTag
  }

  New-Item -ItemType Directory -Path (Join-Path $testPath ".git") | Out-Null
  $assetJsonLocation = Join-Path $testPath "assets.json"

  # if a path ending with assets.json is provided, the assets.json content will be written there
  # instead of the root of the test folder
  foreach ($file in $files) {
    if ($file.ToLower().EndsWith("assets.json")) {

      $assetJsonLocation = Join-Path $testPath $file

      $directory = Split-Path $assetJsonLocation

      if (-not (Test-Path $directory)) {
        New-Item -Type Directory -Force -Path $directory | Out-Null
      }
    }
  }

  # write the content
  if ($AssetsJsonContent) {
    $AssetsJsonContent | ConvertTo-Json | Set-Content -Path $assetJsonLocation | Out-Null
  }

  # generate some fake files and folders
  foreach ($file in $Files) {
    $ext = [System.IO.Path]::GetExtension($file)
    $resolvedFilePath = (Join-Path $testPath $file)

    if ($ext) {
      if ($ext -eq ".json" -and -not $file.EndsWith("assets.json")) {
        $directory = Split-Path $resolvedFilePath

        if (-not (Test-Path $directory)) {
          New-Item -Type Directory -Force -Path $directory | Out-Null
        }

        $testObj = [PSCustomObject]@{
          a = [guid]::NewGuid().ToString()
        } | ConvertTo-Json

        New-Item -Path $resolvedFilePath -ItemType File -Value $testObj | Out-Null
      }
    }
    else {
      if (-not (Test-Path $file)) {
        New-Item -Type Directory -Force -Path $file | Out-Null
      }
    }
  }

  if($IsLinux -or $IsMacOS){
    chmod 777 $testPath
  }

  return $testPath.Replace("`\", "/")
}

# Cleanup the test folder used for testing. The DISABLE_INTEGRATION_BRANCH_CLEANUP
# environment variable will allow us to keep things around for investigations.
Function Remove-Test-Folder {
  param(
    [string] $TestFolder = ""
  )
  if (![string]::IsNullOrWhitespace($env:DISABLE_INTEGRATION_BRANCH_CLEANUP)) {
    return
  }

  if ($null -ne $TestFolder) {
    Remove-Item -LiteralPath $TestFolder -Force -Recurse
  }
}

# Invoke the proxy command and echo the output. The WriteOutput
# is used to output a command response when executing Reset commands.
# When Reset detects pending changes it'll prompt to override. This
# works because there's a single response required.
Function Invoke-ProxyCommand {
  param(
    [string] $TestProxyExe,
    [string] $CommandArgs,
    [string] $MountDirectory,
    [string] $WriteOutput = $null
  )

  if ($TestProxyExe.Trim().ToLower() -eq "test-proxy") {
    $CommandArgs += " --storage-location=$MountDirectory"
    Write-Host "$TestProxyExe $CommandArgs"
    # Need to cast the output into an array otherwise it'll be one long string with no newlines
    if ($WriteOutput) {
        # CommandArgs needs to be split otherwise all of the arguments will be quoted into a single
        # argument.
        [array] $output = Write-Output $WriteOutput | & "$TestProxyExe" $CommandArgs.Split(" ")
    } else {
        [array] $output = & "$TestProxyExe" $CommandArgs.Split(" ")
    }
    # echo the command output
    foreach ($line in $output) {
      Write-Host "$line"
    }
  }
  elseif ($TestProxyExe.Trim().ToLower() -eq "docker"){
    $updatedDirectory = $MountDirectory.ToString().Replace("`\", "/")   # docker doesn't play well with windows style paths when binding a volume, lets keep it simple.
    $token = $env:GIT_TOKEN
    $commiter = $env:GIT_COMMIT_OWNER
    $email = $env:GIT_COMMIT_EMAIL

    $targetImage = if ($env:CLI_TEST_DOCKER_TAG) { $env:CLI_TEST_DOCKER_TAG } else { "azsdkengsys.azurecr.io/engsys/test-proxy:latest" }

    $AmendedArgs = @(
      "run --rm --name transition.test.proxy",
      "-v `"${updatedDirectory}:/srv/testproxy`"",
      "-e `"GIT_TOKEN=${token}`"",
      "-e `"GIT_COMMIT_OWNER=${commiter}`"",
      "-e `"GIT_COMMIT_EMAIL=${email}`"",
      $targetImage,
      "test-proxy",
      $CommandArgs
    ) -join " "

    Write-Host "$TestProxyExe $AmendedArgs"
    # Need to cast the output into an array otherwise it'll be one long string with no newlines
    [array] $output = Write-Output $WriteOutput | & $TestProxyExe $AmendedArgs.Split(" ")

    # echo the command output
    foreach ($line in $output) {
      Write-Host "$line"
    }
  }
  else {
    throw "Unrecognized exe `"$TestProxyExe`""
  }
}

# The assets directory will either be in the .assets directory, in the same
# directory as the assets.json file OR the PROXY_ASSETS_FOLDER. Now, in order
# to not have a ridiculous subdirectory length, the proxy creates a 10 character
# short hash directory, instead of having Azure/azure-sdk-assets-integration/
# <AssetsRepoPrefixPath>, which will be the only sub-directory. From there
# For testing purposes we know that there will only be single sub-directory under
# which the directory referenced by the AssetsRepoPrefixPath will contain the files
# being manipulated by the CLI commands.
Function Get-AssetsFilePath {
  param(
    [PSCustomObject] $AssetsJsonContent,
    [string] $AssetsJsonFile
  )
  $startingPath = $env:PROXY_ASSETS_FOLDER
  # If the assets folder is not defined then the .assets directory will
  # be in the same directory that the assets json file is in
  if ([string]::IsNullOrWhitespace($startingPath)) {
    $assetsDir = Split-Path -Path $AssetsJsonFile
    $startingPath = Join-Path -Path $assetsDir -ChildPath ".assets"
  }
  # It's odd that $folder.Count and $folders.Length work and we need to do this
  $numDirs = Get-ChildItem $startingPath -Directory | Where-Object { $_.Name -ne "breadcrumb" } | Measure-Object | ForEach-Object { $_.Count }
  $folders = Get-ChildItem $startingPath -Directory | Where-Object { $_.Name -ne "breadcrumb" } 

  # There should only be one folder
  if (1 -ne $numDirs) {
    LogError "The assets directory ($startingPath) should only contain 1 subfolder not $numDirs ($folders -join $([Environment]::NewLine))"
    return $assetsFilePath
  }
  $assetsFilePath = Join-Path -Path $folders[0].FullName -ChildPath $AssetsJsonContent.AssetsRepoPrefixPath
  return $assetsFilePath
}


# Given a directory and an expected number of files, verify that we only have that many files.
Function Test-DirectoryFileCount {
  param(
    [string] $Directory,
    [int] $ExpectedNumberOfFiles
  )
  $numFiles = Get-ChildItem $Directory -File | Measure-Object | ForEach-Object{$_.Count}
  $ExpectedNumberOfFiles | Should -Be $numFiles
}

# For testing purposes file versions are a single number within the file. Verify that
# the version in the file is the one we expect.
Function Test-FileVersion {
  param(
    [string] $FilePath,
    [string] $FileName,
    [int] $ExpectedVersion
  )
  $fileFullPath = Join-Path -Path $FilePath -ChildPath $FileName
  $actualVersion = Get-Content -Path $fileFullPath -TotalCount 1
  $actualVersion | Should -Be $ExpectedVersion
}

# This will create a new file that contains the input version number or
# update an existing file to the input version number
Function Edit-FileVersion {
  param(
    [string] $FilePath,
    [string] $FileName,
    [int] $Version
  )
  # this will create the file if it doesn't exist and set the version if it does
  $fileFullPath = Join-Path -Path $FilePath -ChildPath $FileName
  $Version | Out-File $fileFullPath
}

# Given an assets.json file on disk, create a PSCustomObject from that Json file.
Function Update-AssetsFromFile {
  param(
    [string] $AssetsJsonContent
  )
  return [PSCustomObject](Get-Content $AssetsJsonContent | Out-String | ConvertFrom-Json)
}

# Verify that a given tag exists.
Function Test-TagExists{
  param(
    [PSCustomObject] $AssetsJsonContent,
    [string] $WorkingDirectory
  )
  $tagExists = $false
  try {
    Push-Location $WorkingDirectory
    $gitCloneUrl = Get-CloneUrl $AssetsJsonContent.AssetsRepo
    Write-Host "git ls-remote --heads $($gitCloneUrl) $($AssetsJsonContent.Tag)"
    $stdOut = git ls-remote --heads $($gitCloneUrl) $($AssetsJsonContent.Tag) | Out-String
    if ([string]::IsNullOrWhitespace($stdOut)) {
      $tagExists = $true
    } else {
      Write-Host "Test-TagExists git ls-remote --heads $($gitCloneUrl) $($AssetsJsonContent.Tag) returned:=$stdOut"
    }
  } finally {
    Pop-Location
  }
  return $tagExists
}