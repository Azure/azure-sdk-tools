Function Get-Basic-RecordingJson {
  param(
    [boolean]$RandomizeRepoId = $true
  )
  $result = (Get-Content -Raw -Path (Join-Path $PSScriptRoot "recording.json") | ConvertFrom-Json)

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
    # if provided, this content will be written to a recording.json at root of area, or
    # somewhere else in the generated file tree.
    [string] $RecordingJsonContent, 
    # Files is a set of relative paths that will form the basis of the environment on disk
    # ["a/", "b.json", "b/c.json", "b/d/c.json", "a/recording.json" ]
    [string[]] $Files
  )
  
  $testPath = Get-TestPath

  $recordingJsonLocation = Join-Path $testPath "recording.json"
  
  foreach($file in $files){
    if($file.ToLower().EndsWith("recording.json")){
      $recordingJsonLocation = Join-Path $testPath $file
    }
  }
  
  if ($RecordingJsonContent){
    Set-Content -Value ($RecordingJsonContent | ConvertTo-Json) -Path $recordingJsonLocation | Out-Null
  }

  foreach($file in $Files){
    $ext = [System.IO.Path]::GetExtension($file)
    $resolvedFilePath = (Join-Path $testPath $file)

    if ($ext){
      if ($ext -eq ".json" -and -not $file.EndsWith("recording.json")){
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
