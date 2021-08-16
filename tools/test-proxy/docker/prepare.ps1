$targetDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../../../eng/common/testproxy/")

if (-not (Test-Path "dev_certificate/"))
{
    mkdir dev_certificate
}

# copy all files other than .yml from eng/common/scripts/testproxy into local directory dev_certificate
Get-ChildItem $targetDirectory -Exclude "*.yml" | % { Copy-Item -Path $_ -Destination dev_certificate }