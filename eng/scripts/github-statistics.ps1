param(
    [array]$Repositories = @(
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
        'Azure/azure-rest-api-specs'
        'Azure/typespec-azure'
        'Microsoft/typespec'
    ),
    [int]$LookbackWeeks = 52,
    [int]$Limit = 5000
)

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
        $prs = gh pr list -s all --limit $_limit --json author,createdAt --repo $repo
        $parsed = @($prs | ConvertFrom-Json -AsHashtable)
        Write-Host "Found $($parsed.Length) PRs for $repo"
        $allPRs += $parsed | % { @{ 'author' = $_.author.login; 'created' = (Get-Date $_.createdAt) } }
    }

    $negativeLookback = -$_lookbackDays
    $recentPRs = $allPRs | ? { $_.created -gt ([datetime]::Now.AddDays($negativeLookback)) }

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

function Get-DevAndPRCounts([array]$_repositories, [int32]$_lookbackWeeks, [int]$_limit) {
    $weekHash = [ordered]@{}
    $authorHash = @{}

    $recentPRs = Get-PRHistory $_repositories $_lookbackWeeks $_limit

    foreach ($pr in $recentPRs) {
        $weekOfYear = $calendar.GetWeekOfYear($pr.created, 0, 0)
        # Ignore current week, first week, and last week as they may be incomplete or slow and will skew the average
        if ($weekOfYear -in @($currentWeek, 1, 52)) { continue }
        incrementBucket $weekHash $weekOfYear
        incrementBucket $authorHash $pr.author
    }

    $weeklySum = $weekHash.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    [int]$weeklyAverage = $weeklySum / $weekHash.Keys.Count  # cast to [int] for rounding
    $authorSum = $authorHash.Keys.Count

    write-host $authorHash.Keys
    return $weekHash, $weeklyAverage, $authorSum
}

$weekHash, $weeklyAverage, $authorSum = Get-DevAndPRCounts $Repositories $LookbackWeeks $Limit
Write-Host "-> PRs per week"
Write-Host $weekHash
Write-Host "-> Average PRs per week"
Write-Host $weeklyAverage
Write-Host "-> Total authors"
Write-Host $authorSum
