[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
  [string]$RepoName = "azure/azure-sdk-tools",
  [string]$ArtifactName = "apiview",
  [string]$MetadataFileName = "",
  [string]$ApiviewUpdateUrl = "https://apiview.dev/review/UpdateApiReview"
)

####################################################################################################################

# This script is called by tools - generate-<Language>-apireview pipelines to send a request to APIView server to
# to notify that generated code file is ready and published. APIView server pulls this code file from artifact and
# uploads into APIView. This offline step abstracts and protects APIView server resources so caller doesn't have to
# know about APIView server. Pipline will publish an artifact 'apiview' before this script is called

# Script will send an authenticated POST request to APIView with build ID, artifact name and repo name.
# Requires an AzurePowerShell@5 context with a service connection authorized for APIView so that
# `Get-AzAccessToken -ResourceUrl api://apiview` can mint a bearer token.
# Sample request:
# POST https://apiview.dev/review/UpdateApiReview
# Body: { "repoName": "azure/azure-sdk-tools", "buildId": "1742433", "artifactName": "apiview" }

####################################################################################################################

# Acquire Entra ID bearer token for APIView (resource = api://apiview).
# Az.Accounts 5.x returns Token as a SecureString by default; unwrap it explicitly so it
# doesn't serialize as "System.Security.SecureString" in the Authorization header.
try {
    $tokenResponse = Get-AzAccessToken -ResourceUrl "api://apiview" -AsSecureString -ErrorAction Stop
}
catch {
    throw "Failed to acquire access token for APIView (resource: api://apiview): $($_.Exception.Message)"
}
$secureToken = $tokenResponse.Token
if (-not $secureToken) {
    throw "Failed to acquire access token for APIView (resource: api://apiview)"
}
$token = [System.Net.NetworkCredential]::new('', $secureToken).Password
if (-not $token) {
    throw "Acquired access token for APIView was empty after unwrap"
}

$body = @{
    repoName     = $RepoName
    artifactName = $ArtifactName
    buildId      = $BuildId
    project      = "internal"
}
if ($MetadataFileName) {
    $body.metadataFile = $MetadataFileName
}

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}

$jsonBody = $body | ConvertTo-Json -Compress
Write-Host "Request URL: $ApiviewUpdateUrl"
Write-Host "Request body: $jsonBody"
try
{
    $Response = Invoke-WebRequest -Method 'POST' -Uri $ApiviewUpdateUrl -Headers $headers -Body $jsonBody -MaximumRetryCount 3
    Write-Host "Response status : $($Response.StatusCode)"
}
catch
{
    Write-Host "Error - Exception details: $($_.Exception.Response)"
    throw
}