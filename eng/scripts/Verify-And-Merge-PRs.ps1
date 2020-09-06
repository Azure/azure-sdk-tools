param(
  $PRDataArtifactPath,
  $AuthToken,
  $ShouldMerge,
  [switch]$devOpsLogging = $false
)

function LogWarning
{
  if ($devOpsLogging) {
    Write-Host "##vso[task.LogIssue type=warning;]$args"
  }
  else {
    Write-Warning "$args"
  }
}

function LogError
{
  if ($devOpsLogging) {
    Write-Host "##vso[task.logissue type=error]$args"
  }
  else {
    Write-Error "$args"
  }
}

$ReadyForMerge = $true
$mergablePRs = @()
$headers = @{ }

if ($null -eq $ShouldMerge) {
  $ShouldMerge = $null -ne $AuthToken;
}

if ($AuthToken) {
  $headers = @{
    Authorization = "bearer $AuthToken"
  }
}

$PRData = Get-Content $PRDataArtifactPath
# Confirm Mergability
foreach ($prDataLine in $PRData)
{
  $repoOwner, $repoName, $prNumber = $prDataLine.Split(";")

  $prApiUrl = "https://api.github.com/repos/${repoOwner}/${repoName}/pulls/${prNumber}"
  $prUrl = "https://github.com/${repoOwner}/${repoName}/pull/${prNumber}"
  
  try
  {
    $response = Invoke-RestMethod -Headers $headers $prApiUrl
    if ($response.merged) {
      Write-Host "${prUrl} is merged."
    }
    elseif ($response.state -eq "closed") {
      LogWarning "${prUrl} is closed. Please investigate why was not merged."
      $ReadyForMerge = $false
    }
    elseif ($response.mergeable -and $response.mergeable_state -eq "clean") {
      Write-Host "${prUrl} is ready to merge."

      $mergablePRs += @{ Url = $prApiUrl; HeadSHA = $response.head.sha }
    }
    elseif ($response.mergeable_state -ne "clean") {
      LogWarning "${prUrl} is blocked ($($response.mergeable_state)). Please ensure all checks are green and reviewers have approved."
      $ReadyForMerge = $false
    }
    else {
      LogWarning "${prUrl} is in an unknown state please contact engineering system team to understand the state."
      LogWarning $response
      $ReadyForMerge = $false
    }
  }
  catch {
    LogError "Invoke-RestMethod ${prApiUrl} failed with exception:`n$_"
    exit 1
  }
}

if (!$ReadyForMerge) {
  LogError "At least one sync PR is not able to be merged please investigate and then retry running this job to auto-merge them again"
  exit 1
}

if ($ReadyForMerge -and $ShouldMerge)
{
  # Merge Pull Requests
  foreach ($mergablePRObj in $mergablePRs)
  {
    $mergablePR = $mergablePRObj.Url
    $mergeApiUrl = $mergablePR + "/merge"

    Write-Host $mergablePRObj.HeadSHA
    $data = @{
      sha = $mergablePRObj.HeadSHA
      merge_method = "squash"
    }

    # Merge Pull Request
    try {
      Write-Host "Merging $mergablePR"
      $response = Invoke-RestMethod -Method Put -Headers $headers $mergeApiUrl -Body ($data | ConvertTo-Json)
    }
    catch {
      LogError "Invoke-RestMethod [$mergeApiUrl] failed with exception:`n$_"
      exit 1
    }
  }
}