# Pester is installed via Install-Module -Name Pester -Force
# Followed by Invoke-Pester ./assets.Tests.ps1
BeforeAll {
  Import-Module -Force -DisableNameChecking $PSScriptRoot/"assets.psm1"

  Function Get-Basic-RecordingJson {
    return (Get-Content -Raw -Path (Join-Path $PSScriptRoot "recording.json"))
  }

  Function Get-Full-TestPath {
    Param(
        [string] $Path
    )
    return $Path.Replace('TestDrive:', (Get-PSDrive TestDrive).Root)
  }

  Function Get-TestPath {
    $useTempStorage = $env:USE_LOCAL_TEST_PATHS

    $localTempPath = (Join-Path $PSScriptRoot ".testruns")

    if (-not $useTempStorage){
      $testPath = "TestDrive:\$([Guid]::NewGuid())\"
      
      mkdir $testPath
      return $testPath
    }
    else {
      $testPath = (Join-Path $localTempPath "$([Guid]::NewGuid())")

      if (-not (Test-Path $localTempPath)){
        Remove-Item -Recurse -Force $localTempPath
      }

      mkdir -p $testPath
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
      Set-Content -Value $RecordingJsonContent -Path $recordingJsonLocation
    }

    foreach($file in $Files){
      $ext = [System.IO.Path]::GetExtension($file)
      $resolvedFilePath = (Join-Path $testPath $file)

      # handle as file
      if ($ext){
        if ($ext -eq ".json" -and -not $file.EndsWith("recording.json")){
          $directory = Split-Path $resolvedFilePath

          if (-not (Test-Path $directory)){
            mkdir -p $directory
          }
          
          Set-Content -Value ("{ `"a`": `"" + ([guid]::NewGuid().ToString()) + "`" }") `
            -Path resolvedFilePath
        }
      }
      # handle as folder
      else {
        if (-not (TestPath $file)){
          mkdir -p $file
        }
      }
    }

    return $testPath
  }
}

Write-Host (Join-Path $PSScriptRoot ".." "assets.psm1")

# for setting up mocks, use https://pester.dev/docs/usage/testdrive
# $testPath = "TestDrive:\test.txt"

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
      
      $testLocation = Describe-TestFolder -RecordingJsonContent (Get-Basic-RecordingJson) `
        -Files @(
          "a/b.json"
        )
      $Value = Evaluate-Target-Dir -TargetPath (Join-Path $PSScriptRoot ".." ".." "..")
      $Value | Should -Be @($false, $true)
    }
  }
}