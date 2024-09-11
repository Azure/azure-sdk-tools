param(
    [array]$BaseRepositories = @(
        'Azure/azure-sdk-tools'
        'Azure/azure-sdk'
        'Azure/azure-sdk-for-go'
        'Azure/azure-sdk-for-net'
        'Azure/azure-sdk-for-js'
        'Azure/azure-sdk-for-python'
        'Azure/azure-sdk-for-java'
        'Azure/azure-sdk-for-rust'
        'Azure/azure-sdk-for-c'
        'Azure/azure-sdk-for-cpp'
        'Azure/azure-sdk-for-ios'
        'Azure/azure-sdk-for-android'
        'Azure/azure-dev'
        'Azure/typespec-azure'
        'Microsoft/typespec'
    ),
    [array]$ExtraRepositories = @(
        'Azure/azure-rest-api-specs'
        'Azure/azure-rest-api-specs-pr'
    ),
    [int]$LookbackWeeks = 52,
    [int]$Limit = 6000
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

function Get-PRHistory([array]$_repositories, [int32]$_lookbackWeeks, [int]$_limit) {
    $calendar = New-Object System.Globalization.GregorianCalendar
    # Set week boundaries to Sunday and first day of year. See System.Globalization.CalendarWeekRule
    $currentWeek = $calendar.GetWeekOfYear((Get-Date), 0, 0)
    # Set lookback to # of weeks ago starting at previous week boundary (excluding current week)
    $_lookbackDays = $_lookbackWeeks * 7 + (get-date).DayOfWeek.value__
    $allPRs = @()

    foreach ($repo in $_repositories) {
        $prs = gh pr list -s all --limit $_limit --json author,createdAt,mergedAt --repo $repo
        $parsed = @($prs | ConvertFrom-Json -AsHashtable)
        Write-Host "Found $($parsed.Length) PRs for $repo"
        $allPRs += $parsed `
                    | ForEach-Object {
                        @{
                            'author' = $_.author.login
                            'created' = (Get-Date $_.createdAt)
                            'merged' = ($_.mergedAt -ne $null ? (Get-Date $_.mergedAt) : $null)
                        }
                    }
    }

    $negativeLookback = -$_lookbackDays
    $recentPRs = $allPRs | Where-Object { $_.created -gt ([datetime]::Now.AddDays($negativeLookback)) }

    return $recentPRs
}

function incrementBucket($hash, [object]$bucketKey) {
    # Cast key to object in case $hash is an ordered dictionary.
    # Ordered dictionaries treat int keys as an index lookup by default
    if ($hash.Contains([object]$bucketKey)) {
        $hash[[object]$bucketKey] = $hash[[object]$bucketKey] + 1
    } else {
        $hash[[object]$bucketKey] = 1
    }
}

function Get-DevAndPRCounts([array]$pullRequests) {
    $weekHash = [ordered]@{}  # make ordered for ease of printing results in sequence
    $authorHash = @{}

    foreach ($pr in $pullRequests) {
        $weekOfYear = $calendar.GetWeekOfYear($pr.created, 0, 0)
        # Ignore current week, first week, and last week as they may be incomplete or slow and will skew the average
        if ($weekOfYear -in @($currentWeek, 1, 52)) { continue }
        incrementBucket $weekHash $weekOfYear
        incrementBucket $authorHash $pr.author
    }

    $weeklySum = $weekHash.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    [int]$weeklyAverage = $weeklySum / $weekHash.Keys.Count  # cast to [int] for rounding
    $authorSum = $authorHash.Keys.Count

    return $weekHash, $weeklyAverage, $authorSum
}

function Get-PRCompletionTimeHours([array]$pullRequests) {
    $sumHours = 0
    foreach ($pr in $pullRequests) {
        if (!$pr.merged) {
            continue
        }
        $sumHours += ($pr.merged - $pr.created).TotalHours
    }

    $averageHours = ($sumHours / $pullRequests.Count) -as [int]
    return $averageHours, ($averageHours / 24)
}

$recentPRsBase = Get-PRHistory $BaseRepositories $LookbackWeeks $Limit
$weekHashBase, $weeklyAverageBase, $authorSumBase = Get-DevAndPRCounts $recentPRsBase
$completionHoursBase, $completionDaysBase = Get-PRCompletionTimeHours $recentPRsBase

if ($ExtraRepositories) {
    $recentPRsExtra = Get-PRHistory $ExtraRepositories $LookbackWeeks $Limit
    $weekHashExtra, $weeklyAverageExtra, $authorSumExtra = Get-DevAndPRCounts $recentPRsExtra
    $completionHoursExtra, $completionDaysExtra = Get-PRCompletionTimeHours $recentPRsExtra

    $weekHashAll, $weeklyAverageAll, $authorSumAll = Get-DevAndPRCounts ($recentPRsBase + $recentPRsExtra)
    $completionHoursAll, $completionDaysAll = Get-PRCompletionTimeHours ($recentPRsBase + $recentPRsExtra)
}

Write-Host "-> PRs per week - Base repos [week, count]"
$msg = ""
foreach ($key in $weekHashBase.Keys) {
    $msg += "[$key, $($weekHashBase[[object]$key])] "
}
Write-Host $msg

if ($ExtraRepositories) {
    Write-Host "-> PRs per week - Extra repos [week, count]"
    $msg = ""
    foreach ($key in $weekHashBase.Keys) {
        $msg += "[$key, $($weekHashExtra[[object]$key])] "
    }
    Write-Host $msg
    Write-Host "-> PRs per week - All repos [week, count]"
    $msg = ""
    foreach ($key in $weekHashBase.Keys) {
        $msg += "[$key, $($weekHashAll[[object]$key])] "
    }
    Write-Host $msg
}
Write-Host "-> Average PRs per week"
Write-Host "Base repos: $weeklyAverageBase"
if ($ExtraRepositories) {
    Write-Host "Extra repos: $weeklyAverageExtra"
    Write-Host "All repos: $weeklyAverageAll"
}
Write-Host "-> Total authors"
Write-Host "Base repos: $authorSumBase"
if ($ExtraRepositories) {
    Write-Host "Extra repos: $authorSumExtra"
    Write-Host "All repos: $authorSumAll"
}
Write-Host "-> Average completion time (hours)"
Write-Host "Base repos: $completionHoursBase"
if ($ExtraRepositories) {
    Write-Host "Extra repos: $completionHoursExtra"
    Write-Host "All repos: $completionHoursAll"
}
Write-Host "-> Average completion time (days)"
Write-Host "Base repos: $completionDaysBase"
if ($ExtraRepositories) {
    Write-Host "Extra repos: $completionDaysExtra"
    Write-Host "All repos: $completionDaysAll"
}
