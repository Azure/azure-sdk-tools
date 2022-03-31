# See https://stackoverflow.com/a/14440066 for a succinct explanation. We will be using
# this methodology for any global variables that can be considered constants
. (Join-Path $PSScriptRoot ".." "scripts" "common.ps1" )

$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")
$ASSETS_STORE = (Join-Path $REPO_ROOT ".assets")

<#
.SYNOPSIS
Checks the contents of a directory, then returns a tuple of booleans @("recordingJsonPresent", "isRootFolder").
.DESCRIPTION
Evaluates a directory by checking its contents. First value of the tuple is whether or not a "recording.json" file 
is present A "root" directory is one where a recording.json is present OR where we are as far up the file tree as 
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
    $foundRecording = $false
    $foundRoot = $false

    if ($isFile) {
        Write-Error "Evaluated a file `"$TargetPath`" as a directory. Exiting."
    }

    # we need to force to ensure we grab hidden files in the directory dump
    $files = Get-ChildItem -Force $TargetPath

    foreach($file in $files)
    {
        if($file.Name.ToLower() -eq "recording.json"){
            $foundRecording = $true
        }

        # need to ensure that if we're outside a git repo, we will still break out eventually.
        if(($file.Name.ToLower() -eq ".git") -or -not (Split-Path $TargetPath)){
            $foundRoot = $true
        }
    }

    return @(
        $foundRecording, $foundRoot
    )
}

<#
.SYNOPSIS
Traverses up from a provided target directory to find a recording JSON, parses it, and returns the location and JSON content.

.DESCRIPTION
Traverses upwards until it hits either a `.git` folder or a `recording.json` file. Throws an exception if it can't find a recording.json before it hits root.

.PARAMETER TargetPath
Optional argument specifying the directory to start traversing up from. If not provided, current working directory will be used.
#>
Function Resolve-RecordingJson {
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
        $pathForManipulation, $remainder = Split-Path $props

        $found_config, $reached_root = Evaluate-Target-Dir -TargetPath $pathForManipulation
    }

    if ($foundConfig){
        $discoveredPath = Join-Path $pathForManipulation "recording.json"
    }
    else {
        throw "Unable to locate recording.json"
    }

    # path to recording Json
    $config = (Get-Content -Path $discoveredPath | ConvertFrom-Json)
    Add-Member -InputObject $config -MemberType "NoteProperty" -Name "RecordingJsonLocation" -Value "$discoveredPath"

    $relPath = ""

    if($discoveredPath.StartsWith($REPO_ROOT)) {
        try {
            Push-Location $REPO_ROOT
            $relPath = Resolve-Path -Relative -Path $discoveredPath
        }
        finally {
            Pop-Location
        }
    }

    # relative path to recording Json from within path
    Add-Member -InputObject $config -MemberType "NoteProperty" -Name "RecordingJsonRelativeLocation" -Value $relPath

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
    # to begin with, we will use the relative path of the recording json in combination with the
    # Repo/RepoId to create a unique hash. In the future, we will need to take the targeted commit into account,
    # and resolve conflicts
    if ($Config.AssetsRepoId) {
        $repoName = $Config.AssetsRepoId
    }

    $repoNameHashed = Join-Path $repoName $Config.RecordingJsonRelativeLocation



    $repoPath = (Join-Path $assetsLocation $repoName)
    
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
Initializes a recordings repo based on a recordings.json file. 

.DESCRIPTION
This Function will NOT re-initialize a repo if it discovers the repo already ready to go.

.PARAMETER Config
A PSCustomObject that contains an auto-parsed recording.json content.

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
    
    $isInitialized = $false

    if(-not $isInitialized){
        try {
            Push-Location $assetRepo
            git clone --filter=blob:none --no-checkout "https://github.com/$($Config.AssetsRepo)" .
        }
        finally {
            Pop-Location
        }
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
        if($RecordingRepoSHA){
            Write-Host "git checkout $RecordingRepoSHA"
            git checkout $RecordingRepoSHA
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
This function's purpose is solely to update a recording.json (both config and on file) with a new recording SHA.

.DESCRIPTION

#>
Function Update-Recording-Json {
    param(
        $Config,
        $NewSHA
    )
    
    Write-Host "Update!"
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
