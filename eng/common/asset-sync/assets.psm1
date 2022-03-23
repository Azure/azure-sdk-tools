# any functions that are exported outside the module are exposed via Export-ModuleMember and are documented.
# remaining functions NOT exported are essentially private functions

# Module-level variables can be accessed via $script:<Variable> naming convention
# See https://stackoverflow.com/a/14440066 for a succinct explanation. We will be using
# this methodology for any global variables

$REPO_ROOT = Resolve-Path (Join-Path $PSScriptRoot ".." ".." "..")

function Evaluate-Target-Dir {
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
Traverses upwards until it hits either a `.git` folder or a `recording.json` file.

.PARAMETER TargetPath
Optional argument specifying the directory to start traversing up from. If not provided, current working directory will be used.

.COMPONENT
assets
#>
function Resolve-RecordingJson {
    param (
        [Parameter(Mandatory = $false)]
        [string] $TargetPath
    )
    $PathForManipulation = $TargetPath
    $foundConfig = $false
    $reachedRoot = $false

    if(-not $TargetPath){
        $PathForManipulation = Get-Location
    }
    
    
    $foundConfig, $foundRoot = Evaluate-Target-Dir -TargetPath PathForManipulation

    while (-not $foundConfig -and -not $foundRoot){
        $prospectivePath, $remainder = Split-Path $props
    }
}
Export-ModuleMember -Function Resolve-RecordingJson


function Resolve-Asset-Repo-Location {
    $prospectivePath = Join-Path $REPO_ROOT ".assets"

    if (-not (Test-Path $prospectivePath)) {
        mkdir -p $prospectivePath
    }

    return (Resolve-Path $prospectivePath)
}

# <#
# .SYNOPSIS

# .DESCRIPTION

# .COMPONENT
# assets
# #>
# function Initialize-Recordings-Repo {
#     if (!(Test-Path $AssetsRepoLocation)){
#         mkdir -p $AssetsRepoLocation
#     }
#     else {
#         Remove-Item -Force -R "$AssetsRepoLocation/*"
#     }

#     try {
#         Push-Location $AssetsRepoLocation
#         git clone --filter=blob:none --no-checkout "https://github.com/$AssetsRepo" .
#     }
#     finally {
#         Pop-Location
#     }
# }


# <#
# .SYNOPSIS


# .DESCRIPTION

# .COMPONENT
# assets
# #>
# function Reset-Recordings-Repo {
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
# function Recordings-Repo-Initialized {
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

# function Commit-Pending-Changes {

# }

# function Pending-Changes {
#     $result = (git status --porcelain)
#     if ($result){
#         return $true
#     }
#     else{
#         return $false
#     }
# }

# # do we add traversal logic here?
# function Retrieve-SHA-From-JSON {
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