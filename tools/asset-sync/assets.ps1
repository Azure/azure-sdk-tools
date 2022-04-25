Set-StrictMode -Version 3

# See https://stackoverflow.com/a/14440066 for a succinct explanation. We will be using
# this methodology for any global variables that can be considered constants
$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." "..")
$ASSETS_STORE = (Join-Path $REPO_ROOT ".assets")

. (Join-Path $REPO_ROOT "eng" "common" "scripts" "common.ps1")

<#
.SYNOPSIS
Checks the contents of a directory, then returns a tuple of booleans @("assetsJsonPresent", "isRootFolder").

.DESCRIPTION
Evaluates a directory by checking its contents. First value of the tuple is whether or not a "assets.json" file 
is present A "root" directory is one where a assets.json is present OR where we are as far up the file tree as 
we can possibly ascend. 

.PARAMETER TargetPath
The path we will evaulate to detect either a root directory or a .git folder.
#>
Function Evaluate-Target-Dir {
    param (
        [Parameter(Mandatory = $true)]
        [string] $TargetPath
    )

    $isFile = Test-Path $TargetPath -PathType Leaf
    $foundConfig = $false
    $foundRoot = $false

    if ($isFile) {
        throw "Evaluated a file `"$TargetPath`" as a directory. Exiting."
    }

    # we need to force to ensure we grab hidden files in the directory dump
    $files = Get-ChildItem -Force $TargetPath

    foreach($file in $files)
    {
        if($file.Name.ToLower() -eq "assets.json"){
            $foundConfig = $true
        }

        # need to ensure that if we're outside a git repo, we will still break out eventually.
        if(($file.Name.ToLower() -eq ".git") -or -not (Split-Path $TargetPath)){
            $foundRoot = $true
        }
    }

    return @(
        $foundConfig, $foundRoot
    )
}

<#
.SYNOPSIS
Gets the relative path of the assets json within the target repo.

.PARAMETER Config
The config
#>
Function AscendToRepoRoot {
    param(
        [Parameter(Mandatory = $false)]
        [string] $StartPath
    )
    $pathForManipulation = $StartPath
    if(Test-Path $StartPath -PathType Leaf){
        $pathForManipulation = Split-Path $pathForManipulation
    }

    $foundConfig, $reachedRoot = Evaluate-Target-Dir -TargetPath $pathForManipulation

    while (-not $reachedRoot){
        $pathForManipulation, $remainder = Split-Path $pathForManipulation

        $foundConfig, $reachedRoot = Evaluate-Target-Dir -TargetPath $pathForManipulation
    }

    if ($reachedRoot){
        return $pathForManipulation
    }
    else {
        throw "Unable to the root of the git repo."
    }
}


<#
.SYNOPSIS
Traverses up from a provided target directory to find a assets JSON, parses it, and returns the location and JSON content.

.DESCRIPTION
Traverses upwards until it hits either a `.git` folder or a `assets.json` file. Throws an exception if it can't find a assets.json before it hits root.

.PARAMETER TargetPath
Optional argument specifying the directory to start traversing up from. If not provided, current working directory will be used.
#>
Function Resolve-AssetsJson {
    param (
        [Parameter(Mandatory = $false)]
        [string] $TargetPath
    )
    $pathForManipulation = $TargetPath
    $foundConfig = $false
    $reachedRoot = $false

    if(-not $TargetPath){
        $pathForManipulation = Get-Location
    }
    
    $foundConfig, $reachedRoot = Evaluate-Target-Dir -TargetPath $pathForManipulation

    while (-not $foundConfig -and -not $reachedRoot){
        $pathForManipulation, $remainder = Split-Path $pathForManipulation

        $foundConfig, $reachedRoot = Evaluate-Target-Dir -TargetPath $pathForManipulation
    }

    if ($foundConfig){
        $discoveredPath = Join-Path $pathForManipulation "assets.json"
    }
    else {
        throw "Unable to locate assets.json"
    }

    # path to assets Json
    $config = (Get-Content -Path $discoveredPath | ConvertFrom-Json)
    Add-Member -InputObject $config -MemberType "NoteProperty" -Name "AssetsJsonLocation" -Value "$discoveredPath"

    $relPath = AscendToRepoRoot -StartPath $discoveredPath
    if($relPath){
        try {
            Push-Location $relPath
            $relPath = Resolve-Path -Relative -Path $discoveredPath

            # relpaths are returned with ".\<blah>"
            # given that, we need to get rid of it. This has possiiblity for bugs down the line.
            $relPath = $relPath.Substring(2)
        }
        finally {
            Pop-Location
        }

        # relative path to assets Json from within path
        Add-Member -InputObject $config -MemberType "NoteProperty" -Name "AssetsJsonRelativeLocation" -Value $relPath
    }

    return $config
}

Function Resolve-AssetStore-Location {
    if (-not (Test-Path $ASSETS_STORE)){
        New-Item -Type Directory -Force -Path $ASSETS_STORE | Out-Null
    }
    $ASSETS_STORE = Resolve-Path $ASSETS_STORE

    return $ASSETS_STORE
}

Function Get-MD5-Hash {
    param(
        [Parameter(Mandatory=$true)]
        [string] $Input
    )

    $stringAsStream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.StreamWriter]::new($stringAsStream)
    $writer.write($Input)
    $writer.Flush()
    $stringAsStream.Position = 0
    return Get-FileHash -InputStream $stringAsStream -Algorithm MD5
}

Function Resolve-AssetRepo-Location {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $assetsLocation = Resolve-AssetStore-Location
    $repoName = $Config.AssetsRepo.Replace("/", ".")

    # this is where we will need to handle the multi-copying of the repository.
    # to begin with, we will use the relative path of the assets json in combination with the
    # Repo/RepoId to create a unique hash. In the future, we will need to take the targeted commit into account,
    # and resolve conflicts
    if ($Config.AssetsRepoId) {
        $repoName = $Config.AssetsRepoId
    }

    $repoNameHashed = Get-MD5-Hash -Input ((Join-Path $repoName $Config.AssetsJsonRelativeLocation).ToString())

    $repoPath = (Join-Path $assetsLocation $repoNameHashed.Hash)
    
    if (-not (Test-Path $repoPath)){
        New-Item -Type Directory -Force -Path $repoPath | Out-Null
    }
    
    return $repoPath
}

Function Get-Default-Branch {
    param(
        $Config
    )
    $repoJsonResult = curl "https://api.github.com/repos/$($Config.AssetsRepo)"
    return ($repoJsonResult | ConvertFrom-Json).default_branch
}

<#
.SYNOPSIS
This function returns a boolean that indicates whether or not the assets repo has been initialized.
#>
Function Is-AssetsRepo-Initialized {
    param(
        [PSCustomObject] $Config
    )

    $result = $false
    $assetRepoLocation = Resolve-AssetRepo-Location -Config $Config

    try {
        Push-Location $assetRepoLocation
        $gitLocation = Join-Path $assetRepoLocation ".git"

        if (Test-Path $gitLocation){
            $result = $true
        }
        else{
            $result = $false
        }
    }
    catch {
        Write-Error $_
        $result = $false
    }
    finally {
        Pop-Location
    }

    return $result
}


<#
.SYNOPSIS
Given a configuration, determine which paths must be added to the sparse checkout of the assets repo.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content.

#>
Function Resolve-CheckoutPaths {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $assetsJsonPath = $Config.AssetsJsonRelativeLocation

    $assetsJsonFolder = Split-Path $Config.AssetsJsonRelativeLocation

    if(!$assetsJsonFolder -or $assetsJsonFolder -in @(".", "./", ".\"))
    {
        return $Config.AssetsRepoPrefixPath.Replace("`\", "/")
    }
    else {
        $result = (Join-Path $Config.AssetsRepoPrefixPath $assetsJsonFolder).Replace("`\", "/")
        return $result
    }
}

<#
.SYNOPSIS
Given a configuration, determine the _current_ target path.

.DESCRIPTION 
Determines the presence of a branch on the git repo. If the relevant auto/<service> branch does not exist, we should use main.
#>
Function Resolve-TargetBranch {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $assetRepo = Resolve-AssetRepo-Location -Config $Config
    $branch = "main"
    try {
        Push-Location $assetRepo
        
        $latestCommit = git rev-parse "origin/$($Config.AssetsRepoBranch)"

        if($LASTEXITCODE -eq 0) {
            $branch = $Config.AssetsRepoBranch
        }
    }
    finally {
        Pop-Location
    }

    return $branch
}

<#
.SYNOPSIS
Initializes a recordings repo based on an assets.json file. 

.DESCRIPTION
This Function will NOT re-initialize a repo if it discovers the repo already ready to go.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content from Resolve-AssetsJson.

.PARAMETER ForceReinitialize
Should this assets repo be renewed regardless of current status?
#>
Function Initialize-AssetsRepo {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config,
        [Parameter(Mandatory=$false)]
        [boolean] $ForceReinitialize = $false
    )
    $assetRepo = Resolve-AssetRepo-Location -Config $Config
    $initialized = Is-AssetsRepo-Initialized -Config $Config
    $workCompleted = $false

    if ($ForceReinitialize)
    {
        Remove-Item -Force -R "$assetRepo/*"
        $initialized = $false
    }

    if (!$initialized){
        try {
            Push-Location $assetRepo
        
            Write-Host "git clone --no-checkout --filter=tree:0 https://github.com/$($Config.AssetsRepo) ."
            git clone --no-checkout --filter=tree:0 https://github.com/$($Config.AssetsRepo) .

            $targetPath = Resolve-CheckoutPaths -Config $Config
            $targetBranch = Resolve-TargetBranch -Config $Config

            Write-Host "git sparse-checkout init"
            git sparse-checkout init

            Write-Host "git sparse-checkout set '/*' '!/*/' $targetPath"
            git sparse-checkout set '/*' '!/*/' $targetPath
            
            Write-Host "git checkout $($Config.SHA)"
            git checkout $($Config.SHA)
            
            if($LASTEXITCODE -gt 0){
                throw "Unable to clone to directory $assetRepo"
            }
            $workCompleted = $true
        }
        finally {
            Pop-Location
        }
    }

    return $workCompleted
}

