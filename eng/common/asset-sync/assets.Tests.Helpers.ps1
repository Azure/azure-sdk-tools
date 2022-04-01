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
  $usePersistentStorage = $env:USE_LOCAL_TEST_PATHS

  $localTempPath = Get-TestFolder

  if (-not $usePersistentStorage){
    $testPath = "TestDrive:\$([Guid]::NewGuid())\"
    
    mkdir $testPath | Out-Null
    return $testPath
  }
  else {
    $testPath = (Join-Path $localTempPath "$([Guid]::NewGuid())")

    $result = mkdir -p $testPath | Out-Null
    return $testPath
  }
}

Function Describe-TestFolder{
  param(
    # if provided, this content will be written to a assets.json at root of area, or
    # somewhere else in the generated file tree.
    [string] $AssetsJsonContent, 
    # Files is a set of relative paths that will form the basis of the environment on disk
    # ["a/", "b.json", "b/c.json", "b/d/c.json", "a/assets.json" ]
    [string[]] $Files
  )
  
  $testPath = Get-TestPath

  $assetJsonLocation = Join-Path $testPath "assets.json"
  
  foreach($file in $files){
    if($file.ToLower().EndsWith("assets.json")){
      $assetJsonLocation = Join-Path $testPath $file
    }
  }
  
  if ($AssetsJsonContent){
    Set-Content -Value ($AssetsJsonContent | ConvertTo-Json) -Path $assetJsonLocation | Out-Null
  }

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
