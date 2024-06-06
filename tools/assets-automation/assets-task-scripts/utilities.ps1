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

function Get-AssetsRepoSlice {
    param(
        # the assets repo tag
        [Parameter(Mandatory=$true)]
        [string]$Tag,
        # WorkDirectory should be targeted at the .results folder
        [Parameter(Mandatory=$true)]
        [string]$WorkDirectory,
        # the target repo to clone, we will default to the assets repo, but will support others just in case
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