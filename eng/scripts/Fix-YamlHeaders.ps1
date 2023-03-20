param (
  [Parameter(Mandatory=$true)][string]$Path
)

$scanLocation = Get-Item -Path $Path

if ($scanLocation.PSIsContainer -ne $true)
{
    throw "Path is not a directory."
}

Write-Host "Scanning path: $scanLocation"
$yamlFiles = Get-ChildItem -Path $Path -Filter "ci*.yml" -Recurse

Write-Host "Found $($yamlFiles.Length) ci.*.yml files under: $scanLocation"

foreach ($yamlFile in $yamlFiles)
{
    Write-Host "Processing: $yamlFile"

    $lines = Get-Content -Path $yamlFile

    $linesWithoutHeader = @()

    $linesWithoutHeader += "# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file."
    
    $skipComments = $true
    foreach ($line in $lines)
    {
        if ($line -like "#*" -and $skipComments)
        {
            Write-Host "Skipping: $line"
        }
        else {
            Write-Host "Adding: $line"
            $skipComments = $false
            $linesWithoutHeader += $line
        }
    }

    Set-Content -Path $yamlFile -Value $linesWithoutHeader
}