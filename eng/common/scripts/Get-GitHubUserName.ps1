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
    [string]$ToolVersion="1.0.0-dev.20211221.1",
    [string]$DevOpsFeed = "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json"
)

. (Join-Path $PSScriptRoot common.ps1)
. (Join-Path $PSScriptRoot Helpers DotnetTool-Helpers.ps1))

if (!$IdentityName -and !$IdentityEmail)
{
    LogError "You must provide either 'IdentityName' or 'IdentityEmail'"
    exit(1)
}

$command = Get-CodeOwnersTool -toolPath $ToolPath -toolName "Azure.Sdk.Tools.IdentityConverter" -toolVersion $ToolVersion `
-feedUrl $DevOpsFeed -toolCommandName "identity-converter"

$gitHubUserDetails = $command `
    --aad-app-id-var APP_ID `
    --aad-app-secret-var APP_SECRET `
    --aad-tenant-var AAD_TENANT `
    --kusto-url-var KUSTO_URL `
    --kusto-database-var KUSTO_DB `
    --kusto-table-var KUSTO_TABLE `
    --identity-name $IdentityName `
    --identity-email $IdentityEmail

if ($LASTEXITCODE -ne 0) {
    LogError "Filed to retrieve Github Username using $IdentityName and/or $IdentityEmail"
    return null
}

$gitHubUserDetailsJson = $gitHubUserDetails | ConvertFrom-Json
return $gitHubUserDetailsJson.GithubUserName
