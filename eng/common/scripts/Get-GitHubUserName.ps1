<#
.SYNOPSIS
Retrieved the github user information using microsoft username or email

.DESCRIPTION
This script take either a s microsoft username or the alias version of the email and returns the github user information associated with that user.

.PARAMETER IdentityName
Full official microsoft username of a user.

.PARAMETER IdentityEmail
The microsoft aliase email of a user.

.EXAMPLE
PS> ./eng/common/scripts/Get-GitHubUserName.ps1 -IdentityName "Chidozie Ononiwu" -IdentityEmail "chononiw@microsoft.com"

You must provide one of the parameters.
#>
param(
    [string]$IdentityName,
    [string]$IdentityEmail,
    [string]$TargetDevOpsVariable,
    [string]$ToolVersion,
    [string]$ToolPath = (Join-Path ([System.IO.Path]::GetTempPath()) "identity-converter-tool-path"),
    [string]$DevOpsFeed
)

. (Join-Path $PSScriptRoot common.ps1)
. (Join-Path $PSScriptRoot Helpers DotnetTool-Helpers.ps1)

if (!$IdentityName -and !$IdentityEmail)
{
    LogError "You must provide either 'IdentityName' or 'IdentityEmail'"
    exit(1)
}

$command = Get-CodeOwnersTool -toolPath $ToolPath -toolName "Azure.Sdk.Tools.IdentityConverter" -toolVersion $ToolVersion `
-feedUrl $DevOpsFeed -toolCommandName "identity-converter"

$arguments = @("--aad-app-id-var=APP_ID",
    "--aad-app-secret-var=APP_SECRET",
    "--aad-tenant-var=AAD_TENANT",
    "--kusto-url-var=KUSTO_URL",
    "--kusto-database-var=KUSTO_DB",
    "--kusto-table-var=KUSTO_TABLE")

if ($IdentityName)
{
    $arguments += "--identity-name=$IdentityName"
}

if ($IdentityEmail)
{
    $arguments += "--identity-email=$IdentityEmail"
}

if ($TargetDevOpsVariable)
{
    $arguments += "--target-var=$TargetDevOpsVariable"
}

$gitHubUserDetails = &$command $arguments

if ($LASTEXITCODE -ne 0) {
    LogError "Failed to retrieve Github Username using $IdentityName and/or $IdentityEmail"
    return $null
}

Write-Host $gitHubUserDetails
