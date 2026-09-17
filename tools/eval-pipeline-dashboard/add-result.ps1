# Adds one or more vally run folders to the dashboard's results directory so they
# show up after a browser refresh.
#
# A "run" is a folder (timestamp-named) containing results.jsonl.
#
# LOCAL  (default): copies the run folder(s) into the watched results dir.
#   ./add-result.ps1 -Path 'C:\path\to\2026-06-22T10-30-00-000Z'
#   ./add-result.ps1 -Path 'C:\runs\*'              # add several at once
#   ./add-result.ps1 -Path '...'  -ResultsDir 'D:\my\results'
#
# AZURE: zips and uploads to /home/site/results on the web app.
#   ./add-result.ps1 -Path 'C:\path\to\run' -AppName <app> -ResourceGroup <rg>

[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string[]]$Path,                                   # run folder(s); globs ok

  # Local target (watched results directory).
  [string]$ResultsDir = (Join-Path $PSScriptRoot "results"),

  # Azure target — supplying both switches to upload mode.
  [string]$AppName,
  [string]$ResourceGroup
)

$ErrorActionPreference = "Stop"

# Resolve and validate the run folders.
$runs = @()
foreach ($p in $Path) {
  $resolved = Resolve-Path $p -ErrorAction Stop
  foreach ($r in $resolved) {
    $item = Get-Item $r.Path
    if (-not $item.PSIsContainer) {
      throw "Not a folder: $($item.FullName) (expected a run folder containing results.jsonl)"
    }
    if (-not (Test-Path (Join-Path $item.FullName "results.jsonl"))) {
      throw "No results.jsonl in $($item.FullName) — not a valid run folder."
    }
    $runs += $item
  }
}
if ($runs.Count -eq 0) { throw "No run folders matched -Path." }
Write-Host "Run folder(s): $($runs.Name -join ', ')" -ForegroundColor Cyan

$azureMode = $AppName -and $ResourceGroup

if (-not $azureMode) {
  # ---- LOCAL: copy into the watched results directory ----
  New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null
  foreach ($run in $runs) {
    $dest = Join-Path $ResultsDir $run.Name
    if (Test-Path $dest) {
      Write-Warning "Run '$($run.Name)' already exists in results dir — skipping (ingestion ignores duplicate run IDs)."
      continue
    }
    Copy-Item $run.FullName $dest -Recurse
    Write-Host "  added -> $dest" -ForegroundColor Green
  }
  Write-Host "Done. If the server is running it ingests within a few seconds — refresh the dashboard." -ForegroundColor Green
}
else {
  # ---- AZURE: zip and upload into /home/site/results via the Kudu zip API ----
  # NOTE: `az webapp deploy --type zip --target-path ...` does NOT work here —
  # it routes through Oryx, which tries to *build* the results as an app and
  # fails. The Kudu VFS zip endpoint extracts an archive into an arbitrary path.
  # Its root is /home, so /api/zip/site/results/ targets /home/site/results.
  $creds = az webapp deployment list-publishing-credentials `
    --name $AppName --resource-group $ResourceGroup -o json | ConvertFrom-Json
  if (-not $creds) { throw "Could not get publishing credentials. Ensure SCM basic auth is enabled and you have access." }
  $pair = "$($creds.publishingUserName):$($creds.publishingPassword)"
  $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
  $headers = @{ Authorization = "Basic $b64" }
  $scmHost = ($creds.scmUri -replace '^https?://[^@]*@', 'https://')

  $zip = Join-Path ([System.IO.Path]::GetTempPath()) ("vally-runs-" + [Guid]::NewGuid().ToString("N") + ".zip")
  try {
    Compress-Archive -Path ($runs.FullName) -DestinationPath $zip -Force
    Write-Host "Uploading $($runs.Count) run(s) to /home/site/results on '$AppName'..." -ForegroundColor Cyan
    Invoke-RestMethod -Uri "$scmHost/api/zip/site/results/" -Headers $headers `
      -Method Put -InFile $zip -ContentType "application/zip" -TimeoutSec 600
    Write-Host "Done. The app ingests new runs within a few seconds — refresh the dashboard." -ForegroundColor Green
  }
  finally {
    Remove-Item $zip -ErrorAction SilentlyContinue
  }
}
