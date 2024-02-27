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
$isVerbose = $PSCmdlet.MyInvocation.BoundParameters["Verbose"].IsPresent

# Verify that the user exists and has the correct public
# organization memberships.
$response = (gh api "https://api.github.com/users/$UserName/orgs")
$json = $response | ConvertFrom-Json

if ($isVerbose) {
    Write-Verbose "Orginizations API Response:"
    Write-Verbose "`t$response"
}

# If there were no organizations, the user fails validation.
if ($json -ne $null) {

    # If the user is not a member of Microsoft or Azure, the user fails validation.
    $orgs = [System.Collections.Generic.HashSet[String]]::new([StringComparer]::InvariantCultureIgnoreCase)

    foreach ($org in $json) {
        $orgs.Add("$($org.login)") | Out-Null
    }

    if ($isVerbose) {
        Write-Verbose ""
        Write-Verbose "Orginizations:"

        foreach ($org in $orgs) {
            Write-Verbose "`t$($org)"
        }
    }

    if ($orgs.Contains("Microsoft") -and $orgs.Contains("Azure")) {
        $hasOrgs = $true
    }
}

# Verify that the user exists and has the correct permissions
# to the repository.  Delegage to the GH CLI here, as this is a
# priviledged operation that requires an authenticated caller.
$response = (gh api "https://api.github.com/repos/Azure/azure-sdk-for-net/collaborators/$UserName/permission")

if ($isVerbose) {
    Write-Verbose ""
    Write-Verbose "Permissions API Response:"
    Write-Verbose "`t$response"
}

$permission = ($response | ConvertFrom-Json).permission

if ($permission -eq "admin" -or $permission -eq "write") {
    $hasPermissions = $true
}

# Validate the user and write the results.
$isValid = ($hasOrgs -and $hasPermissions)

Write-Host ""
Write-Host "Has organization memberships: " -NoNewline
Write-host $hasOrgs -ForegroundColor "$(if ($hasOrgs) { "Green" } else { "Red" })"
Write-Host "Has permissions: " -NoNewline
Write-Host $hasPermissions -ForegroundColor "$(if ($hasPermissions) { "Green" } else { "Red" })"
Write-Host ""
Write-Host "Is valid: " -NoNewline
Write-Host $isValid -ForegroundColor "$(if ($isValid) { "Green" } else { "Red" })"
Write-Host ""
Write-Host ""

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