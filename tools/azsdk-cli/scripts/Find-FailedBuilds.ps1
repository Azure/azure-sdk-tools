[CmdletBinding()]
param(
    [string]$Owner = "Azure",

    [Parameter(Mandatory)]
    [string[]]$Repo,

    [int]$Count = 5,

    [ValidateSet("pullRequest", "individualCI", "batchedCI", "manual", "all")]
    [string]$Reason = "pullRequest",

    [string]$DefinitionName,

    [switch]$Analyze,

    [switch]$Save,

    [string]$OutputDir = (Join-Path $PSScriptRoot "results")
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$baseUrl = "https://dev.azure.com/azure-sdk/public/_apis/build/builds"

$resultsDir = $null
if ($Save) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $resultsDir = Join-Path $OutputDir "Results-$timestamp"
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
    Write-Host "Saving results to: $resultsDir" -ForegroundColor Green
}

foreach ($repoName in $Repo) {
    Write-Host "`n=== $Owner/$repoName ===" -ForegroundColor Cyan

    $queryParams = @(
        "repositoryId=$Owner/$repoName"
        "repositoryType=GitHub"
        "resultFilter=failed"
        "statusFilter=completed"
        "queryOrder=finishTimeDescending"
        "`$top=$Count"
        "api-version=7.1"
    )

    if ($Reason -ne "all") {
        $queryParams += "reasonFilter=$Reason"
    }

    $query = $queryParams -join '&'
    $uri = "$baseUrl`?$query"

    Write-Host "Querying: $uri" -ForegroundColor DarkGray
    $response = Invoke-RestMethod -Uri $uri

    if ($response.value.Count -eq 0) {
        Write-Warning "No failed builds found for $Owner/$repoName (reason: $Reason)"
        continue
    }

    $builds = foreach ($build in $response.value) {
        if ($DefinitionName -and $build.definition.name -notlike "*$DefinitionName*") {
            continue
        }

        $timelineUri = "$baseUrl/$($build.id)/timeline?api-version=7.1"
        try {
            $timeline = Invoke-RestMethod -Uri $timelineUri
            $failedTasks = $timeline.records | Where-Object {
                $_.result -eq "failed" -and $_.type -eq "Task"
            }
            if (-not $failedTasks) {
                Write-Host "  Skipping $($build.id) ($($build.definition.name)) - no failed tasks in timeline" -ForegroundColor DarkGray
                continue
            }
        } catch {
            Write-Warning "  Could not fetch timeline for $($build.id): $_"
        }

        [PSCustomObject]@{
            BuildId    = $build.id
            Definition = $build.definition.name
            FinishTime = $build.finishTime
            Reason     = $build.reason
            Link       = "https://dev.azure.com/azure-sdk/public/_build/results?buildId=$($build.id)&view=results"
        }
    }

    if (-not $builds) {
        Write-Warning "No builds matched filter for $Owner/$repoName"
        continue
    }

    $builds | Format-Table -AutoSize

    if ($Analyze) {
        $cliProject = Join-Path $PSScriptRoot ".." "Azure.Sdk.Tools.Cli"
        foreach ($build in $builds) {
            Write-Host "`n--- Analyzing Build $($build.BuildId): $($build.Definition) ---" -ForegroundColor Cyan
            $output = & dotnet run --project $cliProject -- azp analyze $build.Link 2>&1
            $output | Write-Host

            if ($Save -and $resultsDir) {
                $safeName = ($build.Definition -replace '[^a-zA-Z0-9_\-]', '_')
                $fileName = "$($build.BuildId)_$safeName.txt"
                $filePath = Join-Path $resultsDir $fileName
                $content = @(
                    "Build ID:    $($build.BuildId)"
                    "Definition:  $($build.Definition)"
                    "Finished:    $($build.FinishTime)"
                    "Reason:      $($build.Reason)"
                    "Link:        $($build.Link)"
                    "Repo:        $Owner/$repoName"
                    ""
                    "--- Analysis Output ---"
                    ""
                    ($output -join "`n")
                ) -join "`n"
                Set-Content -Path $filePath -Value $content
                Write-Host "  Saved to: $filePath" -ForegroundColor DarkGray
            }

            Write-Host ""
        }
    } elseif ($Save -and $resultsDir) {
        # Save build list even without analysis
        foreach ($build in $builds) {
            $safeName = ($build.Definition -replace '[^a-zA-Z0-9_\-]', '_')
            $fileName = "$($build.BuildId)_$safeName.txt"
            $filePath = Join-Path $resultsDir $fileName
            $content = @(
                "Build ID:    $($build.BuildId)"
                "Definition:  $($build.Definition)"
                "Finished:    $($build.FinishTime)"
                "Reason:      $($build.Reason)"
                "Link:        $($build.Link)"
                "Repo:        $Owner/$repoName"
            ) -join "`n"
            Set-Content -Path $filePath -Value $content
            Write-Host "  Saved to: $filePath" -ForegroundColor DarkGray
        }
    }
}
