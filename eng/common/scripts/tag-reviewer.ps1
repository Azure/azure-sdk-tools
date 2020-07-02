param(
    [Parameter(Mandatory = $true)]
    $RepoOwner,

    [Parameter(Mandatory = $true)]
    $RepoName,

    [Parameter(Mandatory = $true)]
    $GitHubUser,

    [Parameter(Mandatory = $true)]
    $PRNumber,
  
    [Parameter(Mandatory = $true)]
    $AuthToken,
)

$headers = @{
  Authorization = "bearer $AuthToken"
}


# sample
# https://api.github.com/repos/MicrosoftDocs/azure-docs-sdk-python/pulls/677/requested_reviewers