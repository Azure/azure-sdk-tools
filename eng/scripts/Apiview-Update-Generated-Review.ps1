[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
  [string]$RepoName = "azure/azure-sdk-tools",
  [string]$ArtifactName = "apiview",
  [string]$ApiviewUpdateUrl = "https://apiview.dev/review/UpdateApiReview"
)

####################################################################################################################

# This script is called by tools - generate-<Language>-apireview pipelines to send a request to APIView server to
# to notify that generated code file is ready and published. APIView server pulls this code file from artifact and 
# uploads into APIView. This offline step abstracts and protects APIView server resources so caller doesn't have to
# know about APIView server. Pipline will publish an artifact 'apiview' before this script is called

# Script will send a request to APIView with build ID, artifact name and repo name as params.
# Sample request is as follows:
# https://apistaging.test.dev/review/UpdateApiReview?repoName=azure/azure-sdk-tools&buildId=1742433&artifact=apiview

####################################################################################################################

$query = [System.Web.HttpUtility]::ParseQueryString('')
$query.Add('artifact', $ArtifactName)
$query.Add('buildId', $BuildId)
$query.Add('repoName', $repoName)
$uri = [System.UriBuilder]$APIViewUri
$uri.query = $query.toString()
Write-Host "Request URI: $($uri.Uri.OriginalString)"
try
{
    $Response = Invoke-WebRequest -Method 'GET' -Uri $uri.Uri -MaximumRetryCount 3
    Write-Host "Response status : $($Response.StatusCode)"
}
catch
{
    Write-Host "Error $StatusCode - Exception details: $($_.Exception.Response)"
    exit 1
}