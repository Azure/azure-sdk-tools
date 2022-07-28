[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$BuildId,
  [string]$RepoName = "azure/azure-sdk-tools",
  [string]$ArtifactName = "apiview",
  [string]$ApiviewUpdateUrl = "https://apiview.dev/review/UpdateApiReview"
)

# Sample request
# https://apistaging.test.dev/review/UpdateApiReview?repoName=azure/azure-sdk-tools&buildId=1742433&artifact=apiview


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