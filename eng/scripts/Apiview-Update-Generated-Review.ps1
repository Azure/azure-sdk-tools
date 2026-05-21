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
# Requires AzurePowerShell@5 context for Get-AzAccessToken.
# Sample request:
# POST https://apiview.dev/review/UpdateApiReview
# Body: { "repoName": "azure/azure-sdk-tools", "buildId": "1742433", "artifactPath": "apiview" }

####################################################################################################################

# Acquire Entra ID token for APIView app registration
try {
    $tokenResponse = Get-AzAccessToken -ResourceUrl "api://apiview" -ErrorAction Stop
    $token = $tokenResponse.Token
    if (-not $token) {
        Write-Error "Failed to acquire access token for APIView (resource: api://apiview)"
        exit 1
    }
}
catch {
    Write-Error "Failed to acquire access token for APIView (resource: api://apiview): $($_.Exception.Message)"
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
    Write-Host "Error - Exception message: $($_.Exception.Message)"

    if ($_.Exception.Response)
    {
        Write-Host "Error - HTTP status: $([int]$_.Exception.Response.StatusCode)"

        $responseContent = $null
        try
        {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseContent = $reader.ReadToEnd()
            $reader.Dispose()
        }
        catch
        {
            Write-Host "Error - Failed to read response body: $($_.Exception.Message)"
        }

        if ($responseContent)
        {
            Write-Host "Error - Response body: $responseContent"
        }
    }

    exit 1
}