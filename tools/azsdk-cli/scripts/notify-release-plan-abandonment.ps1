# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Sends warning emails to release plan owners before abandoning their release plans.

.DESCRIPTION
    This script takes a list of work item IDs, fetches the release plan details from Azure DevOps,
    and sends warning emails to the owners using the azure-sdk-emailer service.

.PARAMETER WorkItemIds
    Array of work item IDs (release plan work items) to process.

.PARAMETER EmailerUri
    The URI for the azure-sdk-emailer service (with SAS token if required).

.PARAMETER DryRun
    If specified, the script will not send emails but will show what would be sent.

.PARAMETER AzureDevOpsPAT
    Personal Access Token for Azure DevOps. If not provided, will try to use environment variable AZURE_DEVOPS_EXT_PAT.

.EXAMPLE
    ./notify-release-plan-abandonment.ps1 -WorkItemIds 12345,12346,12347 -EmailerUri "https://your-emailer-url" -DryRun

.EXAMPLE
    ./notify-release-plan-abandonment.ps1 -WorkItemIds @(12345,12346) -EmailerUri "https://your-emailer-url"
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [int[]]$WorkItemIds,

    [Parameter(Mandatory = $true)]
    [string]$EmailerUri,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [string]$AzureDevOpsPAT = $env:AZURE_DEVOPS_EXT_PAT,

    [Parameter(Mandatory = $false)]
    [string]$CcEmail = "azsdkpm@microsoft.com"
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

# Azure DevOps configuration
$DevOpsOrg = "azure-sdk"
$DevOpsProject = "Release"
$DevOpsApiVersion = "7.1"
$DevOpsBaseUrl = "https://dev.azure.com/$DevOpsOrg/$DevOpsProject/_apis"

function Get-ReleasePlanWorkItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$WorkItemId
    )

    Write-Verbose "Fetching work item $WorkItemId from Azure DevOps..."

    $uri = "$DevOpsBaseUrl/wit/workitems/${WorkItemId}?`$expand=all&api-version=$DevOpsApiVersion"

    $headers = @{
        "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$AzureDevOpsPAT"))
        "Content-Type"  = "application/json"
    }

    try {
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        return $response
    }
    catch {
        Write-Warning "Failed to fetch work item $WorkItemId`: $_"
        return $null
    }
}

function Send-EmailNotification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$To,

        [Parameter(Mandatory = $false)]
        [string]$Cc,

        [Parameter(Mandatory = $true)]
        [string]$Subject,

        [Parameter(Mandatory = $true)]
        [string]$Body
    )

    $emailPayload = @{
        EmailTo = $To
        CC      = $Cc
        Subject = $Subject
        Body    = $Body
    }

    $jsonContent = $emailPayload | ConvertTo-Json -Depth 10

    Write-Host "Sending email to: $To" -ForegroundColor Cyan
    Write-Verbose "Email Subject: $Subject"

    if ($DryRun) {
        Write-Host "[DRY RUN] Would send email:" -ForegroundColor Yellow
        Write-Host "  To: $To"
        Write-Host "  CC: $Cc"
        Write-Host "  Subject: $Subject"
        Write-Host ""
        return $true
    }

    try {
        $response = Invoke-RestMethod -Uri $EmailerUri -Method Post -Body $jsonContent -ContentType "application/json"
        Write-Host "Successfully sent email to $To" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Warning "Failed to send email to $To`: $_"
        return $false
    }
}

function Update-WorkItemState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [int]$WorkItemId,

        [Parameter(Mandatory = $true)]
        [string]$State
    )

    Write-Verbose "Updating work item $WorkItemId state to '$State'..."

    $uri = "$DevOpsBaseUrl/wit/workitems/${WorkItemId}?api-version=$DevOpsApiVersion"

    $headers = @{
        "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$AzureDevOpsPAT"))
        "Content-Type"  = "application/json-patch+json"
    }

    $patchDocument = @(
        @{
            op    = "add"
            path  = "/fields/System.State"
            value = $State
        }
    )

    $jsonBody = $patchDocument | ConvertTo-Json -Depth 10

    if ($DryRun) {
        Write-Host "[DRY RUN] Would update work item $WorkItemId state to '$State'" -ForegroundColor Yellow
        return $true
    }

    try {
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Patch -Body $jsonBody
        Write-Host "Successfully updated work item $WorkItemId state to '$State'" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Warning "Failed to update work item $WorkItemId state: $_"
        return $false
    }
}

