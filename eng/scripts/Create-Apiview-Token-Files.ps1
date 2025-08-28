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

$ErrorActionPreference = "Stop"

Write-Host "Review Details Json: $($ReviewDetailsJson)"
$reviews = ConvertFrom-Json $ReviewDetailsJson
Write-Host $reviews
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
        $CodeFilePath = Join-Path -Path $StagingPath $r.ReviewID | Join-Path -ChildPath $r.RevisionID
        if (-not (Test-Path -Path $CodeFilePath)) {
            New-Item -Path $CodeFilePath -ItemType Directory
        }

        $reviewGenScriptPath = Join-Path $PSScriptRoot $ApiviewGenScript
        
        try {
            if ($ParserPath -eq "")
            {
                &($reviewGenScriptPath) -SourcePath $codeDir/$($r.FileName) -OutPath $CodeFilePath
                if ($LASTEXITCODE -ne 0) {
                    throw "APIView token generation script failed with exit code: $LASTEXITCODE"
                }
            }
            else
            {
                &($reviewGenScriptPath) -SourcePath $codeDir/$($r.FileName) -OutPath $CodeFilePath -ParserPath $ParserPath
                if ($LASTEXITCODE -ne 0) {
                    throw "APIView token generation script failed with exit code: $LASTEXITCODE"
                }
            }
        } catch {
            Write-Error "APIView token generation failed for Review ID: $($r.ReviewID), Revision ID: $($r.RevisionID). Error: $_"
            exit 1
        }
    }
}
else
{
    Write-Host "Invalid Input review details Json $($ReviewDetailsJson)"
    exit 1;
}