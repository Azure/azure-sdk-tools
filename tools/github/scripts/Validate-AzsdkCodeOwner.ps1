[CmdletBinding(SupportsShouldProcess = $true)]
param (
    [Parameter(Mandatory=$true)]
    [string]$UserName
)

if (!(Get-Command -Type Application gh -ErrorAction Ignore)) {
    throw 'You must first install the GitHub CLI: https://github.com/cli/cli/tree/trunk#installation'
}

$hasOrgs = $false
$hasPermissions = $false

# Verify that the user exists and has the correct public
# organization memberships.
$orgResponse = (gh api "https://api.github.com/users/$UserName/orgs")
$orgs = $orgResponse | ConvertFrom-Json | Select-Object -Expand login -ErrorAction Ignore

# Validate that the user has the required public organization memberships.
$requiredOrgs = [System.Collections.Generic.HashSet[String]]::new([StringComparer]::InvariantCultureIgnoreCase)
$requiredOrgs.Add("Microsoft") | Out-Null
$requiredOrgs.Add("Azure") | Out-Null

# Capture non-required organizations for verbose output.
$otherOrgs = $orgs | Where-Object { -not $requiredOrgs.Contains($_) }

Write-Host ""
Write-Host "Required Orginizations:" -ForegroundColor DarkGray

foreach ($org in $orgs) {
    if ($requiredOrgs.Contains($org)) {
        Write-Host "`t$([char]0x2713) $($org) " -ForegroundColor Green
        $requiredOrgs.Remove($org) | Out-Null
    }
}

# Any required organizations left are not present for the user.
foreach ($org in $requiredOrgs) {
    Write-Host "`tx $($org)" -ForegroundColor Red
}

# Write the other public organizations for the user, if
# verbose output is enabled.
if ($otherOrgs.Length -gt 0) {
    Write-Verbose ""
    Write-Verbose "Other Orginizations:"

    foreach ($org in $otherOrgs) {
        Write-Verbose "`t$($org) (not required)"
    }
}

$hasOrgs = ($requiredOrgs.Count -eq 0)

# Verify that the user exists and has the correct permissions
# to the repository.  Delegage to the GH CLI here, as this is a
# priviledged operation that requires an authenticated caller.
$permResponse = (gh api "https://api.github.com/repos/Azure/azure-sdk-for-net/collaborators/$UserName/permission")
$permission = ($permResponse | ConvertFrom-Json).permission

Write-Host ""
Write-Host "Required Permissions:" -ForegroundColor DarkGray

if ($permission -eq "admin" -or $permission -eq "write") {
    Write-Host "`t$([char]0x2713) $($permission) " -ForegroundColor Green
    $hasPermissions = $true
} else {
    Write-Host "`tx write" -ForegroundColor Red
}

# Write the other permissions for the user, if
# verbose output is enabled.
Write-Verbose ""
Write-Verbose "Other Permissions:"
Write-Verbose "`t$($permission) (not required)"

# Validate the user and write the results.
$isValid = ($hasOrgs -and $hasPermissions)

Write-Host ""
Write-Host ""
Write-Host "Validation result for '$UserName':" -ForegroundColor White

if ($isValid) {
    Write-Host "`t$([char]0x2713) Valid code owner" -ForegroundColor Green
} else {
    Write-Host "`tx Not a valid code owner" -ForegroundColor Red
}

Write-Host ""
Write-Host ""

# If verbose output is requested, write the raw API responses.
Write-Verbose "Organizations API Response:"
Write-Verbose "`t$orgResponse"

Write-Verbose ""
Write-Verbose ""
Write-Verbose "Permissions API Response:"
Write-Verbose "`t$permResponse"

Write-Verbose ""
Write-Verbose ""

<#
.SYNOPSIS
Tests a GitHub account for the permissions and public organization memberships required of a code owner in the Azure SDK repositories.

.DESCRIPTION
Tests a GitHub account for the permissions and public organization memberships required of a code owner in the Azure SDK repositories.  These requirements are documented in the Azure SDK onboarding guide and apply to Azure SDK team members and service partners who own their library.

.PARAMETER UserName
The GitHub handle for the user account to test.

 .OUTPUTS
Writes the results of the test to the console, indicating whether or not the user has the correct public organization memberships and permissions to the Azure SDK repositories.

.EXAMPLE
Validate-AzsdkCodeOwner.ps1 jsquire
Tests GitHub user "jsquire" to validate requirements are met for a code owner in the Azure SDK repositories.

.EXAMPLE
Validate-AzsdkCodeOwner.ps1 jsquire -Verbose
Tests GitHub user "jsquire" to validate requirements are met for a code owner in the Azure SDK repositories, showing the raw output from GitHub API calls.
#>
