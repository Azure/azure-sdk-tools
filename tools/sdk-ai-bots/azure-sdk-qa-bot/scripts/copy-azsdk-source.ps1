#!/usr/bin/env pwsh

param(
    [switch]$KeepExisting = $false
)

$azsdk_cli_source = "../../azsdk-cli"
$azsdk_cli_target = "./azsdk-cli"
if (Test-Path $azsdk_cli_source) {
    Write-Host "Found azsdk-cli at $azsdk_cli_source, copying to current directory..."
    
    # Remove existing azsdk-cli in current directory if it exists
    if ((Test-Path $azsdk_cli_target) -and (-not $KeepExisting)) {
        Write-Host "Removing existing azsdk-cli in current directory..."
        Remove-Item $azsdk_cli_target -Recurse -Force
    }
    
    # Copy azsdk-cli to current directory
    if (((Test-Path $azsdk_cli_target) -and (-not $KeepExisting)) -or (-not (Test-Path $azsdk_cli_target))) {
        Copy-Item $azsdk_cli_source $azsdk_cli_target -Recurse -Force
        Write-Host "Successfully copied azsdk-cli to current directory."
    }
} else {
    Write-Warning "azsdk-cli not found at $azsdk_cli_source"
    Write-Host "Expected path: $(Resolve-Path $azsdk_cli_source -ErrorAction SilentlyContinue)"
}
