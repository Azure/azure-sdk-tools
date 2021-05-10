param (
    [Parameter(Mandatory = $true)]
    [string]$RepoOwner,

    [Parameter(Mandatory = $true)]
    [string]$RepoName,

    [Parameter(Mandatory = $true)]
    [string]$RepoPath
)

. (Join-Path $PSScriptRoot common.ps1)

LogDebug "`$RepoOwner was: $RepoOwner"
LogDebug "`$RepoName was: $RepoName"
LogDebug "`$RepoPath was: $RepoPath"

try
{
    $gitHubAuthorizationHeader = Get-GitHubAuthorizationHeaderFromRepository -RepoPath $RepoPath

    if (!$gitHubAuthorizationHeader) {
        # This might seem a bit wierd, but when we make a call to a public GitHub repo
        # with a totally malformed credential it seems to work just fine (it looks like
        # public repos don't even bother to check credentials for public API calls). There
        # might be a rate limiting issue in the future but you would overcome that by
        # using the persistCredentials: true option on the AzP checkout task.

        LogWarn "No authorization header detected!"
        $gitHubAuthorizationHeader = "Basic dummy-value-works-for-public-repos"
    }
    
    $gitHubAuthorizationHeaderBytes = [System.Text.Encoding]::UTF8.GetBytes($gitHubAuthorizationHeader)
    $base64EncodedGitHubAuthorizationHeader = [System.Convert]::ToBase64String($gitHubAuthorizationHeaderBytes)

    $uri = "https://api.github.com/repos/$RepoOwner/$RepoName"
    LogDebug "Computed request URI is: $uri"

    $defaultBranch = Get-GitHubDefaultBranch -RepoOwner $RepoOwner -RepoName $RepoName -AuthToken $base64EncodedGitHubAuthorizationHeader
    Write-Host "##vso[task.setvariable variable=DefaultBranch;isOutput=true]$defaultBranch"
}
catch
{
    Write-Error "Set-DefaultBranchVariable failed with exception:`n$_"
    exit 1
}