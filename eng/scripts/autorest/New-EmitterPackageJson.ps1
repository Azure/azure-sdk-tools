Write-Warning "This script is deprecated. Please use the New-EmitterPackageJson function from the 'eng/common/scripts/typespec/New-EmitterPackageJson.ps1' file."
. (Join-Path $PSScriptRoot "..\..\common\scripts\typespec\New-EmitterPackageJson.ps1") @args
