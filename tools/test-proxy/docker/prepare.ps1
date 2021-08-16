$copyDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../../../eng/common/testproxy/")
$targetDirectory = "$PsScriptRoot/dev_certificate"

if (-not (Test-Path $targetDirectory))
{
    mkdir $targetDirectory
}

# copy all files other than .yml from eng/common/scripts/testproxy into local directory dev_certificate
Get-ChildItem $copyDirectory -Exclude "*.yml" | % { Copy-Item -Path $_ -Destination "$targetDirectory/${$_.Name}" }

