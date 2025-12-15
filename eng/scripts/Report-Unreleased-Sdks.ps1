<#
.SYNOPSIS
    Automates email notifications to Azure SDK release plan owners when Tier 1 SDK coverage is missing.

.DESCRIPTION
    This script checks overdue Azure SDK release plans for missing Tier 1 languages. For any gaps, it builds an HTML email and sends it to the plan owner with steps to fix the issue (generate SDKs or request an exception). 

.PARAMETER AzureSDKEmailUri
    The Uri of the app used to send email notifications

.PARAMETER AzsdkExePath
    Path to the Azure SDK CLI executable (azsdk).
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $AzureSDKEmailUri,

    [Parameter(Mandatory = $true)]
    [string] $AzsdkExePath
)

Set-StrictMode -Version 3

$AzureSdkApexEmail = "azsdkapex@microsoft.com"
$Subject = "Action Required: Missing Tier 1 language in your Azure SDK Release Plan"

function BuildEmailNotification($releaseOwnerName, $plane, $missingSDKs, $releasePlanLink) {
    $body = @"
<html>
<body>
    <p>Hello $releaseOwnerName,</p>
    <p>Our automation has flagged your Azure SDK release plan as missing one or more Tier 1 SDKs for the following plane:</p>
    <ul>
        <li><strong>Plane:</strong> $plane</li>
        <li><strong>Missing SDKs:</strong> $missingSDKs</li>
        <li><strong>Release Plan Link:</strong> <a href=$releasePlanLink>$releasePlanLink</a></li>
    </ul>
    <p>Per Azure SDK release requirements, all Tier 1 languages must be supported unless an approved exclusion is filed. Please take one of the following actions:</p>
    <ol>
        <li>Generate and release the missing SDKs using <a href='https://aka.ms/azsdk/dpcodegen'>https://aka.ms/azsdk/dpcodegen</a></li>
        <li>File for an exclusion: <a href='https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception'>https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception</a></li>
    </ol>
    <p>Thank you for helping maintain language parity across Azure SDKs.</p>
    <p>Best regards,<br/>Azure SDK PM Team</p>
</body>
</html>
"@
    return $body
}

if (-not (Test-Path $AzsdkExePath)) {
    throw "Azure SDK CLI executable not found at: $AzsdkExePath"
}

$jsonOutput =  & $AzsdkExePath release-plan list-overdue --output json
$releasePlansData = $jsonOutput | ConvertFrom-Json
$releasePlans = $releasePlansData.release_plans

if (-not $releasePlans) {
    throw "Unexpected JSON structure. Expected 'release_plans' property."
}


foreach ($releasePlan in $releasePlans) {
    $releaseOwnerEmail = $releasePlan.ReleasePlanSubmittedByEmail
    
    try {
        # Throws on invalid emails
        [void][System.Net.Mail.MailAddress]::new($releaseOwnerEmail)
    }
    catch {
        Write-Host ("Skipped notification for Release Plan ID {0}: invalid email '{1}'" -f $releasePlan.WorkItemId, $releaseOwnerEmail)
        continue
    }

    $releaseOwnerName = $releasePlan.Owner
    $plane = $releasePlan.IsManagementPlane ? "Management Plane" : "Data Plane"
    $releasePlanLink = $releasePlan.ReleasePlanLink
    
    # Start with all Tier 1 languages
    $missingSDKs = @('.NET', 'JavaScript', 'Python', 'Java', 'Go')
    
    # Skip Go for Data Plane release plans
    if ($releasePlan.IsDataPlane) {
        $missingSDKs = $missingSDKs | Where-Object { $_ -ine 'Go' }
    }
    
    foreach ($info in $releasePlan.SDKInfo) {
        $statusNorm = $info.ReleaseStatus.ToLower()

        # Remove language from missing list if it's released
        $isReleased = ($statusNorm -eq 'released')
        if ($isReleased) {
            $missingSDKs = $missingSDKs | Where-Object { $_ -ine $info.Language }
        }
    }

    $missingSDKsString = ($missingSDKs -join ', ')
    $body = BuildEmailNotification -releaseOwnerName $releaseOwnerName -plane $plane -missingSDKs $missingSDKsString -releasePlanLink $releasePlanLink

    & (Join-Path $PSScriptRoot "Send-Email-Notification.ps1") -AzureSDKEmailUri $AzureSDKEmailUri -To $releaseOwnerEmail -Cc $AzureSdkApexEmail -Subject $Subject -Body $body
}