param (
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[\w\-]+$")]
    [string]
    $GitHubRepositoryOwner,
    
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[\w\-]+$")]
    [string]
    $GitHubRepositoryName,
    
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^[\w\-\/]+$")]
    [string]
    $GitReference,
    
    [Parameter(Mandatory=$true)]
    [string]
    [ValidatePattern("^[\w\-\/]+$")]
    $BranchName,
    
    [Parameter(Mandatory=$true)]
    [string]
    $GitHubPersonalAccessToken
)

$ErrorActionPreference = Stop

Write-Host "GitHubRepositoryOwner is: $GitHubRepositoryOwner"
Write-Host "GitHubRepositoryName is: $GitHubRepositoryName"
Write-Host "GitReference is: $GitReference"
Write-Host "BranchName is: $BranchName"

$secureGitHubPersonalAccessToken = ConvertTo-SecureString -String $GitHubPersonalAccessToken -AsPlainText -Force

$getUri = "https://api.github.com/repos/$GitHubRepositoryOwner/$GitHubRepositoryName/git/$GitReference"
Write-Host "Retrieving SHA for: $getUri"

$getResponse = Invoke-RestMethod `
    -Method GET `
    -Uri $getUri `
    -Authentication Bearer `
    -Token $secureGitHubPersonalAccessToken

$gitReferenceSha = $getResponse.object.sha

Write-Host "SHA for $GitReference is: $gitReferenceSha"

$postBody = @{
    ref = "refs/heads/$BranchName"
    sha = $gitReferenceSha
}

$postBodyAsJson = ConvertTo-Json -InputObject $postBody

Write-Host "Creating branch: $BranchName"

$postResponse = Invoke-RestMethod `
    -Method POST `
    -Uri "https://api.github.com/repos/$GitHubRepositoryOwner/$GitHubRepositoryName/git/refs" `
    -Authentication Bearer `
    -Token $secureGitHubPersonalAccessToken `
    -Body $postBodyAsJson `
    -ContentType "application/json"
