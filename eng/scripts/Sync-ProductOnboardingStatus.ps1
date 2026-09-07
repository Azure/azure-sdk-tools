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
   [string] $Submitter,
   
   [Parameter(Mandatory=$false)]
   [bool] $IsTest = $false
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
  "`n`n" `
  "`n`tIsTest:            '$IsTest'" `
  "`n`n"

$azsdk = "azsdk"
if ($env:AZSDK) {
  $azsdk = $env:AZSDK
}

$originalEnv = Get-ChildItem Env:

try {
  $env:AZSDKTOOLS_AGENT_TESTING = 'false'
  if ($IsTest) {
    $env:AZSDKTOOLS_AGENT_TESTING = 'true'
  }

  & "$azsdk" release-plan onboard-product --debug `
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
} finally {
  $envDiff = Compare-Object -PassThru $originalEnv (Get-ChildItem Env:) -Property { '[{0}, {1}]' -f $_.Key, $_.Value }

  foreach ($item in $envDiff | Where-Object SideIndicator -eq '=>') {
    Remove-Item "Env:$($item.Key)" -ErrorAction SilentlyContinue
  }

  foreach ($item in $envDiff | Where-Object SideIndicator -eq '<=') {
    Set-Item "Env:$($item.Key)" $item.Value
  }
}
