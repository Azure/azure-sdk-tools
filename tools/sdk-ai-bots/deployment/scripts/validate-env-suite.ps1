#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates infra/environments/environment-suite.yaml.

.DESCRIPTION
    - Confirms required keys exist for each selected environment.
    - Confirms no placeholder values (REPLACE_WITH_*) remain.
    - Confirms subscription IDs are GUID-shaped.
    - Confirms source-controlled bot routes cover every monitored Teams channel.
    - Confirms every route tenant is defined and links to the configured team.
    - Exits non-zero on any failure.
#>

[CmdletBinding()]
param(
    [string]$SuitePath = "$PSScriptRoot/../infra/environments/environment-suite.yaml",

    [ValidateSet('dev', 'preview', 'prod')]
    [string[]]$Environment = @('dev', 'preview', 'prod')
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
    'serverApplicationClientId', 'serverApplicationIdUri',
    'resourceGroupPrefix', 'keyVaultName', 'appConfigName',
    'containerRegistryName', 'teamsGroupId', 'approvalRequired',
    'prodDeployOnlyFromPipeline', 'chatbotEvolutionAgentEnabled',
    'rolloutStrategy'
)
$Envs = $Environment
$DeploymentRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $SuitePath -Parent) '../..'))

$errors = @()

foreach ($env in $Envs) {
    foreach ($key in $RequiredKeys) {
        $value = $null
        if ($hasYq) {
            $value = (& yq -r ".environments.$env.$key" $SuitePath).Trim()
        } else {
            $content = Get-Content $SuitePath -Raw
            $pattern = "(?ms)^\s{4}${env}:.*?^\s{8}${key}:\s*[`"']?(?<v>[^`"'\r\n]+)[`"']?"
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
        if ($key -eq 'serverApplicationClientId' -and $value -notmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') {
            $errors += "[$env] serverApplicationClientId '$value' is not GUID-shaped"
        }
        if ($key -eq 'serverApplicationIdUri' -and $value -notmatch '^(api|https)://[^/].*[^/]$') {
            $errors += "[$env] serverApplicationIdUri '$value' must be an api:// or https:// URI without a trailing slash"
        }
        if ($key -eq 'chatbotEvolutionAgentEnabled' -and $value -notmatch '^(true|false)$') {
            $errors += "[$env] chatbotEvolutionAgentEnabled '$value' must be true or false"
        }
    }

    if ($hasYq) {
        $teamsChannelIds = @(& yq -r ".environments.$env.teamsChannelIds[]?" $SuitePath)
        if ($teamsChannelIds.Count -eq 0) {
            $errors += "[$env] missing or empty key 'teamsChannelIds'"
        } elseif (@($teamsChannelIds | Where-Object { [string]::IsNullOrWhiteSpace($_) -or $_ -match '^REPLACE_WITH_' }).Count -gt 0) {
            $errors += "[$env] 'teamsChannelIds' contains an empty value or placeholder"
        }

        $evolutionEnabled = (& yq -r ".environments.$env.chatbotEvolutionAgentEnabled" $SuitePath).Trim()
        $candidateEnvironment = (& yq -r ".environments.$env.candidateEnvironment // `"`"" $SuitePath).Trim()
        if ($evolutionEnabled -eq 'true' -and [string]::IsNullOrWhiteSpace($candidateEnvironment)) {
            $errors += "[$env] chatbot evolution is enabled but candidateEnvironment is missing"
        } elseif (-not [string]::IsNullOrWhiteSpace($candidateEnvironment) -and $candidateEnvironment -notin @('dev', 'preview', 'prod')) {
            $errors += "[$env] candidateEnvironment '$candidateEnvironment' is not declared"
        }

        $channelConfigPath = Join-Path $DeploymentRoot "config/$env/channel.yaml"
        $tenantConfigPath = Join-Path $DeploymentRoot "config/$env/tenant.yaml"
        if (-not (Test-Path $channelConfigPath)) {
            $errors += "[$env] config/$env/channel.yaml is missing"
        } else {
            $routeIds = @(& yq -r '.channels[].id' $channelConfigPath)
            foreach ($channelId in $teamsChannelIds) {
                if ($channelId -notin $routeIds) {
                    $errors += "[$env] monitored Teams channel '$channelId' has no route in config/$env/channel.yaml"
                }
            }
            foreach ($routeId in $routeIds) {
                if ($routeId -notin $teamsChannelIds) {
                    $errors += "[$env] config/$env/channel.yaml contains unmonitored Teams channel '$routeId'"
                }
            }

            if (-not (Test-Path $tenantConfigPath)) {
                $errors += "[$env] config/$env/tenant.yaml is missing"
            } else {
                $routeTenants = @(
                    & yq -r '[.default.tenant, .channels[].tenant] | unique | .[]' $channelConfigPath
                )
                $definedTenants = @(& yq -r '.tenants[].tenant' $tenantConfigPath)
                foreach ($routeTenant in $routeTenants) {
                    if ($routeTenant -notin $definedTenants) {
                        $errors += "[$env] route tenant '$routeTenant' is not defined in config/$env/tenant.yaml"
                    }
                }

                $teamsGroupId = (& yq -r ".environments.$env.teamsGroupId" $SuitePath).Trim()
                foreach ($channelLink in @(& yq -r '.tenants[].channel_link' $tenantConfigPath)) {
                    if ($channelLink -notmatch "[?&]groupId=$([regex]::Escape($teamsGroupId))(?:&|$)") {
                        $errors += "[$env] tenant channel link targets a different Teams group: '$channelLink'"
                    }
                }
            }
        }
    } else {
        $content = Get-Content $SuitePath -Raw
        $pattern = "(?ms)^\s{4}${env}:.*?^\s{8}teamsChannelIds:\s*\r?\n(?<items>(?:^\s{12}-\s*[^\r\n]+\r?\n?)+)"
        if ($content -notmatch $pattern) {
            $errors += "[$env] missing or empty key 'teamsChannelIds'"
        } elseif ($Matches['items'] -match 'REPLACE_WITH_') {
            $errors += "[$env] 'teamsChannelIds' contains a placeholder"
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
