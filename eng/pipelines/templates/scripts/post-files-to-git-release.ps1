<#
.SYNOPSIS
Posts a set of files FROM a directory TO a targeted github release.

.DESCRIPTION
Walk the directory with a file path filter


.PARAMETER TargetRelease
What is the name of the github release that we want to attach these artifacts to?

.PARAMETER BinariesDirectory
The folder containing the downloaded file contents of "binaries" artifact.

.PARAMETER FileFilter
The folder containing the result of the signing operation.

#>
param(
   [Parameter(mandatory=$true)]
   $TargetRelease,
   [Parameter(mandatory=$true)]
   $BinariesDirectory,
   $RepoId = "azure/azure-sdk-tools",
   $FileFilter = "*.*"
)

. "$PSScriptRoot/github-api-interactions.ps1"

$SearchPath = Join-Path $BinariesDirectory "*"
$filesForPublish = Get-ChildItem -Path $SearchPath -Include "$FileFilter"

$releaseId = GetReleaseId -ReleaseName $TargetRelease

foreach ($artifact in $filesForPublish) {
   $fileName = Split-Path -Path $artifact -Leaf
   $assetUrl ="https://uploads.github.com/repos/$RepoId/releases/$releaseId/assets?name=$filename"
   Write-Host "Publishing $artifact to $assetUrl"

   $headers = @{
      "Authorization" = "Bearer $($env:GH_TOKEN)"
      "X-GitHub-Api-Version" = "2022-11-28"
      "Accept" = "application/vnd.github+json"
      "Content-Type" = "application/octet-stream"
   }

   FireAPIRequest -url $assetUrl -method POST -headers $headers -rawFile $artifact
}


