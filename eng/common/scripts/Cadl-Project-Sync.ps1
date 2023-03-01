# For details see https://github.com/Azure/azure-sdk-tools/blob/main/doc/common/Cadl-Project-Scripts.md

[CmdletBinding()]
param (
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectDirectory
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot/Helpers/PSModule-Helpers.ps1
. $PSScriptRoot/Helpers/Sparse-Clone-Helpers.ps1
Install-ModuleIfNotInstalled "powershell-yaml" "0.4.1" | Import-Module

function CopySpecToProjectIfNeeded([string]$specCloneRoot, [string]$mainSpecDir, [string]$dest, [string[]]$specAdditionalSubDirectories) {
    $source = "$specCloneRoot/$mainSpecDir"
    Copy-Item -Path $source -Destination $dest -Recurse -Force
    Write-Host "Copying spec from $source to $dest"

    foreach ($additionalDir in $specAdditionalSubDirectories) {
        $source = "$specCloneRoot/$additionalDir"
        Write-Host "Copying spec from $source to $dest"
        Copy-Item -Path $source -Destination $dest -Recurse -Force
    }
}

function UpdateSparseCheckoutFile([string]$mainSpecDir, [string[]]$specAdditionalSubDirectories) {
    AddSparseCheckoutPath $mainSpecDir
    foreach ($subDir in $specAdditionalSubDirectories) {
        AddSparseCheckoutPath $subDir
    }
}

function GetGitRemoteValue([string]$repo) {
    Push-Location $ProjectDirectory
    $result = ""
    try {
        $gitRemotes = (git remote -v)
        foreach ($remote in $gitRemotes) {
            if ($remote.StartsWith("origin")) {
                if ($remote -match 'https://github.com/\S+') {
                    $result = "https://github.com/$repo.git"
                    break
                } elseif ($remote -match "git@github.com:\S+"){
                    $result = "git@github.com:$repo.git"
                    break
                } else {
                    throw "Unknown git remote format found: $remote"
                }
            }
        }
    }
    finally {
        Pop-Location
    }

    return $result
}

function GetSpecCloneDir([string]$projectName) {
    return GetSparseCloneDir $ProjectDirectory $projectName "spec"
}

$cadlConfigurationFile = Resolve-Path "$ProjectDirectory/cadl-location.yaml"
Write-Host "Reading configuration from $cadlConfigurationFile"
$configuration = Get-Content -Path $cadlConfigurationFile -Raw | ConvertFrom-Yaml

$pieces = $cadlConfigurationFile.Path.Replace("\","/").Split("/")
$projectName = $pieces[$pieces.Count - 2]

$specSubDirectory = $configuration["directory"]

if ( $configuration["repo"] -and $configuration["commit"]) {
    $specCloneDir = GetSpecCloneDir $projectName
    $gitRemoteValue = GetGitRemoteValue $configuration["repo"]

    Write-Host "Setting up sparse clone for $projectName at $specCloneDir"

    Push-Location $specCloneDir.Path
    try {
        if (!(Test-Path ".git")) {
            InitializeSparseGitClone $gitRemoteValue
            UpdateSparseCheckoutFile $specSubDirectory $configuration["additionalDirectories"]
        }
        git checkout $configuration["commit"]
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
} elseif ( $configuration["spec-root-dir"] ) {
    $specCloneDir = $configuration["spec-root-dir"]
}


$tempCadlDir = "$ProjectDirectory/TempCadlFiles"
New-Item $tempCadlDir -Type Directory -Force | Out-Null
CopySpecToProjectIfNeeded `
    -specCloneRoot $specCloneDir `
    -mainSpecDir $specSubDirectory `
    -dest $tempCadlDir `
    -specAdditionalSubDirectories $configuration["additionalDirectories"]
