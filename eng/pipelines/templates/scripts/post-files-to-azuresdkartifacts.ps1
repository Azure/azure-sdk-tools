param(
   [Parameter(mandatory=$true)]
   [string] $TargetRelease,

   [Parameter(mandatory=$true)]
   [string] $BinariesDirectory,

   [Parameter(mandatory=$false)]
   [string] $FileFilter = "*.*",

   [Parameter(mandatory=$false)]
   [string] $StorageAccountName = "azuresdkartifacts",

   [Parameter(mandatory=$false)]
   [string] $Container = "public-azsdk-cli"
)

. "$PSScriptRoot/github-api-interactions.ps1"

$SearchPath = Join-Path $BinariesDirectory "*"
$filesForPublish = Get-ChildItem -Path $SearchPath -Include "$FileFilter"

#$releaseId = GetReleaseId -ReleaseName $TargetRelease
$releaseId = "azsdk_0.6.47"

Connect-AzAccount -Identity
$context = New-AzStorageContext -StorageAccountName $StorageAccountName -UseConnectedAccount

foreach ($artifact in $filesForPublish) {
   $fileName = Split-Path -Path $artifact -Leaf
   Write-Host "Publishing $artifact to $StorageAccountName/$releaseId/$fileName"

   Set-AzStorageBlobContent `
      -Context $context `
      -File $artifact `
      -Container $Container `
      -Blob "$releaseId/$fileName"
}
