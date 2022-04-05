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

Function Describe-TestFolder{
  param(
    # if provided, this content will be written to a assets.json at root of area, or
    # somewhere else in the generated file tree.
    [PSCustomObject] $AssetsJsonContent, 
    # Files is a set of relative paths that will form the basis of the environment on disk
    # ["a/", "b.json", "b/c.json", "b/d/c.json", "a/assets.json" ]
    [string[]] $Files
  )
  
  $testPath = Get-TestPath

  "test content" | Set-Content -Path (Join-Path $testPath ".git")
  $assetJsonLocation = Join-Path $testPath "assets.json"

  foreach($file in $files){
    if($file.ToLower().EndsWith("assets.json")){
      
      $assetJsonLocation = Join-Path $testPath $file

      $directory = Split-Path $assetJsonLocation

      if (-not (Test-Path $directory)){
        mkdir -p $directory | Out-Null
      }
    }
  }

  if ($AssetsJsonContent){
    $AssetsJsonContent | ConvertTo-Json | Set-Content -Path $assetJsonLocation | Out-Null
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
