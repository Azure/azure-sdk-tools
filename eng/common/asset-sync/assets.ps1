# See https://stackoverflow.com/a/14440066 for a succinct explanation. We will be using
# this methodology for any global variables that can be considered constants
. (Join-Path $PSScriptRoot ".." "scripts" "common.ps1" )

$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")
$ASSETS_STORE = (Join-Path $REPO_ROOT ".assets")

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
        Write-Error "Evaluated a file `"$TargetPath`" as a directory. Exiting."
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

        $foundConfig, $reached_root = Evaluate-Target-Dir -TargetPath $pathForManipulation
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

    if($discoveredPath.StartsWith($REPO_ROOT)) {
        try {
            Push-Location $REPO_ROOT
            $relPath = Resolve-Path -Relative -Path $discoveredPath
        }
        finally {
            Pop-Location
        }
    }

    # relative path to assets Json from within path
    Add-Member -InputObject $config -MemberType "NoteProperty" -Name "AssetsJsonRelativeLocation" -Value $relPath

    return $config
}

Function Resolve-AssetStore-Location {
    if (-not (Test-Path $ASSETS_STORE)){
        mkdir -p $ASSETS_STORE | Out-Null
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
        mkdir -p $repoPath | Out-Null
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

.DESCRIPTION
#>
Function Is-AssetsRepo-Initialized {
    param(
        [PSCustomObject] $Config
    )

    $result = $false
    $assetRepoLocation = Resolve-AssetRepo-Location -Config $Config

    try {
        Push-Location $assetRepoLocation

        $originData = (git remote show origin)

        $result = $originData.Contains($Config.AssetsRepo)
    }
    catch {
        Write-Host $_
        $result = $false
    }
    finally {
        Pop-Location
    }

    return $result
}

<#
.SYNOPSIS
Initializes a recordings repo based on an assets.json file. 

.DESCRIPTION
This Function will NOT re-initialize a repo if it discovers the repo already ready to go.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed assets.json content.

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

    if ($ForceReinitialize)
    {
        Remove-Item -Force -R "$assetRepo/*"
    }
    
    Write-Host "git clone --filter=blob:none --no-checkout `"https://github.com/$($Config.AssetsRepo)`" $assetRepo"
    git clone --filter=blob:none --no-checkout "https://github.com/$($Config.AssetsRepo)" $assetRepo

    if($LASTEXITCODE -gt 0){
        throw "Unable to clone to directory $assetRepo"
    }
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
This function returns a boolean that indicates whether or not the assets repo has been initialized.

.DESCRIPTION
#>
Function Push-AssetsRepo-Update {
    param(
        [PSCustomObject]$Config
    )

    $assetRepo = Resolve-AssetRepo-Location -Config $Config

    $newSha = "I have a new SHA!"

    return $newSha
}
