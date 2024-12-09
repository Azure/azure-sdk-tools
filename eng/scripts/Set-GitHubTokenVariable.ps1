param(
    [string]$AppId,
    [string]$AppKey,
    [string]$Repository,
    [string]$VariableName
)

$keyLength = $AppKey.Length
Write-Host "AppKey: $($AppKey.Substring(0, 30)) ... $($keyLength - 60) ... $($AppKey.Substring($keyLength - 31, 30))"

$now = [System.DateTimeOffset]::UtcNow
$payload = [ordered]@{ iss = $AppId; iat = $now.ToUnixTimeSeconds(); exp = $now.AddMinutes(1).ToUnixTimeSeconds() }

$jwt = ./eng/scripts/New-JsonWebToken.ps1 -payload $payload -privateKeyPem $AppKey

$headers = @{
    "Authorization" = "Bearer $jwt"
    "Accept" = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

$url = "https://api.github.com/repos/$Repository/installation"
$installationResponse = Invoke-RestMethod -Method GET -Uri $url -Headers $headers
$tokenResponse = Invoke-RestMethod -Method POST -Uri $installationResponse.access_tokens_url -Headers $headers

Write-Host "Setting devops secret $VariableName to installation access token for $Repository expiring at $($tokenResponse.expires_at.ToLocalTime())"
Write-Host "##vso[task.setvariable variable=$VariableName;issecret=true]$($tokenResponse.token)"
