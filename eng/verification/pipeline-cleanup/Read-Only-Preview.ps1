# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AzsdkPath,
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)
Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
$version = (& $AzsdkPath --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -ne '0.6.52') {
    throw 'Read-only pipeline verification requires the published CLI 0.6.52.'
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$stdout = Join-Path $OutputDirectory 'abandon-overdue-dry-run.json'
$stderr = Join-Path $OutputDirectory 'abandon-overdue-dry-run.stderr.log'
if ((Test-Path -LiteralPath $stdout) -or (Test-Path -LiteralPath $stderr)) {
    throw 'Existing evidence must not be overwritten.'
}
$oldEmailer = $env:AZSDKTOOLS_NOTIFICATION_SERVICE_URL
try {
    $env:AZSDKTOOLS_NOTIFICATION_SERVICE_URL = ''
    $process = Start-Process -FilePath $AzsdkPath -ArgumentList @('release-plan', 'abandon-overdue', '--dry-run', '--output', 'json') `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw 'Read-only preview failed; inspect the private raw artifact.' }
    $response = Get-Content -LiteralPath $stdout -Raw | ConvertFrom-Json -Depth 100
    if ($response.dry_run -ne $true -or $response.operation_status -ne 'Succeeded') { throw 'Unexpected preview response.' }
    $summary = $response.preview_summary
    if ($summary.scanned -ne ($summary.eligible + $summary.skipped + $summary.evaluation_errors)) { throw 'Counts do not reconcile.' }
    Write-Host "Released CLI $version preview: scanned=$($summary.scanned), eligible=$($summary.eligible), skipped=$($summary.skipped), errors=$($summary.evaluation_errors)."
} finally { $env:AZSDKTOOLS_NOTIFICATION_SERVICE_URL = $oldEmailer }