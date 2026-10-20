<#
.SYNOPSIS
Replays only the approved saved workflow build 6975803, without rebuilding its results.
.DESCRIPTION
Temporary test-branch harness. Validate runs before the Azure service-connection task;
Publish invokes the unchanged PR17315 uploader, then verifies the stored bytes.
Deployment values come from the existing protected source-pipeline configuration.
#>
#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Validate', 'Publish')] [string] $Mode,
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [Parameter(Mandatory)] [string] $ConfigurationPath
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if ($env:SYSTEM_COLLECTIONURI -cne 'https://dev.azure.com/azure-sdk/' -or
    $env:SYSTEM_TEAMPROJECT -cne 'internal' -or $env:SYSTEM_DEFINITIONID -cne '8424' -or
    $env:BUILD_REPOSITORY_NAME -cne 'Azure/azure-sdk-tools' -or
    $env:BUILD_SOURCEBRANCH -cne 'refs/heads/test/eval-cli-linux-probe' -or
    $env:BUILD_REASON -cne 'Manual') { throw 'Not the authorized manual replay pipeline.' }

$expectedHash = 'b6ce1e32feabc9ffcd130ac78aa08469aa863b59075dd9930eb4a833ae41b85e'
$expectedName = 'v1/azure-sdk/internal/8255/6975803/1/dashboard-bundle.zip'
$configurationHash = 'bd3e9b0b48714d7a27a71d3abb6600d635e10af8ffb3d0241817346322ea4ca6'
$uploader = Join-Path $PSScriptRoot '../common/scripts/eval/publisher/Publish-EvalResults.ps1'
$uploaderText = [IO.File]::ReadAllText($uploader).Replace("`r`n", "`n")
$uploaderHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($uploaderText))).ToLowerInvariant()
if ($uploaderHash -cne 'e50c073737649609756c775de9ef39402950416044a7fa7a9dc279513819f2f9') {
    throw 'Uploader differs from the exact tested PR17315 code.'
}

$bundle = Join-Path $ArtifactDirectory 'dashboard-bundle.zip'
$info = Get-Item -LiteralPath $bundle
$descriptorInfo = Get-Item -LiteralPath "$bundle.json"
if ($info.PSIsContainer -or $info.LinkType -or $info.Length -ne 24184 -or
    $descriptorInfo.PSIsContainer -or $descriptorInfo.LinkType -or $descriptorInfo.Length -gt 65536 -or
    (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
    throw 'The downloaded archive is not the pinned retained result bundle.'
}
$descriptor = Get-Content -LiteralPath "$bundle.json" -Raw | ConvertFrom-Json -AsHashtable
if ($descriptor.blobName -cne $expectedName -or $descriptor.sha256 -cne $expectedHash -or
    $descriptor.bytes -ne 24184 -or $descriptor.trials -ne 426 -or $descriptor.shards -ne 7) {
    throw 'The retained preparation descriptor does not match the approved archive.'
}

function Get-ConfigurationHash([System.Collections.IDictionary] $Configuration) {
    $canonical = [ordered]@{}
    foreach ($name in @('EvalStorageAccountName', 'EvalDashboardRefreshUrl', 'EvalDashboardResource')) {
        $canonical[$name] = [string] $Configuration[$name]
    }
    $json = ConvertTo-Json -InputObject $canonical -Compress
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
}

if ($Mode -eq 'Validate') {
    if (!$env:SYSTEM_ACCESSTOKEN) { throw 'A build token is required to verify original main-build provenance.' }
    $headers = @{ Authorization = "Bearer $env:SYSTEM_ACCESSTOKEN" }
    try {
        $build = Invoke-RestMethod -Uri 'https://dev.azure.com/azure-sdk/internal/_apis/build/builds/6975803?api-version=7.1' -Headers $headers
        $definition = Invoke-RestMethod -Uri 'https://dev.azure.com/azure-sdk/internal/_apis/build/definitions/8255?api-version=7.1' -Headers $headers
    } catch { throw 'Could not verify retained build provenance or protected publication configuration.' }
    finally { $headers = $null }
    if ($build.id -ne 6975803 -or $build.definition.id -ne 8255 -or $build.status -cne 'completed' -or
        $build.sourceBranch -cne 'refs/heads/main' -or $build.sourceVersion -cne '19b2968f09b389e3f7d30cec9ad3cdf1f4f1760d' -or
        $build.repository.id -cne 'Azure/azure-sdk-tools' -or $build.project.name -cne 'internal' -or
        $definition.id -ne 8255 -or $definition.repository.name -cne 'Azure/azure-sdk-tools') {
        throw 'Retained results are not from the approved original main build.'
    }
    $configuration = [ordered]@{}
    foreach ($name in @('EvalStorageAccountName', 'EvalDashboardRefreshUrl', 'EvalDashboardResource')) {
        $variable = $definition.variables.PSObject.Properties[$name].Value
        if (!$variable -or ($variable.PSObject.Properties['allowOverride'] -and $variable.allowOverride) -or
            ($variable.PSObject.Properties['isSecret'] -and $variable.isSecret)) { throw 'Publication configuration is missing or is queue-overridable.' }
        $configuration[$name] = [string] $variable.value
    }
    if ((Get-ConfigurationHash $configuration) -cne $configurationHash) { throw 'Protected deployment configuration changed; replay stopped.' }
    [IO.File]::WriteAllText($ConfigurationPath, (ConvertTo-Json -InputObject $configuration))
    Write-Host 'Verified original main build 6975803: 7 shards, 426 results, 24184 unchanged bytes. No evaluation was rerun.'
    return
}

$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json -AsHashtable
if ((Get-ConfigurationHash $configuration) -cne $configurationHash) { throw 'Replay configuration was not the approved protected configuration.' }
Write-Host 'Publishing original build 6975803 with the unchanged fixed uploader from PR17315.'
$messages = @(& $uploader -StorageAccountName $configuration.EvalStorageAccountName -BundlePath $bundle `
    -DashboardRefreshUrl $configuration.EvalDashboardRefreshUrl -DashboardResource $configuration.EvalDashboardResource 6>&1 3>&1)
$refreshed = $false
foreach ($message in $messages) {
    Write-Host ([string] $message)
    if ([string] $message -ceq 'Dashboard refresh succeeded.') { $refreshed = $true }
}

# Read-only verification through the same real service connection; no keys or overwrites.
$readback = Join-Path $env:AGENT_TEMPDIRECTORY "eval-replay-$env:BUILD_BUILDID-verified.zip"
$null = az storage blob download --account-name $configuration.EvalStorageAccountName --container-name vally-results `
    --name $expectedName --auth-mode login --file $readback --no-progress --only-show-errors --output none 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Archive publication returned, but authenticated Blob readback could not be confirmed.' }
if ((Get-Item -LiteralPath $readback).Length -ne 24184 -or
    (Get-FileHash -LiteralPath $readback -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
    throw 'Stored archive readback does not match the original saved bytes.'
}
Write-Host "Authenticated Blob readback verified: 24184 bytes; SHA256 $expectedHash."
if (!$refreshed) { throw 'Archive is durably stored and verified, but dashboard notification did not report success. Reload can recover it.' }
Write-Host 'REAL PUBLICATION VERIFIED: dashboard refresh succeeded for Azure-sdk-tools-workflow-eval build 6975803 (426 results).'