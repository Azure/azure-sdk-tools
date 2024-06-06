Set-StrictMode -Version 4

function Create-If-Not-Exists {
    param(
        [string]$Path
    )

    if (!(Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }

    return $Path
}

<#
.SYNOPSIS
Retrieve a specific tag from an assets repo and store it on disk within the work directory.

.DESCRIPTION
Clones a specific tag from an assets repo (defaults to azure-sdk-assets) and stores it on disk
within the work directory under a folder with pattern:

.results <-- this should be WorkDirectory arg
    tags/
        <tagname>/
            <tagged repo contents>
        <tagname>/
            <tagged repo contents>
        <tagname>/
            <tagged repo contents>
        ...
  
Returns the location of the folder after the work is complete.

.PARAMETER Tag
The tag to retrieve from the assets repo.

.PARAMETER WorkDirectory
The path to the .results directory within which this script will operate.

.PARAMETER TargetRepo
Defaults to "Azure/azure-sdk-assets". This is the repo that will be cloned from.
#>
function Get-AssetsRepoSlice {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Tag,
        [Parameter(Mandatory=$true)]
        [string]$WorkDirectory,
        [string]$TargetRepo = "Azure/azure-sdk-assets"
    )
    $CloneUri = "https://github.com/$TargetRepo.git"
    $TagFolderName = Join-Path $WorkDirectory "tags" $Tag.Replace("/", "-")

    $TagFolder = Create-If-Not-Exists -Path $TagFolderName

    Write-Host "TagFolder is $TagFolder"

    if (Test-Path $TagFolder/.git) {
        Write-Host "TagFolder already exists, skipping clone, returning $TagFolder"
        return $TagFolder
    }
    else {
        try {
            Push-Location $TagFolder
            git clone -c core.longpaths=true --no-checkout --filter=tree:0 $CloneUri .
            git fetch origin "refs/tags/$($Tag):refs/tags/$Tag"
            git checkout $Tag
        }
        finally {
            Pop-Location
        }

        return $TagFolder
    }
}