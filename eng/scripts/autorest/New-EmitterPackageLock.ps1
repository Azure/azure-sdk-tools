Write-Warning "This script is deprecated. Please use the New-EmitterPackageLock function from the 'eng/common/scripts/typespec/New-EmitterPackageLock.ps1' file."
. (Join-Path $PSScriptRoot "..\..\common\scripts\typespec\New-EmitterPackageLock.ps1") @args
