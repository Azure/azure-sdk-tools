#requires -Version 7.0

param(
    [Parameter(Mandatory)]
    [string]$AppId,
    [Parameter(Mandatory)]
    [string]$AppKey,
    [Parameter(Mandatory, ParameterSetName = 'Repository')]
    [string]$Repository,
    [Parameter(Mandatory, ParameterSetName = 'Organization')]
    [string]$Organization
)

$now = [System.DateTimeOffset]::UtcNow
$payload = [ordered]@{ iss = $AppId; iat = $now.ToUnixTimeSeconds(); exp = $now.AddMinutes(1).ToUnixTimeSeconds() }

$jwt = &"$PSScriptRoot/New-JsonWebToken.ps1" -Payload $payload -PrivateKeyPem $AppKey

$headers = @{
    "Authorization" = "Bearer $jwt"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

if ($Organization) {
    $url = "https://api.github.com/orgs/$Organization/installation"
} else {
    $url = "https://api.github.com/repos/$Repository/installation"
}

$installationResponse = Invoke-RestMethod -Method GET -Uri $url -Headers $headers
$tokenResponse = Invoke-RestMethod -Method POST -Uri $installationResponse.access_tokens_url -Headers $headers

Write-Output $tokenResponse
