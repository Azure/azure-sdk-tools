param(
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistry,

    [Parameter(Mandatory=$true)]
    [string]$Repository,

    [Parameter(Mandatory=$false)]
    [array]$ExcludeTags = @(),

    [Parameter(Mandatory=$false)]
    [int]$CutoffDays = 365
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($CutoffDays -gt 0) {
    $CutoffDays *= -1
}

$ExcludeTags += 'latest'
$cutoffDate = (Get-Date).AddDays($CutoffDays)

$manifests = az acr manifest list-metadata -r $ContainerRegistry -n $Repository -o json | ConvertFrom-Json -AsHashtable
$toDelete = $manifests | Where-Object { (Get-Date $_.lastUpdateTime) -lt $cutoffDate }

Write-Host "Deleting $($toDelete.Count) tags older than $CutoffDays days from $Repository in $ContainerRegistry"

foreach ($manifest in $toDelete) {
  foreach ($tag in $manifest.tags) {
    if ($tag -in $ExcludeTags) {
      Write-Host "Skipping excluded tag $tag"
      continue
    }
    Write-Host "az acr repository delete --name $ContainerRegistry --image "${Repository}:${tag}" --yes"
    az acr repository delete --name $ContainerRegistry --image "${Repository}:${tag}" --yes
  }
}
