<#
.DESCRIPTION
Get the corresponding ms alias from github identity
.PARAMETER AadToken
The aad access token.
.PARAMETER GithubName
Github identity. E.g sima-zhu
.PARAMETER ContentType
Content type of http requests.
.PARAMETER AdditionalHeaders
Additional parameters for http request headers in key-value pair format, e.g. @{ key1 = val1; key2 = val2; key3 = val3}
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string]$AadToken,

  [Parameter(Mandatory = $true)]
  [string]$GithubName,

  [Parameter(Mandatory = $false)]
  [string]$ApiVersion = "2019-10-01",

  [Parameter(Mandatory = $false)]
  [string]$ContentType = "application/json",

  [Parameter(Mandatory = $false)]
  [hashtable]$AdditionalHeaders
)
. "${PSScriptRoot}\logging.ps1"

$OpensourceAPIBaseURI = "https://repos.opensource.microsoft.com/api/people/links/github/$GithubName"

$Headers = @{
    "Authorization" = "Bearer $AadToken"
    "Content-Type" = $ContentType
    "api-version" = $ApiVersion
}

function Load-RequestHeaders() {
    if ($AdditionalHeaders) {
        return $Headers + $AdditionalHeaders
    }
    return $Headers
}


try {
    $headers = Load-RequestHeaders
    $resp = Invoke-RestMethod $OpensourceAPIBaseURI -Method 'GET' -Headers $Headers
}
catch { 
    LogError $PSItem.ToString()
    exit 1
}

$resp | Write-Verbose

if ($resp.aad) {
    return $resp.aad.alias
}

LogError "Failed to retrieve the ms alias from given github identity: $GithubName."