<#
.SYNOPSIS
This function will forcibly reset the repo to a targeted SHA. This is a **destructive** update.

.DESCRIPTION
#>
Function Reset-AssetsRepo {
    param (
        [Parameter(Mandatory = $true)]
        $Config
    )
    try {

        $assetRepo = Resolve-AssetRepo-Location -Config $Config
        Push-Location  $assetRepo

        Write-Host "git checkout *"
        git checkout *
        Write-Host "git clean -xdf"
        git clean -xdf
        Write-Host "git reset --hard (Get-Default-Branch)"
        git reset --hard (Get-Default-Branch)

        # need to figure out the sparse checkouts if we want to optimize this as much as possible
        # for prototyping checking out the whole repo is fine
        if($AssetsRepoSHA){
            Write-Host "git checkout $AssetsRepoSHA"
            git checkout $AssetsRepoSHA
            Write-Host "git pull"
            git pull
        }
    }
    finally {
        Pop-Location
    }
}

<#
.SYNOPSIS
This function's purpose is solely to update a assets.json (both config and on file) with a new recording SHA.

.DESCRIPTION
Retrieves the location of the target recording.json by looking at a property of the Config object.
#>
Function Update-AssetsJson {
    param(
        $Config,
        $NewSHA
    )
    
    $jsonAtRest = Get-Content $Config.AssetsJsonLocation | ConvertFrom-Json

    # update the sha in our current live config
    $Config.SHA = $NewSHA

    # ensure it is propogated to disk
    $jsonAtRest.SHA = $NewSHA

    $jsonAtRest | ConvertTo-Json | Set-Content -Path $Config.AssetsJsonLocation

    return $Config
}

