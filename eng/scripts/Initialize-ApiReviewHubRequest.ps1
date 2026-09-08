<#
.SYNOPSIS
Validates an API Review Hub request and initializes its artifact directories.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

function Assert-RequiredEnvironmentVariable([string]$Name) {
  $value = [System.Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "$Name is required."
  }
}

Assert-RequiredEnvironmentVariable 'ARH_OPERATION_ID'
Assert-RequiredEnvironmentVariable 'ARH_PACKAGE_NAME'
Assert-RequiredEnvironmentVariable 'ARH_TARGET_REF'

$artifactStagingDirectory = [System.Environment]::GetEnvironmentVariable('ARH_ARTIFACT_STAGING_DIRECTORY')
if ([string]::IsNullOrWhiteSpace($artifactStagingDirectory)) {
  throw 'ARH_ARTIFACT_STAGING_DIRECTORY is required.'
}

foreach ($directoryName in @('base', 'target', 'result')) {
  $path = Join-Path $artifactStagingDirectory $directoryName
  New-Item -ItemType Directory -Force -Path $path | Out-Null
}