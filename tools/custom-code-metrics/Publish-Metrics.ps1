#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$SnapshotPath,
    [Parameter(Mandatory)][ValidatePattern("^[a-z0-9]{3,24}$")][string]$StorageAccount,
    [Parameter(Mandatory)][string]$SubscriptionId
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
$SnapshotPath = (Resolve-Path -LiteralPath $SnapshotPath).Path
if (-not $PSCmdlet.ShouldProcess("$SubscriptionId/$StorageAccount", "Publish snapshot $SnapshotPath and update the reporting index")) {
    return
}
$previous = $env:AZURE_STORAGE_ACCESS_TOKEN
try {
    $token = & az account get-access-token --subscription $SubscriptionId --resource https://storage.azure.com/ --query accessToken --output tsv --only-show-errors
    if ($LASTEXITCODE -ne 0 -or -not $token) { throw "Could not acquire an Entra storage access token." }
    $env:AZURE_STORAGE_ACCESS_TOKEN = $token
    & node (Join-Path $PSScriptRoot "publishing.mjs") $SnapshotPath $StorageAccount
    if ($LASTEXITCODE -ne 0) { throw "Metrics publication failed. An existing index is replaced only after every referenced upload succeeds." }
}
finally {
    $env:AZURE_STORAGE_ACCESS_TOKEN = $previous
}
