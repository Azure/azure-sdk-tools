[CmdletBinding()]
param (
    [parameter(Mandatory = $true)]
    [string]$PackageJsonPath,

    [parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [parameter(Mandatory = $false)]
    [string]$PackageJsonFileName = "emitter-package.json"
)

$knownPackages = @(
    "@azure-tools/typespec-azure-core"
    "@azure-tools/typespec-client-generator-core"
    "@typespec/compiler"
    "@typespec/eslint-config-typespec"
    "@typespec/http"
    "@typespec/rest"
    "@typespec/versioning"
)

$packageJson = Get-Content $PackageJsonPath | ConvertFrom-Json

$devDependencies = @{}

foreach ($package in $knownPackages) {
    $pinnedVersion = $packageJson.devDependencies.$package
    if ($pinnedVersion) {
        $devDependencies[$package] = $pinnedVersion
    }
}

$emitterPackageJson = [ordered]@{
    "main" = "dist/src/index.js"
    "dependencies" = @{
        $packageJson.name = $packageJson.version
    }
}

if($devDependencies.Keys.Count -gt 0) {
    $emitterPackageJson["devDependencies"] = $devDependencies
}

New-Item $OutputDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
$OutputDirectory = Resolve-Path $OutputDirectory

$dest = Join-Path $OutputDirectory $PackageJsonFileName
Write-Host "Generating $dest"
$emitterPackageJson | ConvertTo-Json -Depth 100 | Out-File $dest