<#
.SYNOPSIS
This function assumes a set of changes and attempts to use the provided config to automatically push a commit to the configured branch and repo combination.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content from Resolve-AssetsJson.

#>
Function Push-AssetsRepo-Update {
    param(
        [PSCustomObject]$Config
    )
    $newSha = $Config.SHA
    $gitUser = git config --global user.name
    $autoCommitMessage = "Automatic asset update from $gitUser."

    $assetRepo = Resolve-AssetRepo-Location -Config $Config
    try {
        Push-Location $assetRepo
        $statusResult = git status --porcelain

        if(!$statusResult){
            Write-Host "No pending changes."
            exit(0)
        }

        $alreadyLatestSHA = $true
        Write-Host "git rev-parse origin/$($Config.AssetsRepoBranch)"
        $retrievedLatestSHA = git rev-parse origin/$($Config.AssetsRepoBranch)
        Write-Host "Latest SHA is $retrievedLatestSHA."

        # if the above command fails with code 128, the target auto commit branch does not exist, and we need to create it
        if($LASTEXITCODE -ne 0 -and $LASTEXITCODE -eq 128){
            Write-Host "Need to checkout new branch based on current changes."
            $alreadyLatestSHA = $true
        }
        elseif ($LASTEXITCODE -ne 0) {
            Write-Error "A non-code-128 error is not expected here. Check git command output above."
            exit(1)
        }
        # if the branch already exists, we need to check to see if we're actually on the latest commit 
        else {
            if($retrievedLatestSHA -ne $Config.SHA){
                $alreadyLatestSHA = $false
            }
        }

        Write-Host "Based off latest commit: $alreadyLatestSHA"
        
        # if we are based off the latest commit (or it's a nonexistent branch), all we gotta do is checkout a new branch of the correct name and push it.
        if($alreadyLatestSHA) {
            Write-Host "git checkout -b $($Config.AssetsRepoBranch)"
            git checkout -b $($Config.AssetsRepoBranch)
            Write-Host "git add -A ."
            git add -A .
            Write-Host "git commit -m `"$autoCommitMessage`""
            git commit -m "$autoCommitMessage"
            Write-Host "git push origin $($Config.AssetsRepoBranch)"
            git push origin $($Config.AssetsRepoBranch)
        }
        else {
            # TODO is there a noticable downside to stash versus saving our own patchfile like in git-branch-push?
            Write-Host "git stash"
            git stash

            # TODO we want to only fetch the latest commit, instead of the entire branch
            Write-Host "git fetch origin $($Config.AssetsRepoBranch)"
            git fetch origin $($Config.AssetsRepoBranch)
            Write-Host "git checkout $($Config.AssetsRepoBranch)"
            git checkout $($Config.AssetsRepoBranch)

            Write-Host "git stash pop"
            git stash pop

            Write-Host "git add -A ."
            git add -A .
            Write-Host "git commit -m `"$autoCommitMessage`""
            git commit -m "$autoCommitMessage"
            Write-Host "git push origin $($Config.AssetsRepoBranch)"
            git push origin $($Config.AssetsRepoBranch)
        }

        $newSha = git rev-parse HEAD
        Update-AssetsJson -Config $Config -NewSHA $newSha
    }
    catch {
        Write-Error $_
    }
    finally {
        Pop-Location
    }

    return $newSha
}
