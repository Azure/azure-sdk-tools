$copyDirectory = Resolve-Path (Join-Path -Path $PSScriptRoot -ChildPath "../../eng/common/")

Copy-Item -Path $copyDirectory -Destination $PSScriptRoot -Force -Recurse
