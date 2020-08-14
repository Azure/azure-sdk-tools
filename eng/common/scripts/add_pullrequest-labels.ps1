param(
    [Parameter(Mandatory = $true)]
    $RepoOwner,

    [Parameter(Mandatory = $true)]
    $RepoName,

    [Parameter(Mandatory = $true)]
    $PRNumber,
  
    [Parameter(Mandatory = $true)]
    $AuthToken,

    [Parameter(Mandatory = $false)]
    $PRLabel
)

# Add labels to the pull request
if ($PRLabel -ne "") {
    $uri = "https://api.github.com/repos/$RepoOwner/$RepoName/issues/$PRNumber"
    $data = @{
        maintainer_can_modify = $true
        labels                = $PRLabel
    }
    try {
        $resp = Invoke-RestMethod -Method PATCH -Headers $headers $uri -Body ($data | ConvertTo-Json)
    }
    catch {
        Write-Error "Invoke-RestMethod $uri failed with exception:`n$_"
        exit 1
    }

    $resp | Write-Verbose
    Write-Host -f green "Label added to pull request: https://github.com/$RepoOwner/$RepoName/pull/$($resp.number)"

    # setting variable to reference the pull request by number
    Write-Host "##vso[task.setvariable variable=Submitted.PullRequest.Number]$($resp.number)"                                                                       
}