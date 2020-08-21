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

    foreach ($obj in $OpenAndCleanPRs)
    {
        $APIUrl = "${GitHubAPIUrlBase}$($obj.RepoOwner)/$($obj.RepoName)/pulls/$($obj.PRNumber)/merge"
        Write-Output "Will merge the Following PRs"
        Write-Output "`thttps://github.com/$($obj.RepoOwner)/$($obj.RepoName)/pull/$($obj.PRNumber)"
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
                    [void]$OpenButDirtyPRs.Add($obj)
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
        Write-Output ($PRDataObj | Format-Table | Out-String)
        [void]$PRObjs.Add($PRDataObj)
    }
}

Get-PRDataFromFile
Confirm-Mergability
Merge-PRs
