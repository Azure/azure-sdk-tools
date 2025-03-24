$PackagePropertiesPath = Join-Path "eng" "common" "scripts" "Save-Package-Properties.ps1"

Function Get-Repo {
    param (
        [string]$Repo,
        [string]$Reference
    )

    $tempDir = [System.IO.Path]::GetTempPath()
    $repoPath = Join-Path $tempDir $Repo.Replace("/", "-")

    if (!(Test-Path $repoPath)) {
        New-Item -ItemType Directory -Path $repoPath | Out-Null

        try {
            Push-Location $repoPath
            git clone --no-checkout "https://github.com/$Repo.git" .
            git fetch origin $Reference
            git checkout $Reference
        }
        finally {
            Pop-Location | Out-Null
        }
    }

    return $repoPath
}

Function Invoke-PackageProps {
    param (
        [PSCustomObject]$InputDiff,
        [string]$Repo
    )

    $env:GITHUB_ACTIONS="true"
    $uniqueTempDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ([System.IO.Path]::GetRandomFileName())
    New-Item -Path $uniqueTempDir -ItemType Directory | Out-Null

    $prDiffFile = Join-Path $uniqueTempDir "pr-diff.json"
    $InputDiff | ConvertTo-Json -Depth 100 | Set-Content -Path $prDiffFile -Force

    try {
        Push-Location $Repo
        &"$PackagePropertiesPath" -outDirectory "$uniqueTempDir" -prDiff "$prDiffFile"
    }
    finally {
        Pop-Location | Out-Null
    }

    return $uniqueTempDir
}