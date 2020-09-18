 #!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request for a given branch if it doesn't already exist
.PARAMETER RepoOwner
The GitHub repository owner to create the pull request against.
.PARAMETER RepoName
The GitHub repository name to create the pull request against.
.PARAMETER BaseBranch
The base or target branch we want the pull request to be against.
.PARAMETER PROwner
The owner of the branch we want to create a pull request for.
.PARAMETER PRBranch
The branch which we want to create a pull request for.
.PARAMETER AuthToken
A personal access token
.PARAMETER PRTitle
The title of the pull request.
.PARAMETER PRBody
The body message for the pull request. 
.PARAMETER PRLabels
The labels added to the PRs. Multple labels seperated by comma, e.g "bug, service"
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string]$RepoOwner,

  [Parameter(Mandatory = $true)]
  [string]$RepoName,

  [Parameter(Mandatory = $true)]
  [string]$BaseBranch,

  [Parameter(Mandatory = $true)]
  [string]$PROwner,

  [Parameter(Mandatory = $true)]
  [string]$PRBranch,

  [Parameter(Mandatory = $true)]
  [string]$AuthToken,

  [Parameter(Mandatory = $true)]
  [string]$PRTitle,

  [Parameter(Mandatory = $false)]
  [string]$PRBody = $PRTitle,

  [Parameter(Mandatory = $false)]
  [string]$PRLabels,

  [Parameter(Mandatory = $false)]
  [string]$UserReviewers,

  [Parameter(Mandatory = $false)]
  [string]$TeamReviewers,

  [Parameter(Mandatory = $false)]
  [string]$Assignees
)

$baseURI = "https://api.github.com/repos"
function SplitMembers ($membersString)
{
  return @($membersString.Split(",") | % { $_.Trim() } | ? { return $_ })
}

$userAdditions = SplitMembers -membersString $UserReviewers
$teamAdditions = SplitMembers -membersString $TeamReviewers
$labelAdditions = SplitMembers -membersString $PRLabels
$assigneeAdditions = SplitMembers -membersString $Assignees

$headers = @{
  Authorization = "bearer $AuthToken"
}

function AddMembers($apiURI, $memberName, $additionSet) {
  $headers = @{
    Authorization = "bearer $AuthToken"
  }
  $errorOccurred = $false

  try {
    $postResp = @{}
    $postResp[$memberName] = @($additionSet)
    $postResp = $postResp | ConvertTo-Json

    Write-Host $postResp
    $resp = Invoke-RestMethod -Method 'Post' -Headers $headers -Body $postResp -Uri $apiURI -MaximumRetryCount 3
    $resp | Write-Verbose
  }
  catch {
    Write-Error "Invoke-RestMethod $apiURI failed with exception:`n$_"
    $errorOccurred = $true
  }

  return $errorOccurred
}

function AddReviewers ($prNumber) {
  $uri = "$baseURI/$RepoOwner/$RepoName/pulls/$prNumber/requested_reviewers"
  if ($userAdditions) {
    $errorsOccurredAddingUsers = AddMembers -apiURI $uri -memberName "reviewers" -additionSet $userAdditions
    if ($errorsOccurredAddingUsers) { exit 1 }
    Write-Host -f green "User(s) [$userAdditions] added to: https://github.com/$RepoOwner/$RepoName/issue/$prNumber"
  }
  if ($teamAdditions) {
    $errorsOccurredAddingTeams = AddMembers -apiURI $uri -memberName "team_reviewers" -additionSet $teamAdditions
    if ($errorsOccurredAddingTeams) { exit 1 }
    Write-Host -f green "Team(s) [$teamAdditions] added to: https://github.com/$RepoOwner/$RepoName/issue/$prNumber"
  }
}

function AddAssignees ($prNumber) {
  $uri = "$baseURI/$RepoOwner/$RepoName/issues/$prNumber"
  if ($assigneeAdditions) {
    $errorsOccurredAddingUsers = AddMembers -apiURI $uri -memberName "assignees" -additionSet $assigneeAdditions
    if ($errorsOccurredAddingUsers) { exit 1 }
    Write-Host -f green "Users(s) [$assigneeAdditions] added to: https://github.com/$RepoOwner/$RepoName/issue/$prNumber"
  }
}

function AddLabels ($prNumber) {
  $uri = "$baseURI/$RepoOwner/$RepoName/issues/$prNumber"
  if ($labelAdditions) {
    $errorsOccurredAddingUsers = AddMembers -apiURI $uri -memberName "labels" -additionSet $labelAdditions
    if ($errorsOccurredAddingUsers) { exit 1 }
    Write-Host -f green "Label(s) [$labelAdditions] added to: https://github.com/$RepoOwner/$RepoName/issue/$prNumber"
  }
}

$query = "state=open&head=${PROwner}:${PRBranch}&base=${BaseBranch}"

try {
  $resp = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$RepoOwner/$RepoName/pulls?$query"
}
catch { 
  Write-Error "Invoke-RestMethod [https://api.github.com/repos/$RepoOwner/$RepoName/pulls?$query] failed with exception:`n$_"
  exit 1
}
$resp | Write-Verbose

if ($resp.Count -gt 0) {
    Write-Host -f green "Pull request already exists $($resp[0].html_url)"

    # setting variable to reference the pull request by number
    Write-Host "##vso[task.setvariable variable=Submitted.PullRequest.Number]$($resp[0].number)"
    AddLabels $resp[0].number $PRLabels
}
else {
  $data = @{
    title                 = $PRTitle
    head                  = "${PROwner}:${PRBranch}"
    base                  = $BaseBranch
    body                  = $PRBody
    maintainer_can_modify = $true
  }

  try {
    $resp = Invoke-RestMethod -Method POST -Headers $headers `
                              "https://api.github.com/repos/$RepoOwner/$RepoName/pulls" `
                              -Body ($data | ConvertTo-Json)
  }
  catch {
    Write-Error "Invoke-RestMethod [https://api.github.com/repos/$RepoOwner/$RepoName/pulls] failed with exception:`n$_"
    exit 1
  }

  $resp | Write-Verbose
  Write-Host -f green "Pull request created https://github.com/$RepoOwner/$RepoName/pull/$($resp.number)"

  # setting variable to reference the pull request by number
  Write-Host "##vso[task.setvariable variable=Submitted.PullRequest.Number]$($resp.number)"

  AddReviewers -prNumber $resp.number
  AddLabels -prNumber $resp.number
  AddAssignees -prNumber $resp.number
}
