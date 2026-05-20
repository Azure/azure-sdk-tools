[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
  [Parameter(Mandatory = $true)]
  [string]$ApiviewResourceId,
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
# Requires AzurePowerShell@5 context for Get-AzAccessToken.
# Sample request:
# POST https://apiview.dev/review/UpdateApiReview
# Body: { "repoName": "azure/azure-sdk-tools", "buildId": "1742433", "artifactPath": "apiview" }

####################################################################################################################

# Acquire Entra ID token for APIView app registration
$tokenResponse = Get-AzAccessToken -ResourceUrl "api://$ApiviewResourceId"
$token = $tokenResponse.Token
if (-not $token) {
    Write-Error "Failed to acquire access token for APIView (resource: api://$ApiviewResourceId)"
    exit 1
}

$body = @{
    repoName     = $RepoName
    artifactPath = $ArtifactName
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
    exit 1
}