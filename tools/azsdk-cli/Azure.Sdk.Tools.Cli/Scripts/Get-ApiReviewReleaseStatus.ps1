[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$Language,
  [Parameter(Mandatory = $true)]
  [string]$PackageName,
  [Parameter(Mandatory = $true)]
  [string]$PackageVersion,
  [string]$ApiHash = "",
  [string]$APIViewUri = "https://apiview.dev/AutoReview/GetReviewStatus"
)

$ErrorActionPreference = 'Stop'
if ($PSStyle) {
  $PSStyle.OutputRendering = 'PlainText'
}

$helperPath = Join-Path -Path $PSScriptRoot -ChildPath "Helpers/ApiView-Helpers.ps1"
. $helperPath

$apiStatus = [PSCustomObject]@{
  IsApproved = $false
  Details = ''
}
$packageNameStatus = [PSCustomObject]@{
  IsApproved = $false
  Details = ''
}

Check-ApiReviewStatus $PackageName $PackageVersion $Language $APIViewUri '' $apiStatus $packageNameStatus $ApiHash

if ($apiStatus.IsApproved) {
  exit 0
}

exit 1