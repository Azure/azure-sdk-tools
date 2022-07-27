[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$Reviews,
  [Parameter(Mandatory = $true)]
  [string]$StagingPath,
  [Parameter(Mandatory = $true)]
  [string]$WorkingDir,
  [Parameter(Mandatory = $true)]
  [string]$StorageBaseUrl,
  [Parameter(Mandatory = $true)]
  [string]$ContainerSas,
  [Parameter(Mandatory = $true)]
  [string]$ApiviewGenScript
)


Write-Host "Review Details Json: $($Reviews)"
$reviews = ConvertFrom-Json $Reviews
if ($reviews -ne $null)
{
    foreach($r in $reviews)
    {
        $codeDir = New-Item -Path $WorkingDir/$($r.ReviewID)/$($r.RevisionID) -ItemType Directory
        $sourcePath = $StorageBaseUrl + "/" + $r.FileID + $ContainerSas
        Write-Host "Copying $($sourcePath)"
        azcopy cp "$sourcePath" $codeDir/$($r.FileName) --recursive=true

        #Create staging path for review and revision ID
        $CodeFilePath = $StagingPath/$($r.ReviewID)/$($r.RevisionID)
        if (-not (Test-Path -Path $CodeFilePath)) {
            New-Item -Path $StagingPath/$($r.ReviewID)/$($r.RevisionID) -ItemType Directory
        }

        &($ApiviewGenScript) -SourcePath $codeDir/$($r.FileName) -OutPath $CodeFilePath
    }
}
else
{
    Write-Host "Invalid Input review details Json $($Reviews)"
    exit 1;
}