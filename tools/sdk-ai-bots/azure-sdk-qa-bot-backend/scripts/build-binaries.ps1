<#
.SYNOPSIS
Cross-compiles the Go backend into standalone binaries for multiple platforms.

.DESCRIPTION
Produces platform-specific binary archives (tar.gz for Linux/macOS, zip for Windows)
suitable for attaching to a GitHub Release.

.PARAMETER OutputDirectory
Directory to place the final archives. Defaults to '<repo-root>/artifacts/binaries'.

.PARAMETER Version
Version string to embed in archive names. If not specified, reads from version.go.

#>
param(
    [Parameter(mandatory=$false)]
    [string] $OutputDirectory,

    [Parameter(mandatory=$false)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

$BackendRoot = Split-Path -Parent $PSScriptRoot
$BinaryName = "azure-sdk-qa-bot-backend"

# Resolve version from version.go if not provided
if (-not $Version) {
    $versionFile = Join-Path $BackendRoot "version.go"
    $versionContent = Get-Content $versionFile -Raw
    if ($versionContent -match 'moduleVersion\s*=\s*"([^"]+)"') {
        $Version = $Matches[1]
    } else {
        Write-Error "Could not determine version from version.go"
        exit 1
    }
}

# Default output directory
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $BackendRoot "artifacts" "binaries"
}

if (!(Test-Path $OutputDirectory)) {
    New-Item -Force -Path $OutputDirectory -ItemType Directory | Out-Null
}

Write-Host "Building version: $Version"
Write-Host "Output directory: $OutputDirectory"

# Define target platforms
$targets = @(
    @{ GOOS = "linux";   GOARCH = "amd64"; Rid = "linux-x64";    Ext = "" },
    @{ GOOS = "linux";   GOARCH = "arm64"; Rid = "linux-arm64";   Ext = "" },
    @{ GOOS = "darwin";  GOARCH = "amd64"; Rid = "osx-x64";      Ext = "" },
    @{ GOOS = "darwin";  GOARCH = "arm64"; Rid = "osx-arm64";     Ext = "" },
    @{ GOOS = "windows"; GOARCH = "amd64"; Rid = "win-x64";      Ext = ".exe" }
)

$ldflags = "-s -w -X main.moduleVersion=$Version"

foreach ($target in $targets) {
    $rid = $target.Rid
    $goos = $target.GOOS
    $goarch = $target.GOARCH
    $ext = $target.Ext
    $outputBinary = "${BinaryName}${ext}"

    Write-Host "`n=== Building for $rid (GOOS=$goos GOARCH=$goarch) ==="

    $buildDir = Join-Path $OutputDirectory "build-$rid"
    if (Test-Path $buildDir) {
        Remove-Item -Recurse -Force $buildDir
    }
    New-Item -Force -Path $buildDir -ItemType Directory | Out-Null

    $env:GOOS = $goos
    $env:GOARCH = $goarch
    $env:CGO_ENABLED = "0"

    $binaryPath = Join-Path $buildDir $outputBinary

    Push-Location $BackendRoot
    try {
        go build -ldflags $ldflags -o $binaryPath .
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed for $rid with exit code $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    } finally {
        Pop-Location
    }

    # Package the binary
    $archiveName = "${BinaryName}-standalone-${rid}"

    if ($goos -eq "windows") {
        $archivePath = Join-Path $OutputDirectory "${archiveName}.zip"
        if (Test-Path $archivePath) { Remove-Item $archivePath }
        Compress-Archive -Path $binaryPath -DestinationPath $archivePath
        Write-Host "Created: $archivePath"
    } else {
        $archivePath = Join-Path $OutputDirectory "${archiveName}.tar.gz"
        if (Test-Path $archivePath) { Remove-Item $archivePath }

        Push-Location $buildDir
        try {
            if ($IsLinux -or $IsMacOS) {
                bash -c "chmod +x '$outputBinary'"
                tar -czf $archivePath $outputBinary
            } else {
                # Building on Windows for Linux/macOS target - use tar if available
                tar -czf $archivePath $outputBinary
            }
        } finally {
            Pop-Location
        }
        Write-Host "Created: $archivePath"
    }

    # Clean up build directory
    Remove-Item -Recurse -Force $buildDir
}

# Reset environment variables
Remove-Item Env:GOOS -ErrorAction SilentlyContinue
Remove-Item Env:GOARCH -ErrorAction SilentlyContinue
Remove-Item Env:CGO_ENABLED -ErrorAction SilentlyContinue

Write-Host "`n=== Build complete ==="
Write-Host "Archives:"
Get-ChildItem -Path $OutputDirectory | Where-Object { $_.Name -match '\.(tar\.gz|zip)$' } | ForEach-Object {
    Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)"
}
