<#
.SYNOPSIS
    Checks the SLA status of Azure SDK API-drop doc pipelines (main builds only).
.DESCRIPTION
    Queries Azure DevOps for the most recent "main" build triggered by the
    azure-sdk-internal-msdocs-apidrop-connection identity across all monitored
    pipelines. Outputs a summary table and a list of failures organized by language.
.NOTES
    Requires: az CLI, signed in with access to the apidrop Azure DevOps org.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# ── Configuration ────────────────────────────────────────────────────────────
$Org        = "apidrop"
$Project    = "Content%20CI"
$Identity   = "azure-sdk-internal-msdocs-apidrop-connection"
$ApiVersion = "7.0"

# Pipeline definitions: Id → Language
$Pipelines = @(
    @{ Id = 8056; Language = "C++"    }
    @{ Id = 397;  Language = ".NET"   }
    @{ Id = 3188; Language = "Java"   }
    @{ Id = 3452; Language = "JS"   }
    @{ Id = 5533; Language = "Python" }
)

# ── Helpers ──────────────────────────────────────────────────────────────────
function Get-AzDoToken {
    $tok = az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query accessToken -o tsv 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to get Azure DevOps token. Ensure 'az' is signed in." }
    return $tok
}

function Invoke-AzDoApi {
    param([string]$Path, [hashtable]$Query = @{})
    $qs = ($Query.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
    $url = "https://dev.azure.com/$Org/$Project/_apis$Path`?api-version=$ApiVersion"
    if ($qs) { $url += "&$qs" }
    Invoke-RestMethod -Uri $url -Headers $script:Headers
}

function Test-IsMainBuild {
    <#
    .SYNOPSIS
        Returns $true if the build targets the "main" branch.
    #>
    param($Build)

    if (-not $Build.parameters) { return $true }  # C++ has no params, always main

    try {
        $outer = $Build.parameters | ConvertFrom-Json
        $inner = $outer.params | ConvertFrom-Json
        $branch = $null

        if ($inner.target_repo -and $inner.target_repo.branch) {
            $branch = $inner.target_repo.branch
        }
        if (-not $branch -and $inner.source_repo -and $inner.source_repo.branch) {
            $branch = $inner.source_repo.branch
        }

        if ($branch -match "^daily/") { return $false }
        return $true
    }
    catch {
        Write-Warning "Could not parse parameters for build $($Build.id): $_"
        return $false
    }
}

# ── Main ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  API-Drop Pipeline SLA Dashboard" -ForegroundColor Cyan
Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
Write-Host ("  " + ("─" * 55)) -ForegroundColor DarkGray

# Authenticate
$token = Get-AzDoToken
$script:Headers = @{ Authorization = "Bearer $token" }

# Collect results
$results  = @()
$failures = @()

foreach ($pipe in $Pipelines) {
    $defId = $pipe.Id
    $lang  = $pipe.Language

    Write-Host "`n  Querying $lang (definition $defId)..." -ForegroundColor DarkGray

    # Fetch builds until we find the most recent main build
    $foundMain = $null
    $page      = 0
    $batchSize = 10

    while ($true) {
        $skip   = $page * $batchSize
        $builds = Invoke-AzDoApi "/build/builds" @{
            'definitions'  = $defId
            'requestedFor' = $Identity
            '$top'         = $batchSize
            '$skip'        = $skip
        }

        if ($builds.count -eq 0 -or $builds.value.Count -eq 0) { break }

        foreach ($b in $builds.value) {
            if (Test-IsMainBuild $b) {
                $foundMain = [PSCustomObject]@{
                    Language  = $lang
                    BuildId   = $b.id
                    Result    = $b.result
                    QueueTime = ([datetime]$b.queueTime).ToString("yyyy-MM-dd HH:mm")
                    Url       = $b._links.web.href
                }
                break
            }
        }

        if ($foundMain) { break }
        $page++
        if ($page -gt 5) {
            Write-Warning "  Searched 60 builds for $lang without finding a main build."
            break
        }
    }

    if ($foundMain) { $results += $foundMain }

    if ($foundMain -and $foundMain.Result -ne 'succeeded') {
        $failures += $foundMain
    }
}

# ── Status Table ─────────────────────────────────────────────────────────────
Write-Host ""
Write-Host ""
Write-Host "  ┌────────────┬────────────┬──────────────────┬────────────┐" -ForegroundColor White
Write-Host "  │ Language   │ Result     │ Queued           │ Build ID   │" -ForegroundColor White
Write-Host "  ├────────────┼────────────┼──────────────────┼────────────┤" -ForegroundColor White

foreach ($pipe in $Pipelines) {
    $lang = $pipe.Language
    $row  = $results | Where-Object { $_.Language -eq $lang }

    $langPad = $lang.PadRight(10)

    if ($row) {
        $resultColor = if ($row.Result -eq 'succeeded') { 'Green' } else { 'Red' }
        $resultIcon  = if ($row.Result -eq 'succeeded') { '✓ pass' } else { '✗ FAIL' }
        $resultPad   = $resultIcon.PadRight(10)
        $timePad     = $row.QueueTime.PadRight(16)
        $idPad       = "$($row.BuildId)".PadRight(10)
    }
    else {
        $resultColor = 'DarkGray'
        $resultPad   = '—'.PadRight(10)
        $timePad     = '—'.PadRight(16)
        $idPad       = '—'.PadRight(10)
    }

    Write-Host "  │ " -NoNewline -ForegroundColor White
    Write-Host $langPad -NoNewline
    Write-Host " │ " -NoNewline -ForegroundColor White
    Write-Host $resultPad -NoNewline -ForegroundColor $resultColor
    Write-Host " │ " -NoNewline -ForegroundColor White
    Write-Host $timePad -NoNewline -ForegroundColor DarkGray
    Write-Host " │ " -NoNewline -ForegroundColor White
    Write-Host $idPad -NoNewline
    Write-Host " │" -ForegroundColor White
}

Write-Host "  └────────────┴────────────┴──────────────────┴────────────┘" -ForegroundColor White

# ── Failure Details ──────────────────────────────────────────────────────────
if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "  ⚠  FAILED BUILDS ($($failures.Count))" -ForegroundColor Red
    Write-Host ("  " + ("─" * 55)) -ForegroundColor Red

    foreach ($f in $failures | Sort-Object Language) {
        Write-Host ""
        Write-Host "  $($f.Language):" -ForegroundColor Yellow
        Write-Host "    Build $($f.BuildId)  ($($f.QueueTime))  result=$($f.Result)" -ForegroundColor Red
        Write-Host "    $($f.Url)" -ForegroundColor DarkGray
    }
}
else {
    Write-Host ""
    Write-Host "  ✓  All builds passing!" -ForegroundColor Green
}

Write-Host ""
