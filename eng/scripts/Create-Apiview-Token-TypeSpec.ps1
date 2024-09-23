[CmdletBinding()]
param (
    [ValidateNotNullOrEmpty()]
    [string] $Reviews,
    [ValidateNotNullOrEmpty()]
    [string] $WorkingDir,
    [ValidateNotNullOrEmpty()]
    [string] $OutputDir,
    [string] $GitPat = ""
)

Set-StrictMode -Version 3


function Sparse-Checkout($branchName, $packagePath)
{
    git sparse-checkout init
    git sparse-checkout set $packagePath
    git checkout $branchName
}

function Generate-Apiview-File($packagePath)
{
    Write-Host "Generating API review token file from path '$($packagePath)'"
    Push-Location $packagePath
    npm install
    npm list
    npx tsp compile . --emit=@azure-tools/typespec-apiview --option "@azure-tools/typespec-apiview.emitter-output-dir={project-root}/output/apiview.json"
    Pop-Location
}

function Stage-Apiview-File($packagePath, $reviewId, $revisionId)
{
    $tokenFilePath = Join-Path $packagePath "output"
    $stagingReviewPath = Join-Path $OutputDir $reviewId
    $stagingPath = Join-Path $stagingReviewPath $revisionId
    Write-Host "Copying APIView file from '$($tokenFilePath)' to '$($stagingPath)'"
    New-Item $stagingPath -ItemType Directory -Force
    Get-ChildItem -Path $tokenFilePath -Filter *.json -recurse | Copy-Item -Destination $stagingPath
    Write-Host "Files in Staging path $($stagingPath)"
    ls $stagingPath
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
        # initialize git clone if current review is generated from different repo than previous one
        if ($GitRepoName -ne $prevRepo)
        {
            $gitUrl = "https://github.com"
            if ($GitPat)
            {
                $gitUrl = "https://$GitPat@github.com"
            }

            $gitUrl = "$gitUrl/$GitRepoName.git"
            if (Test-Path $repoDirectory)
            {
                Write-Host "Destination path '$($repoDirectory)' already exists in working directory. Deleting '$($repoDirectory)'"
                Remove-Item $repoDirectory -Force -Recurse
            }
            git clone --no-checkout --filter=tree:0 $gitUrl
            if ($LASTEXITCODE) { exit $LASTEXITCODE }
            $prevRepo = $GitRepoName
        }

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