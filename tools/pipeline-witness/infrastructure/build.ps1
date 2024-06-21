<#
.SYNOPSIS
    Validates the bicep file build.
#>

Push-Location $PSScriptRoot
try {
    ./Merge-KustoScripts.ps1 -OutputPath "./artifacts/merged.kql"
    if($?) {
        Write-Host "Merged KQL files"
    } else {
        Write-Error "Failed to merge KQL files"
        exit 1
    }

    az bicep build --file "./bicep/main.bicep" --outdir "./artifacts"
    if($?) {
        Write-Host "Built Bicep files"
    } else {
        Write-Error "Failed to build Bicep files"
        exit 1
    }
}
finally {
    Pop-Location
}
