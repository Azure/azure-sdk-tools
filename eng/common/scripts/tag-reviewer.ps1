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
    $AuthToken
)

if (-not $GitHubUser) {
  Write-Host "No user provided for addition, exiting."
  exit(0)
}

$headers = @{
  Authorization = "bearer $AuthToken"
}
$uri = "https://api.github.com/repos/$RepoOwner/$RepoName/pulls/$PRNumber/requested_reviewers"

try {
  $resp = Invoke-RestMethod -Headers $headers $uri -MaximumRetryCount 3
}
catch {
  Write-Error "Invoke-RestMethod [$uri] failed with exception:`n$_"
  exit 1
}

# the response object takes this form: https://developer.github.com/v3/pulls/review_requests/#response-1
# before we can push a new reviewer, we need to pull the simple Ids out of the complex objects that came back in the response
$userReviewers = @($resp.users | % { return $_.login })
$teamReviewers = @($resp.teams | % { return $_.slug })

if (-not $userReviewers.Contains($GitHubUser)){
  $userReviewers += $GitHubUser

  $postResp = @{
    reviewers = $userReviewers
    team_reviewers = $teamReviewers
  } | ConvertTo-Json

  try {
    $resp = Invoke-RestMethod -Method Post -Headers $headers -Body $postResp -Uri $uri -MaximumRetryCount 3
    $resp | Write-Verbose
  }
  catch {
    Write-Error "Unable to add reviewer."
    Write-Error $_
  }
}
else {
  Write-Host "Reviewer $GitHubUser already added. Exiting."
  exit(0)
}
