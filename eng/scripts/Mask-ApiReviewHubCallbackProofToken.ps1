<#
.SYNOPSIS
Registers the API Review Hub callback proof token for log masking.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

$callbackProofToken = $env:ARH_CALLBACK_PROOF_TOKEN
if ([string]::IsNullOrWhiteSpace($callbackProofToken)) {
  throw 'ARH_CALLBACK_PROOF_TOKEN is required.'
}

Write-Host "##vso[task.setsecret]$callbackProofToken"