<#
.SYNOPSIS
Writes the API Review Hub artifact generation result summary.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

function Get-RequiredEnvironmentVariable([string]$Name) {
  $value = [System.Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "$Name is required."
  }

  return $value
}

$artifactStagingDirectory = Get-RequiredEnvironmentVariable 'ARH_ARTIFACT_STAGING_DIRECTORY'
$status = Get-RequiredEnvironmentVariable 'ARH_RESULT_STATUS'
$resultArtifactName = Get-RequiredEnvironmentVariable 'ARH_RESULT_ARTIFACT_NAME'
$baseRef = $env:ARH_BASE_REF
if ([string]::IsNullOrWhiteSpace($baseRef)) {
  $baseRef = ''
}

$artifacts = [ordered]@{
  result = $resultArtifactName
}
$summary = [ordered]@{
  operationId = $env:ARH_OPERATION_ID
  mode = $env:ARH_REQUEST_MODE
  language = $env:ARH_LANGUAGE
  packageName = $env:ARH_PACKAGE_NAME
  baseRef = $baseRef
  targetRef = $env:ARH_TARGET_REF
  status = $status
  artifacts = $artifacts
}

if ($status -eq 'succeeded') {
  $targetDirectory = Join-Path $artifactStagingDirectory 'target'
  $targetMetadataPath = Join-Path $targetDirectory 'artifact-metadata.json'
  $targetMetadata = Get-Content -Raw -Path $targetMetadataPath | ConvertFrom-Json
  $artifacts.target = Get-RequiredEnvironmentVariable 'ARH_TARGET_ARTIFACT_NAME'
  $summary.repositoryFullName = $targetMetadata.repositoryFullName
  $summary.changed = $null
  $summary.baseVersion = ''
  $summary.targetVersion = $targetMetadata.version
  $summary.packageRelativePath = $targetMetadata.packageRelativePath

  if (-not [string]::IsNullOrWhiteSpace($baseRef)) {
    $baseDirectory = Join-Path $artifactStagingDirectory 'base'
    $baseMetadataPath = Join-Path $baseDirectory 'artifact-metadata.json'
    $baseMetadata = Get-Content -Raw -Path $baseMetadataPath | ConvertFrom-Json
    $artifacts.base = Get-RequiredEnvironmentVariable 'ARH_BASE_ARTIFACT_NAME'
    $summary.baseVersion = $baseMetadata.version

    $baseApiPath = Join-Path $baseDirectory 'api.md'
    $targetApiPath = Join-Path $targetDirectory 'api.md'
    $baseHash = (Get-FileHash -Algorithm SHA256 -Path $baseApiPath).Hash
    $targetHash = (Get-FileHash -Algorithm SHA256 -Path $targetApiPath).Hash
    $summary.changed = $baseHash -ne $targetHash
  }
}
elseif ($status -ne 'failed') {
  throw "Unsupported result status '$status'."
}

$resultDirectory = Join-Path $artifactStagingDirectory 'result'
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$summaryPath = Join-Path $resultDirectory 'result-summary.json'
$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding utf8
Get-Content -Path $summaryPath