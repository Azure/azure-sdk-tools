# Updates the Ref in all Yml files in the Repository
param (
    [Parameter(Mandatory=$True)]
    [String]$RepoRoot,
    [Parameter(Mandatory=$True)]
    [String]$Tag,
    [Parameter(Mandatory=$True)]
    [String]$ToolRepo
)

$ymlFiles = Get-ChildItem -Path $RepoRoot -File -Include *.yml -Recurse

foreach ($file in $ymlFiles)
{
    $regex = [Regex]"ref: refs/tags/${ToolRepo}_[\d\.]+"
    Write-Host "Operating on: " $file.FullName
    $ymlContent = Get-Content $file.FullName -Raw

    $updated = $false

    if ($ymlContent -match $regex)
    {
        $ymlContent = $regex.Replace($ymlContent, "ref: refs/tags/$Tag", 1)
        $updated = $true
    }

    if ($updated) {
        Write-Host "Updated " $file.FullName
        $ymlContent | Set-Content -Path $file.FullName -Force -NoNewline
    }
}