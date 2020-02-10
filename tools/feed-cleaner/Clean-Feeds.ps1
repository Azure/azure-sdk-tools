param(
    [Parameter(Mandatory=$true)][string]$Organization,
    [Parameter(Mandatory=$true)][string]$FeedProject,
    [Parameter(Mandatory=$true)][string]$RunProject,
    [Parameter(Mandatory=$true)][string]$AccessToken    
    )
    
$ErrorActionPreference = "$Stop"
[RegEx]$expressionToExtractRunID = "^azure-sdk-(?<RunID>([0-9]+))$"

function Get-Feeds([string]$Organization, [string]$Project, [PSCredential]$Credential) {
    $url = "https://feeds.dev.azure.com/$Organization/$Project/_apis/packaging/feeds?api-version=4.1-preview"
    
    Write-Information "Requesting list of feeds from: $url"
    $response = Invoke-RestMethod -Uri $url -Method GET -Credential $Credential -Authentication Basic

    Write-Information "Found $($response.value.Length) feeds."

    return $response.value
}

function Get-FilteredFeeds([string]$Organization, [string]$Project, [PSCredential]$Credential) {
    $feeds = Get-Feeds -Organization $Organization -Project $Project -Credential $Credential

    # Filter the list of feeds to those in the form azure-sdk-{runid} as those are the ones
    # that we want to evaluate for deletion.
    Write-Information "Filtering down to burner feeds with the form azure-sdk-{runid}."
    $filteredFeeds = @($feeds | Where-Object { $_.name -match $expressionToExtractRunID })
    Write-Information "Found $($filteredFeeds.Length) burner feeds."

    return $filteredFeeds
}

function Get-Run([string]$Organization, [string]$Project, [int]$RunID, [PSCredential]$Credential) {
    $url = "https://dev.azure.com/$Organization/$Project/_apis/build/builds/$($RunID)?api-version=5.1"
    $response = Invoke-RestMethod -Uri $url -Method GET -Credential $Credential -Authentication Basic
    return $response
}

function Delete-Feed([string]$Organization, [string]$Project, $Feed, [PSCredential]$Credential) {
    $url = "https://feeds.dev.azure.com/$Organization/$Project/_apis/packaging/feeds/$($Feed.id)?api-version=5.1-preview.1"
    Write-Host "Deleting feed: $($Feed.id) ($($Feed.name))"
    $response = Invoke-RestMethod -Uri $url -Method DELETE -Credential $Credential -Authentication Basic
}

$password = ConvertTo-SecureString $AccessToken -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential ("nobody", $password)

$filteredFeeds = Get-FilteredFeeds -Organization $Organization -Project $FeedProject -Credential $credential

foreach ($filteredFeed in $filteredFeeds) {
    $match = $expressionToExtractRunID.Match($filteredFeed.name)
    $runID = [System.Int32]::Parse($match.Groups["RunID"].Value)

    $shouldDeleteFeed = $false
    try {
        $run = Get-Run -Organization $Organization -Project $RunProject -RunID $runID -Credential $credential
        $shouldDeleteFeed = $run.status -ne "inProgress"
    }
    catch {
        $shouldDeleteFeed = $true
    }

    if ($shouldDeleteFeed) {
        Delete-Feed -Organization $Organization -Project $FeedProject -Feed $filteredFeed -Credential $credential
    }
    else {
        Write-Host "Skipping: $($run._links.web)"
    }
}