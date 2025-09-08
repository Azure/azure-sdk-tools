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
        $codeDir = New-Item -Path $pkgWorkingDir -ItemType Directory
        $sourcePath = $StorageBaseUrl + "/" + $r.FileID
        Write-Host "Copying $($sourcePath)"
        azcopy cp "$sourcePath" $codeDir/$($r.FileName) --recursive=true

        #Create staging path for review and revision ID
        $CodeFileDirectory = Join-Path -Path $StagingPath $r.ReviewID | Join-Path -ChildPath $r.RevisionID
        if (-not (Test-Path -Path $CodeFileDirectory)) {
            New-Item -Path $CodeFileDirectory -ItemType Directory
        }

        $reviewGenScriptPath = Join-Path $PSScriptRoot $ApiviewGenScript
        if ($ParserPath -eq "")
        {
            &($reviewGenScriptPath) -SourcePath $codeDir/$($r.FileName) -OutPath $CodeFileDirectory
        }
        else
        {
            &($reviewGenScriptPath) -SourcePath $codeDir/$($r.FileName) -OutPath $CodeFileDirectory -ParserPath $ParserPath
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