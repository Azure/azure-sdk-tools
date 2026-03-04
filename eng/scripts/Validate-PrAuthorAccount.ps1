param(
  [Parameter(Mandatory = $true)]
  [string]$Username
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

# Thresholds
$MinAccountAgeDays = 14
$RiskScoreThreshold = 6

function Get-GitHubUser([string]$login) {
  $json = gh api "users/$login" 2>&1
  return $json | ConvertFrom-Json
}

function Get-GitHubPublicEvents([string]$login) {
  # Fetch the oldest available page of public events to find earliest recent activity.
  # GitHub only retains 90 days of events and returns at most 10 pages of 30.
  # First, get page 1 to see if there are any events at all.
  $page1Json = gh api "users/$login/events/public?per_page=100&page=1" 2>&1
  $page1 = $page1Json | ConvertFrom-Json

  if ($page1.Count -eq 0) {
    return @{ Events = @(); OldestEventDate = $null }
  }

  # Walk forward to find the last non-empty page (max 10 pages per GitHub API).
  $oldestEvents = $page1
  $savedPref = $PSNativeCommandUseErrorActionPreference
  $PSNativeCommandUseErrorActionPreference = $false
  for ($p = 2; $p -le 10; $p++) {
    $pageJson = gh api "users/$login/events/public?per_page=100&page=$p" 2>&1
    if ($LASTEXITCODE -ne 0) { break }
    $page = $pageJson | ConvertFrom-Json
    if ($page.Count -eq 0) { break }
    $oldestEvents = $page
  }
  $PSNativeCommandUseErrorActionPreference = $savedPref

  $oldestDate = ($oldestEvents | Select-Object -Last 1).created_at
  return @{ Events = $page1; OldestEventDate = $oldestDate }
}

function Get-GitHubRepos([string]$login) {
  $json = gh api "users/$login/repos?per_page=100&page=1" 2>&1
  return $json | ConvertFrom-Json
}

# --- Main ---
Write-Host "============================================"
Write-Host "PR Author Account Validation: $Username"
Write-Host "============================================"
Write-Host ""

$user = Get-GitHubUser $Username
$now = Get-Date

# --- Hard fail: account age ---
$createdAt = [DateTime]::Parse($user.created_at)
$accountAgeDays = [int]($now - $createdAt).TotalDays

Write-Host "Account created: $($user.created_at) ($accountAgeDays days ago)"
Write-Host ""

if ($accountAgeDays -lt $MinAccountAgeDays) {
  Write-Host "::error::HARD FAIL: Account '$Username' is only $accountAgeDays day(s) old. Accounts less than $MinAccountAgeDays days old require an admin override to merge. This policy exists to protect against abuse."
  exit 1
}

# --- Scoring heuristics ---
$score = 0
$signals = @()

# Heuristic 1: Dormant account reactivation
# Account > 90 days old but the oldest available public event is within the last 30 days,
# meaning all detectable activity started very recently.
$eventData = Get-GitHubPublicEvents $Username
if ($eventData.OldestEventDate -and $accountAgeDays -gt 90) {
  $oldestEventDate = [DateTime]::Parse($eventData.OldestEventDate)
  $oldestEventAgeDays = [int]($now - $oldestEventDate).TotalDays
  if ($oldestEventAgeDays -le 30) {
    $score += 3
    $signals += @{ Name = "Dormant reactivation"; Points = 3; Detail = "Account is $accountAgeDays days old but oldest public event is only $oldestEventAgeDays days ago" }
  }
}
elseif ($eventData.Events.Count -eq 0 -and $accountAgeDays -gt 90) {
  # No public events at all on an old account — also suspicious
  $score += 3
  $signals += @{ Name = "Dormant reactivation"; Points = 3; Detail = "Account is $accountAgeDays days old with zero public events" }
}

# Heuristic 2: Empty profile
$hasProfile = $user.bio -or $user.company -or $user.location
if (-not $hasProfile) {
  $score += 2
  $signals += @{ Name = "Empty profile"; Points = 2; Detail = "No bio, company, or location" }
}

# Heuristic 3: Zero social graph
if ($user.followers -eq 0 -and $user.following -eq 0) {
  $score += 2
  $signals += @{ Name = "Zero social graph"; Points = 2; Detail = "0 followers, 0 following" }
}

# Heuristic 4: Fork-only repos
$repos = Get-GitHubRepos $Username
if ($repos.Count -gt 0) {
  $originalRepos = $repos | Where-Object { -not $_.fork }
  if ($originalRepos.Count -eq 0) {
    $score += 2
    $signals += @{ Name = "Fork-only repos"; Points = 2; Detail = "All $($repos.Count) public repo(s) are forks" }
  }
}

# Heuristic 5: No gists
if ($user.public_gists -eq 0) {
  $score += 1
  $signals += @{ Name = "No gists"; Points = 1; Detail = "0 public gists" }
}

# --- Output summary ---
Write-Host "--- Risk Assessment ---"
Write-Host ""
Write-Host ("| {0,-25} | {1,-6} | {2}" -f "Signal", "Points", "Detail")
Write-Host ("| {0,-25} | {1,-6} | {2}" -f ("-" * 25), ("-" * 6), ("-" * 50))

if ($signals.Count -eq 0) {
  Write-Host ("| {0,-25} | {1,-6} | {2}" -f "(none)", "", "No risk signals detected")
}
else {
  foreach ($s in $signals) {
    Write-Host ("| {0,-25} | {1,-6} | {2}" -f $s.Name, "+$($s.Points)", $s.Detail)
  }
}

Write-Host ""
Write-Host "Total risk score: $score / $RiskScoreThreshold (threshold)"
Write-Host ""

if ($score -ge $RiskScoreThreshold) {
  Write-Host "::error::FAIL: Account '$Username' has a risk score of $score (threshold: $RiskScoreThreshold). This account exhibits patterns consistent with abuse (dormant/suspicious account). An admin override is required to merge this PR."
  exit 1
}

Write-Host "Account validation passed."
