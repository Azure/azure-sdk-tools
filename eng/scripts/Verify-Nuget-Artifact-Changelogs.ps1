param(
  [string]$ArtifactsPath,
  [bool]$ForRelease
)

Write-Host "Verifying changelogs for artifacts in path: $ArtifactsPath"

# Recursively search for all .nupkg files
$artifacts = Get-ChildItem -Path $ArtifactsPath -Filter *.nupkg -Recurse

if ($artifacts.Count -eq 0) {
    Write-Host "No .nupkg files found in $ArtifactsPath"
    exit 0
}

Write-Host "Found $($artifacts.Count) package(s) to verify"

$errorCount = 0

foreach ($artifact in $artifacts) {
    Write-Host "Verifying changelog for artifact: $($artifact.Name)"

    # Create temporary directory for extraction
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "changelog-verify-$([System.Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    try {
        # Extract the .nupkg file (it's just a zip file)
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($artifact.FullName, $tempDir)

        # Find and read the nuspec file to get package name and version
        $nuspecPath = Get-ChildItem -Path $tempDir -Filter "*.nuspec" | Select-Object -First 1

        if (-not $nuspecPath) {
            Write-Error "No .nuspec file found in package: $($artifact.Name)"
            $errorCount++
            continue
        }

        Write-Host "Reading nuspec file: $($nuspecPath.Name)"
        [xml]$nuspecContent = Get-Content $nuspecPath.FullName

        $packageName = $nuspecContent.package.metadata.id
        $packageVersion = $nuspecContent.package.metadata.version

        if (-not $packageName -or -not $packageVersion) {
            Write-Error "Could not extract package name or version from nuspec file in: $($artifact.Name)"
            $errorCount++
            continue
        }

        Write-Host "Processing changelog for Package: $packageName, Version: $packageVersion"

        # Look for CHANGELOG.md in the extracted contents
        $changelogPath = Get-ChildItem -Path $tempDir -Name "CHANGELOG.md" -Recurse | Select-Object -First 1

        if ($changelogPath) {
            $fullChangelogPath = Join-Path $tempDir $changelogPath

            $verifyScript = Join-Path $PSScriptRoot "..\common\scripts\Verify-ChangeLog.ps1"
            Write-Host "Calling: $verifyScript -ChangeLogLocation '$fullChangelogPath' -VersionString '$packageVersion' -ForRelease $ForRelease"

            $result = & $verifyScript -ChangeLogLocation $fullChangelogPath -VersionString $packageVersion -ForRelease $ForRelease

            if ($LASTEXITCODE -ne 0) {
                Write-Error "Changelog verification failed for $($artifact.Name)"
                $errorCount++
            } else {
                Write-Host "Changelog verification passed for $($artifact.Name)" -ForegroundColor Green
            }
        } else {
            Write-Warning "No CHANGELOG.md found in package: $($artifact.Name)"
            $errorCount++
        }
    }
    catch {
        Write-Error "Failed to process package $($artifact.Name): $_"
        $errorCount++
    }
    finally {
        # Clean up temporary directory
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force
        }
    }
}

Write-Host "Changelog verification completed. $errorCount error(s) found."

if ($errorCount -gt 0) {
    exit 1
}

exit 0