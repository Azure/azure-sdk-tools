param(
    [Parameter(Mandatory = $true)]
    $AuthToken,

    [Parameter(Mandatory = $true)]
    $PRDataArtifactPath
)

$PRObjs = [System.Collections.ArrayList]::new()
$MergedPRs = [System.Collections.ArrayList]::new()
$ClosedPRs = [System.Collections.ArrayList]::new()
$UnexpectedState = [System.Collections.ArrayList]::new()
$OpenAndCleanPRs = [System.Collections.ArrayList]::new()
$OpenButDirtyPRs = [System.Collections.ArrayList]::new()


$GitHubAPIUrlBase = "https://api.github.com/repos/"
$Headers = @{
    Authorization = "bearer $AuthToken"
}

function Merge-PRs()
{
    if (($OpenButDirtyPRs.Count -gt 0) -or ($ClosedPRs.Count -gt 0) -or ($UnexpectedState.Count -gt 0))
    {
        Write-Output "The following PRs are either closed, or in an Un-mergeable state"
        Write-Output "If Closed, Please investigate why a Sync PR was closed prematurely."
        Write-Output "Ensure all checks are green and pr is approved. Merge them manually or run the sync pipeline again"

        foreach ($obj in $OpenButDirtyPRs)
        {
            Write-Output "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Un-Mergable"
        }

        foreach ($obj in $ClosedPRs)
        {
            Write-Output "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Closed"
        }

        foreach ($obj in $UnexpectedState)
        {
            Write-Output "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber): State: Unexpected"
        }
        exit 1
    }

    Write-Output "Will merge the Following PRs"
    foreach ($obj in $OpenAndCleanPRs)
    {
        $MergeAPIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)/merge"
        $CommitsAPIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)/commits"

        $data = @{
            commit_title = ""
            commit_message = ""
            sha = $obj.HeadSHA
            merge_method = "squash"
        }

        # Get latest Commit of the pull request
        try 
        {
            $response = Invoke-RestMethod -Method Get -Headers $Headers $CommitsAPIUrl
            $data.commit_message = $response[$response.Count - 1].commit.message # last commit message of the pull request
            $data.commit_title = "Merge pull request #$($obj.PRNumber) from $($obj.HeadLabel)"
        }
        catch 
        {
            Write-Error "Invoke-RestMethod [$CommitsAPIUrl] failed with exception:`n$_"
            exit 1
        }

        # Merge Pull Request
        try 
        {
            #$response = Invoke-RestMethod -Method Put -Headers $Headers $MergeAPIUrl -Body ($data | ConvertTo-Json)
            Write-Host $MergeAPIUrl
            ($data | Format-Table | Write-Output)
        }
        catch 
        {
            Write-Error "Invoke-RestMethod [$MergeAPIUrl] failed with exception:`n$_"
            exit 1
        }
    }
}

function Confirm-Mergability()
{
    foreach ($obj in $PRObjs)
    {
        $APIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)"
        try
        {
            $response = Invoke-RestMethod -Headers $Headers $APIUrl
            if ($response.merged)
            {
                [void]$MergedPRs.Add($obj)
            }
            elseif ($response.state -eq "closed")
            {
                [void]$ClosedPRs.Add($obj)
            }
            else 
            {
                if ($response.mergeable_state -ne "clean")
                {
                    [void]$OpenButDirtyPRs.Add($obj)
                }
                elseif ($response.mergeable -and ($response.mergeable_state -eq "clean"))
                {
                    $obj | Add-Member -MemberType NoteProperty -Name "HeadSHA" -Value $response.head.sha
                    $obj | Add-Member -MemberType NoteProperty -Name "HeadLabel" -Value $response.head.label
                    [void]$OpenAndCleanPRs.Add($obj)
                }
                else 
                {
                    [void]$UnexpectedState.Add($obj)
                }
            }
        }
        catch
        {
            Write-Error "Invoke-RestMethod ${APIUrl} failed with exception:`n$_"
            exit 1
        }
    }
}
function Get-PRDataFromFile()
{
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
}

Get-PRDataFromFile
Confirm-Mergability
Merge-PRs
