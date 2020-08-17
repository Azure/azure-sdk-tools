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
# at least one of label needs to be populated
if (-not $PRLabel) {
    Write-Host "No labels provided for addition, exiting."
    exit 0
}

# Add labels to the pull request
$prLabels = @($PRLabel.Split(",") | % { $_.Trim() } | ? { return $_ })
$uri = "https://api.github.com/repos/$RepoOwner/$RepoName/issues/$PRNumber"
$data = @{
    maintainer_can_modify = $true
    labels                = $prLabels
}
$headers = @{
    Authorization = "bearer $AuthToken"
}
    
try {
    $resp = Invoke-RestMethod -Method PATCH -Headers $headers $uri -Body ($data | ConvertTo-Json)
}
catch {
    Write-Error "Invoke-RestMethod $uri failed with exception:`n$_"
    exit 1
}

$resp | Write-Verbose
Write-Host -f green "Label added to pull request: https://github.com/$RepoOwner/$RepoName/pull/$PRNumber"

# setting variable to reference the pull request by number
Write-Host "##vso[task.setvariable variable=Submitted.PullRequest.Number]$PRNumber"                                                                       
