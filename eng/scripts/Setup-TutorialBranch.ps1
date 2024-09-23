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

$ErrorActionPreference = "Stop"

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

Write-Host "Creating/updating branch: $BranchName"

try {
    $patchBody = @{
        sha = $gitReferenceSha
        force = $true
    }
    $patchBodyAsJson = ConvertTo-Json -InputObject $patchBody

    $patchUri = "https://api.github.com/repos/$GitHubRepositoryOwner/$GitHubRepositoryName/git/refs/heads/$BranchName"
    Write-Host "Attempting to update ref for: $patchUri"
    
    $patchResponse = Invoke-RestMethod `
        -Method PATCH `
        -Uri $patchUri `
        -Authentication Bearer `
        -Token $secureGitHubPersonalAccessToken `
        -Body $patchBodyAsJson `
        -ContentType "application/json"

    Write-Host "Successfully updated ref."
}
catch {
    Write-Host "Failed to update branch, attempting to create."

    $postBody = @{
        ref = "refs/heads/$BranchName"
        sha = $gitReferenceSha
    }
    $postBodyAsJson = ConvertTo-Json -InputObject $postBody

    $postUri = "https://api.github.com/repos/$GitHubRepositoryOwner/$GitHubRepositoryName/git/refs"
    Write-Host "Attempting to update ref for: $postUri"

    $postResponse = Invoke-RestMethod `
        -Method POST `
        -Uri $postUri `
        -Authentication Bearer `
        -Token $secureGitHubPersonalAccessToken `
        -Body $postBodyAsJson `
        -ContentType "application/json"    

        Write-Host "Successfully created ref."
}