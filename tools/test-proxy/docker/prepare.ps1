$copyDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../../../eng/common/testproxy/")
$codeDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../Azure.Sdk.Tools.TestProxy/")
$targetDirectory = "$PsScriptRoot/docker_build"

if (-not (Test-Path $targetDirectory))
{
    mkdir $targetDirectory
}

# copy all files other than .yml from eng/common/scripts/testproxy into local directory dev_certificate
Get-ChildItem $copyDirectory -Exclude "*.yml" | % { Copy-Item -Path $_ -Destination "$targetDirectory/${$_.Name}" }

# get a local copy of the source
Copy-Item -Path $codeDirectory -Destination $targetDirectory -Force -Recurse
