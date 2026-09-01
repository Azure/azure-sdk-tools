[CmdletBinding()]
param(
   [Parameter(Mandatory=$true)]
   [string] $ProductID,

   [Parameter(Mandatory=$true)]
   [string] $ProductName,

   [Parameter(Mandatory=$true)]
   [string] $ProductType,

   [Parameter(Mandatory=$true)]
   [string] $ProductLifecycle,

   [Parameter(Mandatory=$true)]
   [string] $ServiceID,

   [Parameter(Mandatory=$true)]
   [string] $ServiceName,

   [Parameter(Mandatory=$true)]
   [bool] $NeedsSDK,

   [Parameter(Mandatory=$true)]
   [string] $DataPlane,

   [Parameter(Mandatory=$true)]
   [string] $MgmtPlane,

   [Parameter(Mandatory=$true)]
   [string] $Submitter
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
  $PSNativeCommandUseErrorActionPreference = $true
}

Write-Host "Syncing product onboarding status:" `
  "`n`tProduct ID:        '$ProductID'" `
  "`n`tProduct Name:      '$ProductName'" `
  "`n`tProduct Type:      '$ProductType'" `
  "`n`tProduct Lifecycle: '$ProductLifecycle'" `
  "`n`n" `
  "`n`tService ID:        '$ServiceID'" `
  "`n`tService Name:      '$ServiceName'" `
  "`n`n" `
  "`n`tNeeds SDK:         '$NeedsSDK'" `
  "`n`tData Plane:        '$DataPlane'" `
  "`n`tManagement Plane:  '$MgmtPlane'" `
  "`n`n" `
  "`n`tSubmitter:         '$Submitter'" `
  "`n`n"

$azsdk = "azsdk"
if ($env:AZSDK) {
  $azsdk = $env:AZSDK
}

& "$azsdk" product-onboarding sync --debug `
  --product-id "$ProductID" `
  --product-name "$ProductName" `
  --product-type "$ProductType" `
  --product-lifecycle "$ProductLifecycle" `
  --service-id "$ServiceID" `
  --service-name "$ServiceName" `
  --needs-sdk $NeedsSDK `
  --data-plane "$DataPlane" `
  --management-plane "$MgmtPlane" `
  --submitter "$Submitter"
