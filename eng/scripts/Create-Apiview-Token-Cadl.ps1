[CmdletBinding()]
param (
    [ValidateNotNullOrEmpty()]
    [string] $Reviews,
    [ValidateNotNullOrEmpty()]
    [string] $WorkingDir,
    [ValidateNotNullOrEmpty()]
    [string] $OutputDir
)

Set-StrictMode -Version 3
$sparseCheckoutFile = ".git/info/sparse-checkout"


function Sparse-Checkout($branchName, $packagePath)
{
    if (Test-Path $sparseCheckoutFile)
    {
        Remove-Item $sparseCheckoutFile -Force
    }    
    git sparse-checkout init --cone
    git sparse-checkout set $packagePath
    git checkout $BranchName
}

function Generate-Apiview-File($packagePath)
{
    Write-Host "Generating API review token file from path '$($packagePath)'"
    Push-Location $packagePath
    try
    {
        npm install
        cadl compile . --emit=@azure-tools/cadl-apiview
    }
    finally
    {
        Pop-Location
    }
}

function Stage-Apiview-File($packagePath, $reviewId, $revisionId)
{
    $tokenFilePath = Join-Path $packagePath "cadl-output"
    $stagingReviewPath = Join-Path $OutputDir $reviewId
    $stagingPath = Join-Path $stagingReviewPath $revisionId
    Write-Host "Copying APIView file from '$($tokenFilePath)' to '$($stagingPath)'"
    New-Item $stagingPath -ItemType Directory -Force
    Copy-Item -Destination $stagingPath -Path "$tokenFilePath/*"
}


Write-Host "Review Details Json: $($Reviews)"
$revs = ConvertFrom-Json $Reviews
if ($revs)
{
    $prevRepo = ""
    foreach ($r in $revs)
    {
        $reviewId = $r.ReviewID
        $revisionId = $r.RevisionID
        $packagePath = $r.FileName
        $GitRepoName = $r.SourceRepoName
        $branchName = $r.SourceBranchName

        if ($packagePath.StartsWith("/"))
        {
            $packagePath = $packagePath.Substring(1)
        }
        Write-Host "Generating API review for Review ID: '$($reviewId), Revision ID: '$($revisionId)"
        Write-Host "URL to Repo: '$($GitRepoName), Branch name: '$($branchName), Package Path: '$($packagePath)"

        $repoDirectory = Split-Path $GitRepoName -leaf
        if (Test-Path $repoDirectory)
        {
            Write-Host "Destination path '$($repoDirectory)' already exists in working directory and is not an empty directory."
            exit 1
        }
        # initialize git clone if current review is generated from different repo than previous one
        if ($GitRepoName -ne $prevRepo)
        {
            git clone --no-checkout --filter=tree:0 "https://github.com/$GitRepoName"
            if ($LASTEXITCODE) { exit $LASTEXITCODE }
            $prevRepo = $GitRepoName
        }

        $repoDirectory = Split-Path $GitRepoName -leaf
        Push-Location $repoDirectory
        try
        {
            Write-Host "GitHub Repo Name: '$($repoDirectory)"
            # Sparse checkout package root path
            Sparse-Checkout -branchName $branchName -packagePath $packagePath
            # Generate API code file
            Generate-Apiview-File -packagePath $packagePath
            #Copy generated code file to stagin location
            Stage-Apiview-File -packagePath $packagePath -reviewId $reviewId -revisionId $revisionId
        }
        finally
        {
            Pop-Location
        }
    }

    Write-Host "Generated and copied Api review token file to output directory"
}