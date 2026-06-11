[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$ReviewDetailsJson,
  [Parameter(Mandatory = $true)]
  [string]$StagingPath,
  [Parameter(Mandatory = $true)]
  [string]$WorkingDir,
  [Parameter(Mandatory = $true)]
  [string]$StorageBaseUrl,
  [Parameter(Mandatory = $true)]
  [string]$ApiviewGenScript,
  [string]$ParserPath = ""
)

. "${PSScriptRoot}/../common/scripts/logging.ps1"

function Get-ContainedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath,
        [Parameter(Mandatory = $true)]
        [string]$ChildPath
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath)
    $combinedPath = Join-Path -Path $rootFullPath -ChildPath $ChildPath
    $fullPath = [System.IO.Path]::GetFullPath($combinedPath)

    $normalizedRoot = $rootFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $comparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }
    $isExactMatch = $fullPath.Equals($normalizedRoot, $comparison)
    $isChildPath = $fullPath.StartsWith($normalizedRoot + [System.IO.Path]::DirectorySeparatorChar, $comparison)
    if (-not $isExactMatch -and -not $isChildPath) {
        throw "Path traversal detected. Resolved path '$fullPath' escapes root '$normalizedRoot'."
    }

    return $fullPath
}

function Get-SafeFileName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $reservedNames = @('CON','PRN','AUX','NUL',
        'COM1','COM2','COM3','COM4','COM5','COM6','COM7','COM8','COM9',
        'LPT1','LPT2','LPT3','LPT4','LPT5','LPT6','LPT7','LPT8','LPT9')

    $baseName = [System.IO.Path]::GetFileName($FileName)
    if ([string]::IsNullOrWhiteSpace($baseName)) {
        return $null
    }

    $safeName = -join ($baseName.ToCharArray() | ForEach-Object { if ($_ -cmatch '[A-Za-z0-9._-]') { $_ } else { '_' } })
    $safeName = $safeName.TrimEnd('. ')

    if ([string]::IsNullOrEmpty($safeName) -or $safeName -eq '.' -or $safeName -eq '..') {
        return $null
    }

    $nameWithoutExt = [System.IO.Path]::GetFileNameWithoutExtension($safeName).ToUpperInvariant()
    if ($reservedNames -contains $nameWithoutExt) {
        return $null
    }

    return $safeName
}

Write-Host "Review Details Json: $($ReviewDetailsJson)"
$reviews = ConvertFrom-Json $ReviewDetailsJson
Write-Host $reviews
$apiviewTokenFileCreationFailed = $false
if ($reviews -ne $null)
{
    foreach($r in $reviews)
    {
        Write-Host  "Review:$($r.ReviewID)"
        Write-Host "Revision: $($r.RevisionID)"

        $pkgWorkingDir = Join-Path -Path $WorkingDir $r.ReviewID | Join-Path -ChildPath $r.RevisionID
        $codeDir = New-Item -Path $pkgWorkingDir -ItemType Directory -Force
        $codeDirPath = [System.IO.Path]::GetFullPath($codeDir.FullName)

        $safeFileName = Get-SafeFileName -FileName $r.FileName
        if ([string]::IsNullOrWhiteSpace($safeFileName)) {
            throw "Invalid file name '$($r.FileName)' in review generation payload."
        }

        $sourceFilePath = Get-ContainedPath -RootPath $codeDirPath -ChildPath $safeFileName
        $sourcePath = $StorageBaseUrl + "/" + $r.FileID
        Write-Host "Copying $($sourcePath)"
        azcopy cp "$sourcePath" "$sourceFilePath" --recursive=true
        if ($LASTEXITCODE -ne 0) {
            throw "azcopy cp failed with exit code $LASTEXITCODE for source '$sourcePath'"
        }

        #Create staging path for review and revision ID
        $CodeFileDirectory = Join-Path -Path $StagingPath $r.ReviewID | Join-Path -ChildPath $r.RevisionID
        if (-not (Test-Path -Path $CodeFileDirectory)) {
            New-Item -Path $CodeFileDirectory -ItemType Directory
        }

        $reviewGenScriptPath = Get-ContainedPath -RootPath $PSScriptRoot -ChildPath $ApiviewGenScript
        if ($ParserPath -eq "")
        {
            &($reviewGenScriptPath) -SourcePath $sourceFilePath -OutPath $CodeFileDirectory
        }
        else
        {
            &($reviewGenScriptPath) -SourcePath $sourceFilePath -OutPath $CodeFileDirectory -ParserPath $ParserPath
        }

        if ((Get-ChildItem -Path $CodeFileDirectory -Recurse | Measure-Object).Count -eq 0)
        {
            $apiviewTokenFileCreationFailed = $true
        }
    }
    if ($apiviewTokenFileCreationFailed)
    {
        LogWarning "One or more APIView Token files were not created"
        Write-Host "##vso[task.complete result=SucceededWithIssues;]Step Completed with Issues"
    }
}
else
{
    Write-Host "Invalid Input review details Json $($ReviewDetailsJson)"
    exit 1;
}