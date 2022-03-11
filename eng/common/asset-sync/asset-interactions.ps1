param(
    [string]$Goal,
    [string]$Folder
)

$RepoRoot = Resolve-Path "${PSScriptRoot}..\..\..\.."
$EngDir = Join-Path $RepoRoot "eng"
$EngCommonDir = Join-Path $EngDir "common"
$EngCommonScriptsDir = Join-Path $EngCommonDir "scripts"

$AssetsRepo = "Azure/azure-sdk-for-python-assets"
$AssetsRepoLocation = Join-Path "${RepoRoot}" ".recordings"

$RecordingsFile = Join-Path $RepoRoot "recordings.json"
$ShaObject = @{}
(gc $RecordingsFile | ConvertFrom-Json).psobject.properties | % { $ShaObject[$_.Name] = $_.Value}


$hash = @{}
foreach ($property in $jsonObj.PSObject.Properties) {
    $hash[$property.Name] = $property.Value
}

. (Join-Path $EngCommonScriptsDir Invoke-GitHubAPI.ps1)

function Get-Default-Branch {
    $repoJsonResult = curl "https://api.github.com/repos/$AssetsRepo"
    return ($repoJsonResult | ConvertFrom-Json).default_branch
}

# check for existence within the repo
function Recordings-Repo-Initialized {
    $result = $false
    if (!(Test-Path $AssetsRepoLocation))
    {
        return $result
    }

    try {
        Push-Location $AssetsRepoLocation

        $originData = (git remote show origin)

        $result = $originData.Contains($AssetsRepo)
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

function Initialize-Recordings-Repo {
    if (!(Test-Path $AssetsRepoLocation)){
        mkdir -p $AssetsRepoLocation
    }
    else {
        Remove-Item -Force -R "$AssetsRepoLocation/*"
    }

    try {
        Push-Location $AssetsRepoLocation
        git clone --filter=blob:none --no-checkout "https://github.com/$AssetsRepo" .
    }
    finally {
        Pop-Location
    }
}

function Reset-Recordings-Repo {
    param (
        [Parameter(Mandatory = $true)]
        $targetPath,
        [Parameter(Mandatory = $false)]
        $RecordingRepoSHA = ""
    )
    try {
        Push-Location $AssetsRepoLocation

        Write-Host (Get-Location)

        Write-Host "git checkout *"
        git checkout *
        Write-Host "git clean -xdf"
        git clean -xdf
        Write-Host "git reset --hard (Get-Default-Branch)"
        git reset --hard (Get-Default-Branch)

        Write-Host "git sparse-checkout add $targetPath"
        git sparse-checkout add $targetPath

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

function Commit-Pending-Changes {

}

function Pending-Changes {
    $result = (git status --porcelain)
    if ($result){
        return $true
    }
    else{
        return $false
    }
}

# do we add traversal logic here?
function Retrieve-SHA-From-JSON {
    if ($ShaObject[$Folder]){
        Write-Host "Matched Path from recordings.json: $Folder"
        return $ShaObject[$Folder]
    }
    else{
        Write-Host "No matches in ShaObject found. Falling back to root."
        return $ShaObject["/"]
    }
}

# reset repo to default branch
if ($Goal -eq "playback"){
    $targetSHA = Retrieve-SHA-From-JSON

    try {
        Initialize-Recordings-Repo
        Reset-Recordings-Repo $Folder $targetSHA
    }
    catch {
        Write-Host $_
    }
}

# updates, do we even want to target a branch?
# let's start with checking out 
if ($Goal -eq "submit"){

}
