<#
.SYNOPSIS
Prepares an individual binary artifact for signing.

.DESCRIPTION
Examines a binaries directory, prepares an individual artifact (as selected by the RID parameter) for signing. This preparation is unfortunately a bit different per platform. 

For windows signing:

  - Decompress artifact zip folder
  - Prepare variable with targeted zip folder, this folder will be passed along with filter **/*.exe to the signing service.

For mac signing:
  - Decompress artifact zip folder
  - Compress just the executable into zip file under a different staging directory
  - Prepare variable with different staging directory containing the isolated zip file.

For linux signing:
  - Do nothing

This is necessary because the ESRP mac signing requires our binaries to be contained within a zip file.

.PARAMETER Rid
The targeted dotnet framework. Example values: osx-x64, win-x64, linux-arm64

.Parameter BinariesDirectory
The folder containing the downloaded file contents of "binaries" artifact.

#>
param (
   [Parameter(mandatory=$true)]
   $Rid,
   [Parameter(mandatory=$true)]
   $BinariesDirectory,
   [Parameter(mandatory=$true)]
   $AssemblyName
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

<#
Before run targeting osx-x64:

<BinariesDir>
   test-proxy-standalone-linux-arm64.tar.gz
   test-proxy-standalone-linux-x64.tar.gz
   test-proxy-standalone-osx-arm64.tar.gz
   test-proxy-standalone-osx-x64.zip
   test-proxy-standalone-win-x64.zip

prepare-artifact-for-signing.ps1 runs -> unzips and readies for signing

<BinariesDir>
   test-proxy-standalone-osx-x64 <-- the expanded folder from the rid binary that we're currently working on
   test-proxy-standalone-osx-x64-Signing <-- only if necessary, like for mac (and maybe linux)
   test-proxy-standalone-linux-arm64.tar.gz
   test-proxy-standalone-linux-x64.tar.gz
   test-proxy-standalone-osx-arm64.tar.gz
   test-proxy-standalone-win-x64.zip
#>
try {
   Push-Location $BinariesDirectory

   if ($Rid.StartsWith("win")) {
      $FileBeforeSigning = Get-ChildItem -Recurse -Force $BinariesDirectory -Filter "*$($Rid)*" | Select-Object -First 1
      $UnzippedArtifactDirectory = Join-Path -Path $BinariesDirectory -ChildPath ([System.IO.Path]::GetFileNameWithoutExtension($FileBeforeSigning))
   
      Expand-Archive $FileBeforeSigning
   
      Remove-Item -Force $FileBeforeSigning
      Write-Host "##vso[task.setvariable variable=SigningArtifactDir]$UnzippedArtifactDirectory"
   }
   elseif ($Rid.StartsWith("osx")) {
      $FileBeforeSigning = Get-ChildItem -Recurse -Force "$BinariesDirectory" -Filter "*$($Rid)*" | Select-Object -First 1
      $ArtifactName = [System.IO.Path]::GetFileNameWithoutExtension($FileBeforeSigning)
      $UnzippedArtifactDirectory = Join-Path -Path $BinariesDirectory -ChildPath $ArtifactName
      $SigningExclusionDirectory = Join-Path -Path $BinariesDirectory -ChildPath "$($ArtifactName)-Signing"

      New-Item -Force -ItemType Directory -Path $SigningExclusionDirectory | Out-Null
      Expand-Archive $FileBeforeSigning

      $binaryFile = Get-ChildItem -Path $UnzippedArtifactDirectory | Where-Object { $_.Name -eq $AssemblyName } | Select-Object -First 1

      Compress-Archive -Path $binaryFile -DestinationPath (Join-Path $SigningExclusionDirectory payload.zip)

      Remove-Item -Force $FileBeforeSigning
      Write-Host "##vso[task.setvariable variable=SigningArtifactDir]$SigningExclusionDirectory"
   }
   else {
      Write-Host "##vso[task.setvariable variable=SigningArtifactDir]$BinariesDirectory"
      Write-Host "Linux artifacts are not currently signed by the azure-sdk team."
   }
}
finally {
   Pop-Location
}