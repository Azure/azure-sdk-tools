<#
.SYNOPSIS 
Publishes a standalone dotnet executable to an artifact staging directory.

.DESCRIPTION
Assembles a standalone executable and places it within the given staging directory. This script takes care of any additional minutae that is required to
enable a usable binary down the line after signing or notarization.

.PARAMETER Rid
The target platform. Takes the form of "osx-x64", "win-arm64", "linux-x64", etc. A full list is available here: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

.PARAMETER ArtifactStagingDirectory
The root directory which will receive the compressed standalone executable. 

.PARAMETER Target 
The targeted folder that should be built and assembled into a standalone executable.

.PARAMETER Framework
The targeted .NET framework. Defaults to "net6.0."

#>
param(
   [Parameter(mandatory=$true)]
   [string] $Rid,
   [Parameter(mandatory=$true)]
   [string] $Target,
   [Parameter(mandatory=$true)]
   [string] $ArtifactStagingDirectory,
   [Parameter(mandatory=$true)]
   [string] $AssemblyName,
   [Parameter(mandatory=$false)]
   [string] $Framework = "net6.0"
)

# resolves to <artifactfolder>/win-x64
$destinationArtifactFolder = Join-Path $ArtifactStagingDirectory $Rid

# resolves to <artifactfolder>/win-x64/test-proxy-standalone-win-x64 (.zip or .tar.gz will be added as appropriate for platform)
$destinationPathSegment = Join-Path $destinationArtifactFolder "$(Split-Path -Leaf "$Target")-standalone-$Rid"

# resolves to tools/test-proxy/win-x64
$outputPath = Join-Path $Target $Rid

# ensure the destination artifact directory exists
if (!(Test-Path $destinationArtifactFolder)){
   New-Item -Force -Path $destinationArtifactFolder -ItemType directory
}

Write-Host "dotnet publish -f $Framework -c Release -r $Rid -p:PublishSingleFile=true --self-contained --output $outputPath $Target"
dotnet publish -f $Framework -c Release -r $Rid -p:PublishSingleFile=true --self-contained --output $outputPath $Target

if ($LASTEXITCODE -ne 0) {
   Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
   exit $LASTEXITCODE
}

# produce a tar.gz only for linux
if ("$($Rid)".Contains("linux")){
   # tar on powershell in linux has some weirdness. For instance, this is a proper call to tar when we don't want to include the relative path to the target folder
   # tar -cvzf -C tools/test-proxy/linux-arm64 blah.tar.gz tools/test-proxy/linux-arm64 
   # however when we use this, we actually get an error. To avoid this, we simply CD into the target directory before tar-ing it.
   Push-Location "$outputPath"
   # The sum contents within this folder will be: `appSettings.json`, `test-proxy.pdb`, `test-proxy` (the binary), and a certificate.
   # This statement grabs the first extensionless file within the produced binary folder, which will always be the binary we need to set the executable bit on.
   $binaryFile = (Get-ChildItem -Path . | Where-Object { $_.Name -eq $AssemblyName } | Select-Object -First 1).ToString().Replace("`\","/")

   bash -c "chmod +x $binaryFile"
   tar -cvzf "$($destinationPathSegment).tar.gz" .
   Pop-Location
}
elseif("$($Rid)".Contains("osx")){
   # need to codesign the binary with an entitlements file such that the signed and notarized binary will properly invoke on
   # a mac system. However, the `codesign` command is only available on a MacOS agent. With that being the case, we simply special case
   # this function here to ensure that the script does not fail outside of a MacOS agent.
   if ($IsMacOS) {
      $binaryFile = Get-ChildItem -Path $outputPath | Where-Object { $_.Name -eq $AssemblyName } | Select-Object -First 1
      $binaryFileBash = $binaryFile.ToString().Replace("`\","/")

      $entitlements = (Resolve-Path -Path (Join-Path $PSScriptRoot ".." ".." ".." "dotnet-executable-entitlements.plist")).ToString().Replace("`\", "/")

      bash -c "codesign --deep -s - -f --options runtime --entitlements $($entitlements) $($binaryFileBash)"
      bash -c "codesign -d --entitlements :- $($binaryFileBash)"
   }

   Compress-Archive -Path "$($outputPath)/*" -DestinationPath "$($destinationPathSegment).zip"
}
else {
   Compress-Archive -Path "$($outputPath)/*" -DestinationPath "$($destinationPathSegment).zip"
}

# clean up the uncompressed artifact directory
Remove-Item -Recurse -Force -Path $outputPath



