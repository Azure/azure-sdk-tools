[CmdletBinding()]
param (
    [parameter(Mandatory = $true)]
    [string]$PackageJsonPath,

    [parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [parameter(Mandatory = $false)]
    [string]$PackageJsonFileName = "emitter-package.json"
)

$packageJson = Get-Content $PackageJsonPath | ConvertFrom-Json

$knownPackages = @(
    "@typespec/compiler"
    "@typespec/rest"
    "@typespec/http"
    "@typespec/versioning"
    "@azure-tools/typespec-client-generator-core"
    "@azure-tools/typespec-azure-core"
    "@typespec/eslint-config-typespec"
)

$devDependencies = @{}

foreach ($package in $knownPackages) {
    $pinnedVersion = $packageJson.dependencies.$package ?? $packageJson.devDependencies.$package ?? $packageJson.peerDependencies.$package;
    if ($pinnedVersion) {
        $devDependencies[$package] = $pinnedVersion;
    }
}

$emitterPackageJson = [ordered]@{
    "main" = "dist/src/index.js"
    "dependencies" = @{
        $packageJson.name = $packageJson.version
    }
    "devDependencies" = $devDependencies
}

New-Item $OutputDirectory -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
$OutputDirectory = Resolve-Path $OutputDirectory

$dest = Join-Path $OutputDirectory $PackageJsonFileName
Write-Host "Generating $dest"
$emitterPackageJson | ConvertTo-Json -Depth 100 | Out-File $dest
