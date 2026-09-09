<#
.SYNOPSIS
Notifies API Review Hub that an artifact generation run completed.
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

$completionCallbackUrl = Get-RequiredEnvironmentVariable 'ARH_COMPLETION_CALLBACK_URL'
$callbackTokenAudience = Get-RequiredEnvironmentVariable 'ARH_CALLBACK_TOKEN_AUDIENCE'
$callbackProofToken = Get-RequiredEnvironmentVariable 'ARH_CALLBACK_PROOF_TOKEN'
$operationId = Get-RequiredEnvironmentVariable 'ARH_OPERATION_ID'
$resultSummaryPath = Get-RequiredEnvironmentVariable 'ARH_RESULT_SUMMARY_PATH'
$generationResult = 'Failed'
if (Test-Path -Path $resultSummaryPath -PathType Leaf) {
  $resultSummary = Get-Content -Raw -Path $resultSummaryPath | ConvertFrom-Json
  if ($resultSummary.status -eq 'succeeded') {
    $generationResult = 'Succeeded'
  }
  elseif ($resultSummary.status -ne 'failed') {
    throw "Unsupported result summary status '$($resultSummary.status)'."
  }
}

try {
  $tokenResponse = Get-AzAccessToken -ResourceUrl $callbackTokenAudience -AsSecureString -ErrorAction Stop
}
catch {
  throw "Failed to acquire access token for API Review Hub callback audience '$callbackTokenAudience': $($_.Exception.Message)"
}

$secureToken = $tokenResponse.Token
if (-not $secureToken) {
  throw "Failed to acquire access token for API Review Hub callback audience '$callbackTokenAudience'"
}

$token = [System.Net.NetworkCredential]::new('', $secureToken).Password
if (-not $token) {
  throw 'Acquired API Review Hub callback token was empty after unwrap'
}

$body = [ordered]@{
  operationId = $operationId
  mode = $env:ARH_REQUEST_MODE
  language = $env:ARH_LANGUAGE
  buildId = $env:ARH_BUILD_ID
  project = $env:ARH_PROJECT
  result = $generationResult
  artifacts = [ordered]@{
    result = $env:ARH_RESULT_ARTIFACT_NAME
  }
}

$generationSucceeded = $generationResult -eq 'Succeeded' -or $generationResult -eq 'SucceededWithIssues'
if ($generationSucceeded) {
  $body.artifacts.target = $env:ARH_TARGET_ARTIFACT_NAME
}

$hasBaseline = -not [string]::IsNullOrWhiteSpace($env:ARH_BASE_REF)
if ($hasBaseline -and $generationSucceeded) {
  $body.artifacts.base = $env:ARH_BASE_ARTIFACT_NAME
}

if ($completionCallbackUrl.EndsWith('/')) {
  $completionCallbackUrl = "${completionCallbackUrl}${operationId}"
}

$completionCallbackUri = $null
if (-not [System.Uri]::TryCreate($completionCallbackUrl, [System.UriKind]::Absolute, [ref] $completionCallbackUri)) {
  throw "completionCallbackUrl '$completionCallbackUrl' is not a valid absolute URI."
}

$headers = @{
  Authorization = "Bearer $token"
  'Content-Type' = 'application/json'
  'x-arh-callback-proof' = $callbackProofToken
}
$jsonBody = $body | ConvertTo-Json -Depth 10 -Compress

Write-Host "Completion callback URL: $completionCallbackUrl"
Write-Host "Completion callback body: $jsonBody"
Invoke-WebRequest -Method POST -Uri $completionCallbackUri -Headers $headers -Body $jsonBody -MaximumRetryCount 3 | Out-Null
