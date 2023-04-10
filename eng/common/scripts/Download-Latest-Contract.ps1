# This script downloads the project's latest GA contract if there is a GA tag provided. The contract is downloaded at {root}/../sparse-spec/sdk/{ProjectDirectory}/
# You need to implement these functions in in Language-Settings.ps1:
# Get-${Language}-LatestTag: Returns the tag tied with the latest GA version
# Get-${Language}-SourceCodeSubDirectory: Returns the sub folder of source code relative to the project folder
[CmdletBinding()]
param (
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectDirectory
)

. $PSScriptRoot/Helpers/Sparse-Clone-Helpers.ps1
. $PSScriptRoot/common.ps1

function GetCommit([string]$sdkGitRemoteAPI) {
    try {
        $response =  Invoke-WebRequest $sdkGitRemoteAPI
        $responseContent = ConvertFrom-Json $([String]::new($response.Content))
        return $responseContent.object.sha
    }
    catch {
        throw $_.Exception.Message
    }
}

function GetGitRemoteValue() {
    Push-Location $ProjectDirectory
    $result = ""
    try {
        $gitRemotes = (git remote -v)
        foreach ($remote in $gitRemotes) {
            if ($remote.StartsWith("origin") -and $remote -match '(?:https://github.com/|git@github.com:).*/(.*).git\s+') {
                return $Matches[1]
            }
        }
    }
    finally {
        Pop-Location
    }

    return $result
}

function GetProjectRelativePath() {
    $rootPath = GetProjectRootPath $ProjectDirectory
    $subDirectory = &$GetSourceCodeSubDirectory
    return [System.IO.Path]::GetRelativePath($rootPath, (Join-Path $ProjectDirectory $subDirectory)).Replace("\","/")
}

if (!(Test-Path "Function:$GetLatestTagFn")) {
    return
}

$tagName = &$GetLatestTagFn $ProjectDirectory
if (!$tagName) {
    return
}

$sdkRepoName = GetGitRemoteValue
$sdkGitRemoteAPI = "https://api.github.com/repos/Azure/$sdkRepoName/git/refs/tags/$tagName"
$sdkGitRemote = "https://github.com/Azure/$sdkRepoName.git"
$latestCommit = GetCommit $sdkGitRemoteAPI

if ($latestCommit) {
    $pieces = $ProjectDirectory.Replace("\","/").Split("/")
    $projectName = $pieces[$pieces.Count - 1]
    
    $sdkCloneDir = GetSparseCloneDir $ProjectDirectory $projectName "sdk"

    Write-Host "Setting up sparse clone for $projectName at $sdkCloneDir"

    Push-Location $sdkCloneDir.Path
    try {        
        $projectRelativePath = GetProjectRelativePath
        if (!(Test-Path ".git")) {
            InitializeSparseGitClone $sdkGitRemote
            AddSparseCheckoutPath $projectRelativePath
        }
        git checkout $latestCommit
    }
    finally {
        Pop-Location
    }
}
