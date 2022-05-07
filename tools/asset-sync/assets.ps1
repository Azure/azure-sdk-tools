Set-StrictMode -Version 3

$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." "..")
$ASSETS_STORE = (Join-Path $REPO_ROOT ".assets")
$EXPECTED_MEMBERS = @("AssetsRepo", "AssetsRepoPrefixPath", "AssetsRepoBranch", "SHA")

. (Join-Path $REPO_ROOT "eng" "common" "scripts" "common.ps1")

<#
.SYNOPSIS
Checks the contents of a directory, then returns an array of booleans @(<assetsJsonPresent>, <isRootFolder>).

.DESCRIPTION
Evaluates a directory by checking its contents. First value of the tuple is whether or not a "assets.json" file 
is present A "root" directory is one where a assets.json is present OR where we are as far up the file tree as 
we can possibly ascend. 

.PARAMETER TargetPath
A targeted directory. This MUST be a directory, not a file path.
#>
Function EvaluateDirectory {
    param (
        [Parameter(Mandatory = $true)]
        [string] $TargetPath
    )
    if (!(Test-Path $TargetPath)){
        return @(
            $false, $false
        )
    }

    # can't handle files here
    if (Test-Path $TargetPath -PathType Leaf) {
        throw "Evaluated a file `"$TargetPath`" as a directory. Exiting."
    }

    return @(
        (Test-Path -Path (Join-Path $TargetPath "assets.json")),
        (Test-Path -Path (Join-Path $TargetPath ".git"))
    )
}

<#
.SYNOPSIS
From a start directory, stops when it finds the root of a git repository (or root of disk).

.PARAMETER StartPath
The starting directory.
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

    $foundConfig, $reachedRoot = EvaluateDirectory -TargetPath $pathForManipulation

    while (-not $reachedRoot){
        $pathForManipulation, $remainder = Split-Path $pathForManipulation

        $foundConfig, $reachedRoot = EvaluateDirectory -TargetPath $pathForManipulation
    }

    return $pathForManipulation
}


<#
.SYNOPSIS
Traverses up from a provided target directory to find a assets JSON, parses it, and returns the location and JSON content.

.DESCRIPTION
Traverses upwards until it hits either a `.git` folder or a `assets.json` file. Throws an exception if it can't find a assets.json before it hits root.

.PARAMETER TargetPath
Optional argument specifying the directory to start traversing up from. If not provided, current working directory will be used.
#>
Function ResolveAssetsJson {
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
    else {
        $pathForManipulation = Resolve-Path -Path $TargetPath
    }
    
    $foundConfig, $reachedRoot = EvaluateDirectory -TargetPath $pathForManipulation

    while (-not $foundConfig -and -not $reachedRoot){
        $pathForManipulation, $remainder = Split-Path $pathForManipulation

        $foundConfig, $reachedRoot = EvaluateDirectory -TargetPath $pathForManipulation
    }

    if ($foundConfig){
        $discoveredPath = Join-Path $pathForManipulation "assets.json"
    }
    else {
        throw "Unable to locate assets.json"
    }

    # path to assets Json
    $config = (Get-Content -Raw -Path $discoveredPath | ConvertFrom-Json)
    Add-Member -InputObject $config -MemberType "NoteProperty" -Name "AssetsJsonLocation" -Value "$discoveredPath"

    $relPath = AscendToRepoRoot -StartPath $discoveredPath
    if($relPath){
        try {
            Push-Location $relPath
            $relPath = Resolve-Path -Relative -Path $discoveredPath

            # relpaths are returned with "./<blah>"
            # given that, we need to get rid of it. This has possibility for bugs down the line.
            $relPath = $relPath -replace "^(\.\/)|(\.\\)"
        }
        finally {
            Pop-Location
        }

        # relative path to assets Json from within path
        Add-Member -InputObject $config -MemberType "NoteProperty" -Name "AssetsJsonRelativeLocation" -Value $relPath
    }

    $props = Get-Member -InputObject $config -MemberType NoteProperty | ForEach-Object { $_.Name }
    $missingMembers = Compare-Object -ReferenceObject $EXPECTED_MEMBERS -DifferenceObject $props `
        | Where-Object { $_.SideIndicator -ne "=>"} | Foreach-Object { $_.InputObject }

    if($missingMembers){
        if($missingMembers.Length -gt 0){
            $allMissingMembers = $missingMembers -Join ", "
            throw "Missing required members for assets json detected: `"$($allMissingMembers)`""
        }
    }

    return $config
}

<#
.SYNOPSIS
Returns the location of the "assets" store. This should return a string of the form "<path to language repo root>/.assets."
#>
Function ResolveAssetStoreLocation {
    if (-not (Test-Path $ASSETS_STORE)){
        New-Item -Type Directory -Force -Path $ASSETS_STORE | Out-Null
    }
    $ASSETS_STORE = Resolve-Path $ASSETS_STORE

    return $ASSETS_STORE
}

<#
Takes an input string, returns the MD5 hash for the entire thing.

.PARAMETER Input
Any string.
#>
Function GetMD5Hash {
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

<#
.SYNOPSIS
Given a configuration, where will the assets repo exist? This function will both return that answer, as well as ensure
that directory exists.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function ResolveAssetRepoLocation {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $assetsLocation = ResolveAssetStoreLocation
    $repoName = $Config.AssetsRepo.Replace("/", ".")

    # this is where we will need to handle the multi-copying of the repository.
    # to begin with, we will use the relative path of the assets json in combination with the
    # Repo/RepoId to create a unique hash. In the future, we will need to take the targeted commit into account,
    # and resolve conflicts
    if ($Config.AssetsRepoId) {
        $repoName = $Config.AssetsRepoId
    }

    $repoNameHashed = GetMD5Hash -Input ((Join-Path $repoName $Config.AssetsJsonRelativeLocation).ToString())

    $repoPath = (Join-Path $assetsLocation $repoNameHashed.Hash)
    
    if (-not (Test-Path $repoPath)){
        New-Item -Type Directory -Force -Path $repoPath | Out-Null
    }
    
    return $repoPath
}

<#
.SYNOPSIS
Gets the default branch from the git repo targeted in a assets.json.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function GetDefaultBranch {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $repoJsonResult = Invoke-RestMethod -Method "GET" -Uri "https://api.github.com/repos/$($Config.AssetsRepo)"
    return ($repoJsonResult | ConvertFrom-Json).default_branch
}

<#
.SYNOPSIS
This function returns a boolean that indicates whether or not the assets repo has been initialized.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function IsAssetsRepoInitialized {
    param(
        [PSCustomObject] $Config
    )

    $result = $false
    $assetRepoLocation = ResolveAssetRepoLocation -Config $Config

    try {
        $gitLocation = Join-Path $assetRepoLocation ".git"
        $result = Test-Path $gitLocation
    }
    catch {
        Write-Error $_
        $result = $false
    }

    return $result
}


<#
.SYNOPSIS
Given a configuration, determine which paths must be added to the sparse checkout of the assets repo.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function ResolveCheckoutPaths {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
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
Resolve which branch on the assets repo should be checked out, given an input Configuration.

.DESCRIPTION 
Determines the presence of a branch on the git repo. If the relevant auto/<service> branch does not exist, we should use main.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function ResolveTargetBranch {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $assetRepo = ResolveAssetRepoLocation -Config $Config
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
Uses a target config to reset an already initialized assets repository to another service/commit SHA.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content from ResolveAssetsJson.

.PARAMETER SparseCheckoutPath
The target path within the repo that should be sparsely checked out. Multiple path values are supported, but one must place spaces between them.

#>
Function CheckoutRepoAtConfig {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config,
        [string] $SparseCheckoutPath
    )

    Write-Host "git sparse-checkout set $($SparseCheckoutPath)"
    git sparse-checkout set $SparseCheckoutPath

    Write-Host "git checkout $($Config.SHA)"
    git checkout $($Config.SHA)
}

<#
.SYNOPSIS
Initializes a recordings repo based on an assets.json file. 

.DESCRIPTION
This Function will NOT re-initialize a repo if it discovers the repo already ready to go.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content from ResolveAssetsJson.

.PARAMETER ForceReinitialize
Should this assets repo be renewed regardless of current status?
#>
Function InitializeAssetsRepo {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config,
        [Parameter(Mandatory=$false)]
        [boolean] $ForceReinitialize = $false
    )
    $assetRepo = ResolveAssetRepoLocation -Config $Config
    $initialized = IsAssetsRepoInitialized -Config $Config
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

            $targetPath = ResolveCheckoutPaths -Config $Config

            Write-Host "git sparse-checkout init"
            git sparse-checkout init

            CheckoutRepoAtConfig -Config $Config -SparseCheckoutPath $targetPath
            
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
Used to interrupt script flow and get user input.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.

.PARAMETER UserPrompt
This is the message that should be shown to the user before waiting for input.

#>
Function GetUserInput {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $Config,
        [Parameter(Mandatory = $false)]
        [string] $UserPrompt = "Please type some input, then press ENTER to accept." 
    )

    return Read-Host $UserPrompt
}

<#
.SYNOPSIS
Are there any files changed?

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function DetectPendingChanges {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $Config
    )
    $assetRepo = ResolveAssetRepoLocation -Config $Config
    $filesChanged = @()

    try {
        Push-Location  $assetRepo
        Write-Host "git diff-index --name-only HEAD"
        $filesChanged = git diff-index --name-only HEAD
    }
    finally {
        Pop-Location
    }

    return $filesChanged
}

<#
.SYNOPSIS
This function will forcibly reset the repo to a targeted SHA. This is a **destructive** update.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function ResetAssetsRepo {
    param (
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $Config,
        [Parameter(Mandatory = $false)]
        [bool] $IgnorePendingChanges = $false
    )
    try {
        $assetRepo = ResolveAssetRepoLocation -Config $Config
        $allowReset = $true

        Push-Location  $assetRepo

        if(!$IgnorePendingChanges){
            # detect pending changes
            $pendingChanges = DetectPendingChanges -Config $Config

            if($pendingChanges){
                Write-Host "Visible Pending Changes:"
                Write-Host $pendingChanges
                $userInput = GetUserInput "This operation will need to undo pending changes prior to checking out a different SHA. To abandon pending changes, enter 'y'. An empty 'enter' or 'n' will result in no action."

                if($userInput.Trim().ToLower() -ne 'y'){
                    $allowReset = $false
                }
            }
        }

        if($allowReset){
            Write-Host "git checkout *"
            git checkout *
            Write-Host "git clean -xdf"
            git clean -xdf

            # need to figure out the sparse checkouts if we want to optimize this as much as possible
            # for prototyping checking out the whole repo is fine
            if($Config.SHA){
                CheckoutRepoAtConfig -Config $Config -SparseCheckoutPath (ResolveCheckoutPaths -Config $Config)
            }
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
Retrieves the location of the target recording.json by looking at a property of the Config object. Updates the file at rest, returns the completed object.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.

.PARAMETER NewSHA
A string representing the new SHA.
#>
Function UpdateAssetsJson {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config,
        [Parameter(Mandatory=$true)]
        [string] $NewSHA
    )
    
    $jsonAtRest = Get-Content -Raw -Path $Config.AssetsJsonLocation | ConvertFrom-Json

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
A PSCustomObject that contains an auto-parsed assets.json object from ResolveAssetsJson.
#>
Function PushAssetsRepoUpdate {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Config
    )
    $newSha = $Config.SHA
    $gitUser = git config --global user.name
    $autoCommitMessage = "Automatic asset update from $gitUser."

    $assetRepo = ResolveAssetRepoLocation -Config $Config
    try {
        Push-Location $assetRepo
        $statusResult = git status --porcelain

        if(!$statusResult){
            Write-Host "No pending changes."
            exit 0
        }

        $alreadyLatestSHA = $true
        Write-Host "git rev-parse origin/$($Config.AssetsRepoBranch)"
        $retrievedLatestSHA = git rev-parse origin/$($Config.AssetsRepoBranch)
        Write-Host "Latest SHA is $retrievedLatestSHA."

        # if the above command fails with code 128, the target auto commit branch does not exist, and we need to create it
        if($LASTEXITCODE -eq 128){
            Write-Host "Need to checkout new branch based on current changes."
            $alreadyLatestSHA = $true
        }
        elseif ($LASTEXITCODE -ne 0) {
            Write-Error "A non-code-128 error is not expected here. Check git command output above."
            exit 1
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
        UpdateAssetsJson -Config $Config -NewSHA $newSha
    }
    catch {
        Write-Error $_
    }
    finally {
        Pop-Location
    }

    return $newSha
}
