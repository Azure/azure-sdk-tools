$sparseCheckoutFile = ".git/info/sparse-checkout"

function GetProjectRootPath([string]$projectDirectory) {
    Push-Location $projectDirectory
    try {
        return git rev-parse --show-toplevel
    }
    finally {
        Pop-Location
    }
}

function GetSparseCloneDir([string]$projectDirectory, [string]$projectName, [string]$repoName) {
    $root = GetProjectRootPath $projectDirectory

    $sparseSpecCloneDir = "$root/../sparse-spec/$repoName/$projectName"
    New-Item $sparseSpecCloneDir -Type Directory -Force | Out-Null
    $createResult = Resolve-Path $sparseSpecCloneDir
    return $createResult
}

function InitializeSparseGitClone([string]$repo) {
    git clone --no-checkout --filter=tree:0 $repo .
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    git sparse-checkout init
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    Remove-Item $sparseCheckoutFile -Force
}

function AddSparseCheckoutPath([string]$subDirectory) {
    if (!(Test-Path $sparseCheckoutFile) -or !((Get-Content $sparseCheckoutFile).Contains($subDirectory))) {
        Write-Output $subDirectory >> .git/info/sparse-checkout
    }
}
