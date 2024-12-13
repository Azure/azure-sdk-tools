#requires -Version 7.0

param(
    [Parameter(Mandatory)]
    [string]$AppId,
    [Parameter(Mandatory)]
    [string]$AppKey,
    [Parameter(Mandatory)]
    [string]$VariableName,
    [Parameter(Mandatory, ParameterSetName = 'Repository')]
    [string]$Repository,
    [Parameter(Mandatory, ParameterSetName = 'Organization')]
    [string]$Organization
)



if ($Organization) {
    $tokenResponse = &"$PSScriptRoot/Get-GitHubInstallationToken.ps1" -AppId $AppId -AppKey $AppKey -Organization $Organization
} else {
    $tokenResponse = &"$PSScriptRoot/Get-GitHubInstallationToken.ps1" -AppId $AppId -AppKey $AppKey -Repository $Repository
}

Write-Host "Setting devops secret $VariableName to installation access token for $($Organization ?? $Repository) expiring at $($tokenResponse.expires_at) UTC"
Write-Host "##vso[task.setvariable variable=$VariableName;issecret=true]$($tokenResponse.token)"
