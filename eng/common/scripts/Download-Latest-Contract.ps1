[CmdletBinding()]
param (
    [Parameter(Position=0)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectDirectory,
    [ValidateNotNullOrEmpty()]
    [String] $TagName
)

. $PSScriptRoot/Helpers/Sparse-Clone-Helpers.ps1

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
    return [System.IO.Path]::GetRelativePath($rootPath, (Join-Path $ProjectDirectory "src")).Replace("\","/")
}


$sdkRepoName = GetGitRemoteValue
$sdkGitRemoteAPI = "https://api.github.com/repos/Azure/$sdkRepoName/git/refs/tags/$TagName"
$sdkGitRemote = "https://github.com/Azure/$sdkRepoName.git"
$latestCommit = GetCommit $sdkGitRemoteAPI

if ($latestCommit) {
    $pieces = $ProjectDirectory.Replace("\","/").Split("/")
    $projectName = $pieces[$pieces.Count - 1]
    
    $sdkCloneDir = GetSparseCloneDir $ProjectDirectory $projectName "sdk"

    Write-Host "Setting up sparse clone for $projectName at $sdkCloneDir"

    Push-Location $sdkCloneDir.Path
    try {
        if (!(Test-Path ".git")) {
            InitializeSparseGitClone $sdkGitRemote
            AddSparseCheckoutPath (GetProjectRelativePath)
        }
        git checkout $latestCommit
    }
    finally {
        Pop-Location
    }
}