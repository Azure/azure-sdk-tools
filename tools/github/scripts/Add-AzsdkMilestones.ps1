[CmdletBinding(DefaultParameterSetName = 'RepositoryFile', SupportsShouldProcess = $true)]
param (
    [Parameter(ParameterSetName = 'RepositoryFile')]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string]$RepositoryFilePath = "$PSScriptRoot/../data/repositories.txt",

    [Parameter(ParameterSetName = 'Repositories')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Repositories = @(
        'Azure/azure-sdk-for-cpp'
        'Azure/azure-sdk-for-go'
        'Azure/azure-sdk-for-java'
        'Azure/azure-sdk-for-js'
        'Azure/azure-sdk-for-net'
        'Azure/azure-sdk-for-python'
        'Azure/azure-sdk-tools'
    ),

    [Parameter(ParameterSetName = 'Languages')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Languages = @('cpp', 'go', 'java', 'js', 'net', 'python', 'c', 'ios', 'android'),

    [Parameter()]
    [DateTimeOffset] $StartDate = [DateTimeOffset]::Now,

    [Parameter()]
    [DateTimeOffset] $EndDate = [DateTimeOffset]::Now.AddMonths(6),

    [Parameter()]
    [switch] $Force
)

if (!(Get-Command -Type Application gh -ErrorAction Ignore)) {
    throw 'You must first install the GitHub CLI: https://github.com/cli/cli/tree/trunk#installation'
}

if ($PSCmdlet.ParameterSetName -eq 'Languages') {
    $Repositories = foreach ($lang in $Languages) {
        "Azure/azure-sdk-for-$lang"
    }
}

if ($PSCmdlet.ParameterSetName -eq 'RepositoryFile') {
    $Repositories = Get-Content $RepositoryFilePath
}

$date = $StartDate
[pscustomobject[]] $milestones = do {
    # Start with the first of the month at 23:59 UTC.
    $date = [DateTimeOffset]::Parse("$($date.ToString('yyyy-MM'))-01T23:59:59Z")

    # The end date is always the first Friday of the month.
    while ($date.DayOfWeek -ne 5) {
        $date = $date.AddDays(1)
    }
    [pscustomobject]@{
        Title = $date.ToString("yyyy-MM")
        Description = $date.ToString("MMMM yyyy")
        DueOn = $date.ToString('s') + 'Z'
    }

    # The next date to consider is the following month.
    $date = $date.AddMonths(1)
} while ($date -lt $EndDate)

if (!$milestones) {
    Write-Warning "No milestones to create between start and end dates"
    return
}

$completed = 0
$total = $Repositories.Length * $milestones.Length
Write-Progress -Activity ($activity = 'Creating milestones') -PercentComplete 0

foreach ($repo in $Repositories) {
    foreach ($m in $milestones) {
        Write-Progress -Activity $activity -Status "In $repo" -CurrentOperation "Milestone $($m.Title)" -PercentComplete ($completed / $total * 100)
        if ($Force -or $PSCmdlet.ShouldProcess(
            "Creating milestone $($m.Title) ending $($m.DueOn) in $repo",
            "Create milestone $($m.Title) ending $($m.DueOn) in $repo?",
            "Create milestone")) {
            $result = gh api repos/$repo/milestones -f title="$($m.Title)" -f description="$($m.Description)" -f due_on="$($m.DueOn)" 2>$null | ConvertFrom-Json
            if ($LASTEXITCODE) {
                if ($result.errors.code -contains "already_exists") {
                    Write-Verbose "Warning: milestone $($m.Title) already exists in $repo"
                } else {
                    Write-Error "Failed to create milestone $($m.Title) in ${repo}: $($result.message)"
                }
            } else {
                Write-Verbose "Created $($result.url) for milestone $($m.Title) in $repo"
            }
        }
        $completed++
    }
}
Write-Progress -Activity $activity -Completed

<#
.SYNOPSIS
Creates Azure SDK milestones in the form of "yyyy-MM".

.DESCRIPTION
Creates Azure SDK milestones in the form of "yyyy-MM" with the due date set to the first Friday of the following month at 11:59 PM UTC.

.PARAMETER Repositories
The GitHub repositories to update.

.PARAMETER Languages
The Azure SDK languages to query for milestones e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to update.

.PARAMETER StartDate
The starting date for new milestones.

.PARAMETER EndDate
The end date for new milestones.

.PARAMETER Force
Create milestones for each repository without prompting.

.EXAMPLE
Add-AzsdkMilestones.ps1 -WhatIf
See how many milestones may be created for each repository without actually adding them.
#>
