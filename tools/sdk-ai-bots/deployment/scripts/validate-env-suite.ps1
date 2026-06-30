#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates infra/environments/environment-suite.yaml.

.DESCRIPTION
    - Confirms required keys exist for every environment.
    - Confirms no placeholder values (REPLACE_WITH_*) remain.
    - Confirms subscription IDs are GUID-shaped.
    - Exits non-zero on any failure.
#>

[CmdletBinding()]
param(
    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml"
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SuitePath)) {
    Write-Error "environment-suite.yaml not found at $SuitePath"
    exit 1
}

# Use yq when available; fall back to a regex-based parser for CI agents
# without yq.
$hasYq = $null -ne (Get-Command yq -ErrorAction SilentlyContinue)

$RequiredKeys = @(
    'subscription', 'subscriptionId', 'tenantId',
    'resourceGroupPrefix', 'keyVaultName', 'appConfigName',
    'containerRegistryName', 'approvalRequired',
    'prodDeployOnlyFromPipeline', 'rolloutStrategy'
)
$Envs = @('dev', 'preview', 'prod')

$errors = @()

foreach ($env in $Envs) {
    foreach ($key in $RequiredKeys) {
        $value = $null
        if ($hasYq) {
            $value = (& yq -r ".environments.$env.$key" $SuitePath).Trim()
        } else {
            $content = Get-Content $SuitePath -Raw
            $pattern = "(?ms)^\s{2}${env}:.*?^\s{4}${key}:\s*[`"']?(?<v>[^`"'\r\n]+)[`"']?"
            if ($content -match $pattern) { $value = $Matches['v'].Trim() }
        }

        if ([string]::IsNullOrEmpty($value) -or $value -eq 'null') {
            $errors += "[$env] missing key '$key'"
            continue
        }
        if ($value -match '^REPLACE_WITH_') {
            $errors += "[$env] '$key' still contains placeholder '$value'"
        }
        if ($key -eq 'subscriptionId' -and $value -notmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') {
            $errors += "[$env] subscriptionId '$value' is not GUID-shaped"
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "environment-suite.yaml validation FAILED:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "environment-suite.yaml validation passed." -ForegroundColor Green
exit 0
