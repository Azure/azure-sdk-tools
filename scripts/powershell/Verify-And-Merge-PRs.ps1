param(
    [Parameter(Mandatory = $true)]
    $AuthToken,

    [Parameter(Mandatory = $true)]
    $PRDataArtifactPath,

    [switch]$devOpsLogging = $false
)

function LogWarning
{
  if ($devOpsLogging)
  {
    Write-Host "##vso[task.LogIssue type=warning;]$args"
  }
  else
  {
    Write-Warning "$args"
  }
}

function LogError
{
  if ($devOpsLogging)
  {
    Write-Host "##vso[task.logissue type=error]$args"
  }
  else
  {
    Write-Error "$args"
  }
}

function LogInfo
{
  if ($devOpsLogging)
  {
    Write-Host "##vso[task.logissue type=debug]$args"
  }
  else
  {
    Write-Error "$args"
  }
}


$PRObjs = [System.Collections.ArrayList]::new()
$ClosedPRs = [System.Collections.ArrayList]::new()
$UnexpectedState = [System.Collections.ArrayList]::new()
$MergablePRs = [System.Collections.ArrayList]::new()
$BlockedPRs = [System.Collections.ArrayList]::new()


$GitHubAPIUrlBase = "https://api.github.com/repos/"
$Headers = @{
    Authorization = "bearer $AuthToken"
}

# Get PRData from File
$PRData = Get-Content $PRDataArtifactPath
foreach ($line in $PRData)
{
    $PRDetails = $line.Split(';')
    $PRDataObj = [PSCustomObject]@{
        RepoOwner = $PRDetails[0]
        RepoName = $PRDetails[1]
        PRNumber = $PRDetails[2]
    }
    [void]$PRObjs.Add($PRDataObj)
}

# Confirm Mergability
foreach ($obj in $PRObjs)
{
    $APIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)"
    try
    {
        $response = Invoke-RestMethod -Headers $Headers $APIUrl
        if ($response.merged)
        {
            LogInfo "PR $($obj.PRNumber) in Repo $($obj.RepoName) is Merged"
            continue
        }
        elseif ($response.state -eq "closed")
        {
            LogInfo "PR $($obj.PRNumber) in Repo $($obj.RepoName) is Closed"
            [void]$ClosedPRs.Add($obj)
        }
        else 
        {
            if ($response.mergeable_state -ne "clean")
            {
                LogInfo "PR $($obj.PRNumber) in Repo $($obj.RepoName) is Blocked"
                [void]$BlockedPRs.Add($obj)
            }
            elseif ($response.mergeable -and ($response.mergeable_state -eq "clean"))
            {
                $obj | Add-Member -MemberType NoteProperty -Name "HeadSHA" -Value $response.head.sha
                $obj | Add-Member -MemberType NoteProperty -Name "HeadLabel" -Value $response.head.label
                LogInfo "PR $($obj.PRNumber) in Repo $($obj.RepoName) is MergablePR"
                [void]$MergablePRs.Add($obj)
            }
            else 
            {
                $obj | Add-Member -MemberType NoteProperty -Name "Response" -Value $response
                LogInfo "PR $($obj.PRNumber) in Repo $($obj.RepoName) is in an UnexpectedState"
                [void]$UnexpectedState.Add($obj)
            }
        }
    }
    catch
    {
        LogError "Invoke-RestMethod ${APIUrl} failed with exception:`n$_"
        exit 1
    }
}

# Verify and Report all Unmergable PRS
if (($BlockedPRs.Count -gt 0) -or ($ClosedPRs.Count -gt 0) -or ($UnexpectedState.Count -gt 0))
{
    if ($BlockedPRs.Count -gt 0)
    {
        LogWarning "The following PRs are Blocked, Please ensure all checks are green and PR is approved"
        foreach ($obj in $BlockedPRs)
        {
            LogWarning "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Blocked"
        }
    }

    if ($ClosedPRs.Count -gt 0)
    {
        LogWarning "The following PRs are Closed, Please investigate why a Sync PR was closed prematurely without being merged."
        foreach ($obj in $ClosedPRs)
        {
            LogWarning "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Closed"
        }
    }

    if ($UnexpectedState.Count -gt 0)
    {
        LogWarning "The following PRs are Unexpected state, Please contact EngSys team"
        foreach ($obj in $UnexpectedState)
        {
            LogWarning "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Unexpected"
            LogWarning $obj.Response
        }
    }

    LogError "At least one sync PR is not able to be merged please investigate and then retry running this job to auto-merge them again"
    exit 1
}

# Merge Pull Requests
foreach ($obj in $MergablePRs)
{
    $MergeAPIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)/merge"

    $data = @{
        sha = $obj.HeadSHA
        merge_method = "squash"
    }

    # Merge Pull Request
    try 
    {
        LogInfo "Merging $($obj.PRNumber) in Repo $($obj.RepoName)"
        $response = Invoke-RestMethod -Method Put -Headers $Headers $MergeAPIUrl -Body ($data | ConvertTo-Json)
    }
    catch 
    {
        LogWarning "Could not merge https://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber)"
        LogError "Invoke-RestMethod [$MergeAPIUrl] failed with exception:`n$_"
        exit 1
    }
}