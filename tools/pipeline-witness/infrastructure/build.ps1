$root = $PSScriptRoot

. $root/kusto/Merge-KustoScripts.ps1 -OutputPath "$root/artifacts/merged.kql"
if($?) {
    Write-Host "Merged KQL files"
} else {
    Write-Error "Failed to merge KQL files"
    exit 1
}

az bicep build --file "$root/bicep/resourceGroup.bicep" --outdir "$root/artifacts"
if($?) {
    Write-Host "Built Bicep files"
} else {
    Write-Error "Failed to build Bicep files"
    exit 1
}