function Format-AbandonmentWarningEmail {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [object]$WorkItem
    )

    $ownerName = if ($WorkItem.fields.PSObject.Properties['Custom.PrimaryPM']) { $WorkItem.fields.'Custom.PrimaryPM' } else { "Release Plan Owner" }
    $title = if ($WorkItem.fields.PSObject.Properties['System.Title']) { $WorkItem.fields.'System.Title' } else { "Unknown Release Plan" }
    $releasePlanId = if ($WorkItem.fields.PSObject.Properties['Custom.ReleasePlanID']) { $WorkItem.fields.'Custom.ReleasePlanID' } else { "" }
    $releaseMonth = if ($WorkItem.fields.PSObject.Properties['Custom.SDKReleaseMonth']) { $WorkItem.fields.'Custom.SDKReleaseMonth' } else { "Not specified" }
    $releasePlanUrl = "https://aka.ms/azsdk/release-planner/$releasePlanId"

    $body = @"
<html>
<body>
    <p>Hello $ownerName,</p>
    
    <p>This is a courtesy notice to inform you that the following release plan is scheduled to be <strong>abandoned</strong>:</p>
    
    <ul>
        <li><strong>Release Plan Title:</strong> $title</li>
        <li><strong>Release Plan ID:</strong> $releasePlanId</li>
        <li><strong>Target Release Month:</strong> $releaseMonth</li>
        <li><strong>Release Planner Link:</strong> <a href="$releasePlanUrl">$releasePlanUrl</a></li>
    </ul>
    
    <p><strong>Why is this being abandoned?</strong></p>
    <p>Our records indicate that this release plan is <strong>incomplete</strong> and has passed its scheduled target release month of <strong>$releaseMonth</strong>. Release plans that remain incomplete beyond their target date are flagged for abandonment to keep our tracking system current.</p>
    
    <p><strong>What does this mean?</strong></p>
    <p>The release plan will be moved to the "Abandoned" state, indicating that the planned SDK release will not proceed as originally planned.</p>
    
    <p><strong>If you believe this is an error:</strong></p>
    <ol>
        <li>Please respond to this email immediately</li>
        <li>Update the release plan with current status and any relevant information</li>
        <li>Contact the Azure SDK PM team if you need assistance</li>
    </ol>
    
    <p><strong>If you no longer need this release plan:</strong></p>
    <p>No action is required. The release plan will be abandoned as scheduled.</p>
    
    <p>Thank you for your attention to this matter.</p>
    
    <p>Best regards,</p>
    <p>Azure SDK PM Team</p>
</body>
</html>
"@

    return $body
}

# Main script execution
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Release Plan Abandonment Warning Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN MODE - No emails will be sent]" -ForegroundColor Yellow
    Write-Host ""
}

if ([string]::IsNullOrWhiteSpace($AzureDevOpsPAT)) {
    Write-Error "Azure DevOps PAT is required. Set the -AzureDevOpsPAT parameter or AZURE_DEVOPS_EXT_PAT environment variable."
    exit 1
}

$successCount = 0
$failureCount = 0
$skippedCount = 0

foreach ($workItemId in $WorkItemIds) {
    Write-Host "Processing work item: $workItemId" -ForegroundColor Cyan

    # Fetch work item details
    $workItem = Get-ReleasePlanWorkItem -WorkItemId $workItemId

    if ($null -eq $workItem) {
        Write-Warning "Skipping work item $workItemId - could not fetch details"
        $skippedCount++
        continue
    }

    # Validate it's a release plan work item
    $workItemType = $workItem.fields.'System.WorkItemType'
    if ($workItemType -ne "Release Plan") {
        Write-Warning "Skipping work item $workItemId - not a Release Plan (type: $workItemType)"
        $skippedCount++
        continue
    }

    # Get owner email
    $ownerEmail = $workItem.fields.'Custom.ReleasePlanSubmittedby'
    
    if ([string]::IsNullOrWhiteSpace($ownerEmail)) {
        Write-Warning "Skipping work item $workItemId - missing owner email"
        $skippedCount++
        continue
    }

    # Format and send email
    $releasePlanId = if ($workItem.fields.PSObject.Properties['Custom.ReleasePlanID']) { $workItem.fields.'Custom.ReleasePlanID' } else { "" }
    $subject = "Notice: Release Plan $releasePlanId Scheduled for Abandonment"
    $body = Format-AbandonmentWarningEmail -WorkItem $workItem

    $emailSent = Send-EmailNotification -To $ownerEmail -Cc $CcEmail -Subject $subject -Body $body

    if ($emailSent) {
        # Mark the work item as Abandoned
        $stateUpdated = Update-WorkItemState -WorkItemId $workItemId -State "Abandoned"
        if ($stateUpdated) {
            $successCount++
        }
        else {
            Write-Warning "Email sent but failed to update work item state for $workItemId"
            $failureCount++
        }
    }
    else {
        $failureCount++
    }

    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total work items processed: $($WorkItemIds.Count)"
Write-Host "Emails sent successfully: $successCount" -ForegroundColor Green
Write-Host "Emails failed: $failureCount" -ForegroundColor $(if ($failureCount -gt 0) { "Red" } else { "Green" })
Write-Host "Work items skipped: $skippedCount" -ForegroundColor $(if ($skippedCount -gt 0) { "Yellow" } else { "Green" })
