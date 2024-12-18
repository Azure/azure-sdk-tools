#requires -Version 7.0

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string]$AppId,
    [Parameter(Mandatory)]
    [string]$AppKey,
    [Parameter(Mandatory, ParameterSetName = 'Repository')]
    [string]$Repository,
    [Parameter(Mandatory, ParameterSetName = 'Organization')]
    [string]$Organization,
    [Parameter(Mandatory, ParameterSetName = 'User')]
    [string]$User,
    [switch]$Token
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$now = [System.DateTimeOffset]::UtcNow
$payload = [ordered]@{ iss = $AppId; iat = $now.ToUnixTimeSeconds(); exp = $now.AddMinutes(1).ToUnixTimeSeconds() }

$jwt = &"$PSScriptRoot/New-JsonWebToken.ps1" -Payload $payload -PrivateKeyPem $AppKey

$headers = @{
    "Authorization" = "Bearer $jwt"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$url = switch ($PSCmdlet.ParameterSetName) {
    "Organization" { "https://api.github.com/orgs/$Organization/installation" }
    "User" { "https://api.github.com/users/$User/installation" }
    "Repository" { "https://api.github.com/repos/$Repository/installation" }
    Default { Write-Error "No installation specified" }
}

$installationResponse = Invoke-RestMethod -Method GET -Uri $url -Headers $headers
$tokenResponse = Invoke-RestMethod -Method POST -Uri $installationResponse.access_tokens_url -Headers $headers

if($Token) {
    Write-Output $tokenResponse.token
} else {    
    Write-Output $tokenResponse
}