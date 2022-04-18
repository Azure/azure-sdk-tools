Function Get-Basic-AssetsJson {
  param(
    [boolean]$RandomizeRepoId = $true
  )
  $result = (Get-Content -Raw -Path (Join-Path $PSScriptRoot "assets.json") | ConvertFrom-Json)

  if ($RandomizeRepoId) {
    Add-Member -InputObject $result -MemberType "NoteProperty" -Name "AssetRepoId" -Value (New-Guid).ToString()
  }

  return $result
}

Function Get-Full-TestPath {
  Param(
      [string] $Path
  )
  return $Path.Replace('TestDrive:', (Get-PSDrive TestDrive).Root)
}

Function Get-TestFolder {
  return (Join-Path $PSScriptRoot ".testruns")
}

Function Get-TestPath {
  $disablePermanentStorage = $env:USE_TESTDRIVE

  $localTempPath = Get-TestFolder

  if ($disablePermanentStorage){
    $testPath = "TestDrive:\$([Guid]::NewGuid())\"
    
    mkdir $testPath | Out-Null
    return $testPath
  }
  else {
    $testPath = (Join-Path $localTempPath "$([Guid]::NewGuid())")

    mkdir -p $testPath | Out-Null
    return $testPath
  }
}

Function Initialize-Integration-Branches {
  param(
    [PSCustomObject] $Config,
    [string] $TestGuid
  )

  $adjustedBranchName = "test_$($TestGuid)_$($Config.AssetsRepoBranch)"
  $tempPath = "TestDrive:\$([Guid]::NewGuid())\"
  mkdir $tempPath | Out-Null
  
  try {
    Push-Location $tempPath
    Write-Host "git clone https://github.com/Azure/azure-sdk-assets-integration ."
    git clone https://github.com/Azure/azure-sdk-assets-integration .

    Write-Host "git ls-remote --heads https://github.com/Azure/azure-sdk-assets-integration $($Config.AssetsRepoBranch)"
    $lsremoteResponse = git ls-remote --heads https://github.com/Azure/azure-sdk-assets-integration $Config.AssetsRepoBranch
    
    Write-Host $lsremoteResponse
    Write-Host $adjustedBranchName
    if($lsremoteResponse){
      Write-Host "git checkout $($Config.AssetsRepoBranch)"
      Write-Host "git checkout *"
      Write-Host "git clean -xdf"
      Write-Host "git checkout -b $adjustedBranchName"
      Write-Host "git push origin $adjustedBranchName"
      git checkout $Config.AssetsRepoBranch
      git checkout *
      git clean -xdf
      git checkout -b $adjustedBranchName
      git push origin $adjustedBranchName
    }
   }
  catch {
    Write-Error $_
  }
  finally {
    Pop-Location
    Remove-Item -Force -Recurse $tempPath
  }

  return $adjustedBranchName
}

Function DeInitialize-Integration-Branches {
  $targetedRepo = @("Azure/Azure-sdk-assets-integration")
  Write-Host "Cleaning up $targetedRepo"

  $tempPath = "TestDrive:\$([Guid]::NewGuid())\"
  mkdir $tempPath | Out-Null
  
  try {
    Push-Location $tempPath
    git clone https://github.com/Azure/azure-sdk-assets-integration .

    $branches = git branch -a
    
    foreach($branch in $branches){
      $adjustedName = $branch.Replace("remotes/origin/", "").Trim()
      if($adjustedName.Contains("test_")){
        Write-Host "`"$adjustedName`""

        git checkout $adjustedName
        git push origin --delete $adjustedName
      }
    }
   }
  catch {
    Write-Error $_
  }
  finally {
    Pop-Location
    Remove-Item -Force -Recurse $tempPath
  } 
}

<#
.SYNOPSIS
Creates a unique test folder when invoked from a test.

.DESCRIPTION
Initializes file and folder content as listed in -Files, an assets.json if content is provided, and git integration branches if necessary.

.PARAMETER AssetsJsonContent
If provided, this content will be written to a assets.json at root of area, or
somewhere else in the generated file tree. 

.PARAMETER Files
Files is a set of relative paths that will form the basis of the environment on disk
EG: ["a/", "b.json", "b/c.json", "b/d/c.json", "a/assets.json" ]

.PARAMETER IntegrationBranch
The target branch that will be duplicated and set in the AssetsJson that is written to disk. If not provided, no special functionality will invoke.
#>
Function Describe-TestFolder{
  param(
    [PSCustomObject] $AssetsJsonContent, 
    [string[]] $Files,
    [string] $IntegrationBranch = ""
  )

  $testPath = Get-TestPath

  if($IntegrationBranch){
    $testGuid = Split-Path $testPath -Leaf
    $new_target_branch = Initialize-Integration-Branches -Config $AssetsJsonContent -TestGuid $testGuid
    $AssetsJsonContent.AssetsRepoBranch = $new_target_branch
  }

  "test content" | Set-Content -Path (Join-Path $testPath ".git")
  $assetJsonLocation = Join-Path $testPath "assets.json"

  # if a path ending with assets.json is provided, the assets.json content will be written there
  # instead of the root of the test folder
  foreach($file in $files){
    if($file.ToLower().EndsWith("assets.json")){
      
      $assetJsonLocation = Join-Path $testPath $file

      $directory = Split-Path $assetJsonLocation

      if (-not (Test-Path $directory)){
        mkdir -p $directory | Out-Null
      }
    }
  }

  # write the content
  if ($AssetsJsonContent){
    $AssetsJsonContent | ConvertTo-Json | Set-Content -Path $assetJsonLocation | Out-Null
  }

  # generate some fake files and folders
  foreach($file in $Files){
    $ext = [System.IO.Path]::GetExtension($file)
    $resolvedFilePath = (Join-Path $testPath $file)

    if ($ext){
      if ($ext -eq ".json" -and -not $file.EndsWith("assets.json")){
        $directory = Split-Path $resolvedFilePath

        if (-not (Test-Path $directory)){
          mkdir -p $directory | Out-Null
        }
        
        New-Item -Path $resolvedFilePath -ItemType File -Value ("{ `"a`": `"" + ([guid]::NewGuid().ToString()) + "`" }") | Out-Null
      }
    }
    else {
      if (-not (Test-Path $file)){
        mkdir -p $file | Out-Null
      }
    }
  }

  return $testPath
}
