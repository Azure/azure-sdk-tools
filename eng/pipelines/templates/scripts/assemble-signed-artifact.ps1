<#
.SYNOPSIS
Assembles a signed file + artifact into an output artifact.


.DESCRIPTION
Takes the signed output folder and assembles it into an output artifact. For each platform, there is a slightly different methodology to re-assembling them due to how they were constructed.

For windows signing:
  - We need to move code sign summary files to their own folder, we don't want to place the code sign summary in the package itself
  - After moving the code sign files, package the signed file directory.
  - Send the whole directory to a zip folder

For mac signing:
  - We will have an unzipped artifact folder, but will be handed the individual signed zip.
  - We need to unzip that payload.zip
  - We need to chmod +x the unzipped executable
  - We need to override the executable in the original artifact folder
  - Send the resulting directory to a zip folder.

For linux signing:
  - Do nothing, pass through


.PARAMETER Rid
The targeted dotnet framework. Example values: osx-x64, win-x64, linux-arm64

.PARAMETER BinariesDirectory
The folder containing the downloaded file contents of "binaries" artifact.

.PARAMETER SignedFileDirectory
The folder containing the result of the signing operation.

#>
param(
   [Parameter(mandatory=$true)]
   $Rid,
   [Parameter(mandatory=$true)]
   $BinariesDirectory,
   [Parameter(mandatory=$true)]
   $SignedFileDirectory,
   [Parameter(mandatory=$true)]
   $AssemblyName
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

function CopyCodeSignArtifacts {
   param(
      $SourceDir,
      $TargetDir
   )

   if (-not (Test-Path $TargetDir) ){
      New-Item -Force -ItemType Directory $TargetDir
   }

   Get-ChildItem -Filter "CodeSignSummary*" -Path $SourceDir | ForEach-Object {
      Move-Item -Path $_.FullName -Destination "$TargetDir"
   }
}

<#
For a sample run against rid osx-x64:

prepare-artifact-for-signing.ps1 runs -> unzips and readies for signing
signing stage runs, replaces dropped files in place
assemble-signed-artifact.ps1 runs -> moves files into the correct place if necessary, rezips

The file structure that this script will operate against will look like the following

<BinariesDir>
   test-proxy-standalone-osx-x64 <-- the expanded folder from the rid binary that we're currently working on
   <original test-proxy-standalone-osx-x64 file has been deleted by the same step that expanded it. when we recompress it'll nicely fit in place>
   test-proxy-standalone-osx-x64-Signing <-- only if necessary, like for mac (and maybe linux)
   test-proxy-standalone-linux-arm64.tar.gz <-- the other binaries that will be operated upon by other steps according to what rid is running
   test-proxy-standalone-linux-x64.tar.gz
   test-proxy-standalone-osx-arm64.tar.gz
   test-proxy-standalone-win-x64.zip
#>
try {
   Push-Location $BinariesDirectory

   if ($Rid.StartsWith("win")) {
      $unzippedArtifactName = (Split-Path $SignedFileDirectory -Leaf).ToString()

      Write-Host "Unzipped artifact name: $unzippedArtifactName"
      $summaryDirectory = Join-Path -Path $BinariesDirectory -ChildPath (Join-Path -Path "CodeSignSummaries" -ChildPath $unzippedArtifactName)
      $destinationDirectory = Join-Path -Path $BinariesDirectory -ChildPath $unzippedArtifactName

      CopyCodeSignArtifacts -SourceDir $SignedFileDirectory -TargetDir $summaryDirectory

      Compress-Archive -Path "$($SignedFileDirectory)/*" -DestinationPath "$($destinationDirectory).zip"
      Remove-Item -Recurse -Force $SignedFileDirectory
   }
   elseif ($Rid.StartsWith("osx")) {
      # understand where we need to copy the signed binary back to (as we had to zip this thing up to send it off)
      # we HAVE blah/blah/blah/test-proxy-standalone-osx-x64-Signing (from SignedFileDirectory)
      # we NEED blah/blah/blah/test-proxy-standalone-osx-x64
      $unzippedArtifactName = (Split-Path $SignedFileDirectory -Leaf).ToString().Replace("-Signing", "")
      $destinationDirectory = Join-Path -Path $BinariesDirectory -ChildPath $unzippedArtifactName
      $summaryDirectory = Join-Path -Path $BinariesDirectory -ChildPath (Join-Path -Path "CodeSignSummaries" -ChildPath $unzippedArtifactName)

      $payload = Join-Path -Path $SignedFileDirectory -ChildPath payload.zip
      Expand-Archive $payload -DestinationPath $SignedFileDirectory
      Remove-Item $payload

      # for mac/linux, the executable will be the only item without an extension. force linux path style for bash call
      $binaryFile = (Get-ChildItem -Path $SignedFileDirectory | Where-Object { $_.Name -eq $AssemblyName } | Select-Object -First 1).ToString().Replace("`\","/")

      # per daniel jurek, we have to make this executable an executable again after it is signed
      bash -c "chmod +x $binaryFile"

      Move-Item -Force -Path $binaryFile -Destination $destinationDirectory

      CopyCodeSignArtifacts -SourceDir $SignedFileDirectory -TargetDir $summaryDirectory

      Compress-Archive -Path "$($destinationDirectory)/*" -DestinationPath "$($destinationDirectory).zip"
      Remove-Item -Recurse -Force $destinationDirectory
      Remove-Item -Recurse -Force $SignedFileDirectory/
   }
   else {
      # it never was unzipped, so we will just upload the target
      Write-Host "Linux artifacts are not currently signed by the azure-sdk team."
   }
}
finally {
   Pop-Location
}
