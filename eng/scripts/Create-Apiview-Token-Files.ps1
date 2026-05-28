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

        $safeFileName = [System.IO.Path]::GetFileName($r.FileName)
        if ([string]::IsNullOrWhiteSpace($safeFileName) -or $safeFileName -eq "." -or $safeFileName -eq "..") {
            throw "Invalid file name '$($r.FileName)' in review generation payload."
        }

        $sourceFilePath = Get-ContainedPath -RootPath $codeDirPath -ChildPath $safeFileName
        $sourcePath = $StorageBaseUrl + "/" + $r.FileID
        Write-Host "Copying $($sourcePath)"
        azcopy cp "$sourcePath" "$sourceFilePath" --recursive=true

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