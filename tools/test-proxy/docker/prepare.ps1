<#
.SYNOPSIS
Copies local test-proxy code into the docker build context.

.DESCRIPTION
Copies code from tools/test-proxy/Azure.Sdk.Tools.TestProxy/ into the local docker directory. Taking a copy of the source allows us to pass the source into
the docker build without including the entire sdk-tools repo as build context.

.PARAMETER qemu
Flag. Used to enable the installation of unix QEMU packages and subsequent docker reset.
#>
param(
    [Parameter(mandatory=$false)]
    [switch]$qemu = $false
)

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

# install qemu if requested
if($qemu){
    apt-get install qemu binfmt-support qemu-user-static
    docker run --rm --privileged multiarch/qemu-user-static --reset -p yes
}
