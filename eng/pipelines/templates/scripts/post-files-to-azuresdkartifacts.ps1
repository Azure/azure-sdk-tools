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

$SearchPath = Join-Path $BinariesDirectory "*"
$filesForPublish = Get-ChildItem -Path $SearchPath -Include "$FileFilter"

$context = New-AzStorageContext -StorageAccountName "$StorageAccountName" -SasToken "$env:AZURESDKARTIFACTS_SAS_TOKEN"

foreach ($artifact in $filesForPublish) {
   $fileName = Split-Path -Path $artifact -Leaf
   Write-Host "Publishing $artifact to $StorageAccountName/$TargetRelease/$fileName"

   Set-AzStorageBlobContent `
      -Force `
      -Context $context `
      -File $artifact `
      -Container $Container `
      -Blob "$TargetRelease/$fileName"
}
