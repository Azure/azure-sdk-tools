# Module-level variables can be accessed via $script:<Variable> naming convention from within module functions.
# See https://stackoverflow.com/a/14440066 for a succinct explanation. We will be using
# this methodology for any global variables that can be considered constants
. (Join-Path $PSScriptRoot ".." "scripts" "common.ps1" )

$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")
$ASSETS_STORE = Resolve-Path (Join-Path $REPO_ROOT ".assets")

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
Export-ModuleMember -Function Evaluate-Target-Dir

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

    return @($discoveredPath, (Get-Content -Path $discoveredPath | ConvertFrom-Json))
}
Export-ModuleMember -Function Resolve-RecordingJson

Function Resolve-AssetStore-Location {

    if (-not (Test-Path $script:ASSETS_STORE)){
        mkdir -p $script:ASSETS_STORE | Out-Null
    }

    return $script:ASSETS_STORE
}
Export-ModuleMember -Function Resolve-AssetStore-Location

Function Resolve-AssetRepo-Location {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Context
    )
    $assetsLocation = Resolve-AssetStore-Location
    $repoName = $Context.AssetsRepo.Replace("/", ".")
    
    if ($Context.AssetsRepoId) {
        $repoName = $Context.AssetsRepoId
    }

    $repoPath = (Join-Path $assetsLocation $repoName)
    
    if (-not (Test-Path $repoPath)){
        mkdir -p $repoPath | Out-Null
    }
    
    return $repoPath
}
Export-ModuleMember -Function Resolve-AssetRepo-Location

<#
.SYNOPSIS
Initializes a recordings repo based on a recordings.json file. 

.DESCRIPTION
This Function will NOT re-initialize a repo if it discovers the repo already ready to go.


.PARAMETER Context
A PSCustomObject that contains an auto-parsed recording.json content.

.PARAMETER TargetDirectory
Optional directory containing a "recording.json" file.
#>
Function Initialize-Assets-Repo {
    param(
        [Parameter(Mandatory=$true)]
        [PSCustomObject] $Context,
        [Parameter(Mandatory=$false)]
        [boolean] $ForceReinit = $false
    )
    
    $assetRepo = Resolve-AssetRepo-Location
    $configLocation, $config = Resolve-RecordingJson -TargetPath $TargetDirectory


    # if (!()){
    #     mkdir -p $AssetsRepoLocation | Out-Null
    #     return $AssetsRepoLocation
    # }

    # Remove-Item -Force -R "$AssetsRepoLocation/*"

    # try {
    #     Push-Location $AssetsRepoLocation
    #     git clone --filter=blob:none --no-checkout "https://github.com/$AssetsRepo" .
    # }
    # finally {
    #     Pop-Location
    # }
}



# <#
# .SYNOPSIS


# .DESCRIPTION

# #>
# Function Reset-Recordings-Repo {
#     param (
#         [Parameter(Mandatory = $true)]
#         $targetPath,
#         [Parameter(Mandatory = $false)]
#         $RecordingRepoSHA = ""
#     )
#     try {
#         Push-Location $AssetsRepoLocation

#         Write-Host (Get-Location)

#         Write-Host "git checkout *"
#         git checkout *
#         Write-Host "git clean -xdf"
#         git clean -xdf
#         Write-Host "git reset --hard (Get-Default-Branch)"
#         git reset --hard (Get-Default-Branch)

#         Write-Host "git sparse-checkout add $targetPath"
#         git sparse-checkout add $targetPath

#         if($RecordingRepoSHA){
#             Write-Host "git checkout $RecordingRepoSHA"
#             git checkout $RecordingRepoSHA
#             Write-Host "git pull"
#             git pull
#         }
#     }
#     finally {
#         Pop-Location
#     }
# }



# $RepoRoot = Resolve-Path "${PSScriptRoot}..\..\..\.."
# Write-Host $RepoRoot



# $hash = @{}
# foreach ($property in $jsonObj.PSObject.Properties) {
#     $hash[$property.Name] = $property.Value
# }



# # check for existence within the repo
# Function Recordings-Repo-Initialized {
#     $result = $false
#     if (!(Test-Path $AssetsRepoLocation))
#     {
#         return $result
#     }

#     try {
#         Push-Location $AssetsRepoLocation

#         $originData = (git remote show origin)

#         $result = $originData.Contains($AssetsRepo)
#     }
#     catch {
#         Write-Host $_
#         $result = $false
#     }
#     finally {
#         Pop-Location
#     }

#     return $result
# }

# Function Commit-Pending-Changes {

# }

# Function Pending-Changes {
#     $result = (git status --porcelain)
#     if ($result){
#         return $true
#     }
#     else{
#         return $false
#     }
# }

# # do we add traversal logic here?
# Function Retrieve-SHA-From-JSON {
#     if ($ShaObject[$Folder]){
#         Write-Host "Matched Path from recordings.json: $Folder"
#         return $ShaObject[$Folder]
#     }
#     else{
#         Write-Host "No matches in ShaObject found. Falling back to root."
#         return $ShaObject["/"]
#     }
# }

# # reset repo to default branch
# if ($Goal -eq "playback"){
#     $targetSHA = Retrieve-SHA-From-JSON

#     try {
#         Initialize-Recordings-Repo
#         Reset-Recordings-Repo $Folder $targetSHA
#     }
#     catch {
#         Write-Host $_
#     }
# }

# # updates, do we even want to target a branch?
# # let's start with checking out 
# if ($Goal -eq "submit"){

# }

# exports