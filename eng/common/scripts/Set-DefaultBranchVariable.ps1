param (
    [Parameter(Mandatory = $true)]
    [string]$RepoOwner,

    [Parameter(Mandatory = $true)]
    [string]$RepoName
)

. (Join-Path $PSScriptRoot common.ps1)

LogDebug "`$RepoOwner was: $RepoOwner"
LogDebug "`$RepoName was: $RepoName"

try
{
    if ($RepoName -like "*-pr") {
        LogDebug "Assuming private repository, translating repository name to public version"
        $publicRepositoryName = $RepoName.Substring(0, $RepoName.Length - 3)
    } else {
        LogDebug "Assuming public repository"
        $publicRepositoryName = $RepoName
    }

    LogDebug "Computed repository name is: $publicRepositoryName"
    $uri = "https://api.github.com/repos/$RepoOwner/$publicRepositoryName"
    LogDebug "Computed request URI is: $uri"

    $repository = Invoke-RestMethod -Uri $uri
    Write-Host "##vso[task.setvariable variable=DefaultBranch;isOutput=true]$($repository.default_branch)"
}
catch
{
    Write-Error "Set-DefaultBranchVariable failed with exception:`n$_"
    exit 1
}

