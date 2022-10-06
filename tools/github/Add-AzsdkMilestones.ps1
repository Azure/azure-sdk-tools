[CmdletBinding(DefaultParameterSetName = 'Repositories', SupportsShouldProcess = $true)]
param (
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
    [string[]] $Languages = @('cpp', 'go', 'java', 'js', 'net', 'python'),

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

$date = $StartDate
$milestones = do {
    # Start with the first of the month at 23:59 UTC.
    $date = [DateTimeOffset]::Parse("$($date.ToString('yyyy-MM'))-01T23:59:59Z")

    # The end date is always the first Friday of next month.
    $next = $date.AddMonths(1)
    while ($next.DayOfWeek -ne 5) {
        $next = $next.AddDays(1)
    }
    [pscustomobject]@{
        Title = $date.ToString("yyyy-MM")
        Description = $date.ToString("MMMM yyyy")
        DueOn = $next.ToString('s') + 'Z'
    }

    $date = $next
} while ($date -lt $EndDate)

if (!$milestones) {
    Write-Warning "No milestones to create between start and end dates"
    return
}

foreach ($repo in $Repositories) {
    foreach ($m in $milestones) {
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
    }
}

<#
.SYNOPSIS
Creates Azure SDK milestones in the form of "yyyy-MM".
.DESCRIPTION
Creates Azure SDK milestones in the form of "yyyy-MM" with the due date set to the first Friday of the following month at 11:59 PM UTC.
.PARAMETER Repositories
The GitHub repositories to update.
.PARAMETER Languages
The Azure SDK languages to query for milestones e.g., "net" for "Azure/azure-sdk-for-net".
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
