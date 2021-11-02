$copyDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../../../../../../../eng/common/")
$targetDirectory = "$PsScriptRoot/docker_build/common"

Copy-Item -Path $copyDirectory -Destination $targetDirectory -Force -Recurse
