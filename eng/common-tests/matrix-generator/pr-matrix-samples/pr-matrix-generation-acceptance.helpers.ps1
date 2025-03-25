$PackagePropertiesPath = Join-Path "eng" "common" "scripts" "Save-Package-Properties.ps1"

Function Copy-EngCommon {
    param (
        [string]$OutputRepo,
        [string]$InputRepo
    )
    Set-StrictMode -Version 3.0
    $ErrorActionPreference = "Stop"

    $InputRepo = Resolve-Path $InputRepo
    $OutputRepo = Resolve-Path $OutputRepo

    $inputFiles = Get-ChildItem -Path (Join-Path $InputRepo "eng/common") -Recurse -File

    foreach ($file in $inputFiles) {
        $relativePath = $file.FullName.Substring($InputRepo.Length + 1)
        $outputPath = Join-Path $OutputRepo $relativePath

        if (-not (Test-Path $outputPath) -or
            (Compare-Object (Get-Content $file.FullName) (Get-Content $outputPath))) {

            $outputDirectory = Split-Path -Parent $outputPath
            if (-not (Test-Path $outputDirectory)) {
                New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
            }

            Copy-Item -Path $file.FullName -Destination $outputPath -Force
            Write-Host "Copied $relativePath"
        }
    }
}

Function Get-Repo {
    param (
        [string]$Repo,
        [string]$Reference
    )

    $tempDir = [System.IO.Path]::GetTempPath()
    $repoPath = Join-Path $tempDir $Repo.Replace("/", "-")
    $thisRepo = Resolve-Path (Join-Path $PSScriptRoot ".." ".." ".." "..")

    if (!(Test-Path $repoPath)) {
        New-Item -ItemType Directory -Path $repoPath | Out-Null

        try {
            Write-Host "Push-Location $repoPath"
            Push-Location $repoPath
            Write-Host "git clone --no-checkout `"https://github.com/$Repo.git`" ."
            git clone --no-checkout "https://github.com/$Repo.git" .
            Write-Host "git fetch origin $Reference"
            git fetch origin $Reference
            Write-Host "git checkout $Reference"
            git checkout $Reference
        }
        finally {
            Pop-Location | Out-Null
        }
    }
    Copy-EngCommon -InputRepo $thisRepo -OutputRepo $repoPath

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
        Write-Host "Push-Location $Repo"
        Push-Location $Repo
        Write-Host "&`"$PackagePropertiesPath`" -outDirectory `"$uniqueTempDir`" -prDiff `"$prDiffFile`""
        Write-Host "Package Diff:"
        Write-Host (Get-Content -Raw $prDiffFile)
        &"$PackagePropertiesPath" -outDirectory "$uniqueTempDir" -prDiff "$prDiffFile"
        Remove-Item $prDiffFile -Force
    }
    finally {
        Pop-Location | Out-Null
    }

    return $uniqueTempDir
}

Function OrderArtifactDetails {
    param (
        [PSCustomObject]$PackagePropObject
    )
    $sortedDetails = [ordered]@{}

    # Sort the existing hashtable by key name, then rebuild it
    $PackagePropObject.ArtifactDetails.PSObject.Properties | Sort-Object Name | ForEach-Object {
        $sortedDetails[$_.Name] = $_.Value
    }

    $PackagePropObject.ArtifactDetails = $sortedDetails
}

Function Compare-PackageResults {
    param(
        [PSCustomObject[]]$Actual,
        [PSCustomObject[]]$Expected
    )
    # we would LOVE to be able to run a simple comparison here, but the artifact details are not in consistent order (nor should HAVE to be)
    # so we need to do a little more work to do a valid comparison
    if ($Actual.Count -ne $Expected.Count) {
        $packages = ($Actual | ForEach-Object { $_.ArtifactName }) -join ", "
        $exception = "Expected $($Expected.Count) results, but got $($Actual.Count). Visible Packages: [ $packages ]"
        throw $exception
    }

    # sort the results by name so we can compare them easily
    $sortedActual = $Actual | Sort-Object -Property Name
    $sortedExpected = $Expected | Sort-Object -Property Name

    for ($i = 0; $i -lt $sortedActual.Count; $i++) {
        OrderArtifactDetails -PackagePropObject $sortedActual[$i]
        OrderArtifactDetails -PackagePropObject $sortedExpected[$i]
    }

    $sortedActual | ConvertTo-Json -Depth 100 | Should -Be ($sortedExpected | ConvertTo-Json -Depth 100)
}