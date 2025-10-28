<#
.SYNOPSIS
Automates CPEX KPI attestation by updating work items and logging evidence to Kusto.

.DESCRIPTION
This script processes triage and release plan work items to validate whether the product meets the required KPI criteria for their lifecycle stage.
It adds attestation entries to a Kusto table and updates work item fields accordingly. It also sends email notifications and includes error handling for service interactions.

.PARAMETER AzureSDKEmailUri
The Uri of the app used to send email notifications

.PARAMETER TableName
The name of the Kusto table where attestation entries will be written.
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $AzureSDKEmailUri, 

    [Parameter(Mandatory = $true)]
    [string] $TableName
)

Set-StrictMode -Version 3
. (Join-Path $PSScriptRoot "../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

$EMAIL_TO = "azsdkapex@microsoft.com"
$EMAIL_CC = "azsdkexp@microsoft.com"

$COMPLETED = 1
$NA = 3

$KPI_ID_Onboarding = "ba2c80d5-b8be-465f-8948-283229082fd1"
$KPI_ID_Mgmt_Private_Preview = "e0504da9-8897-41db-a75f-5027298ba410"
$KPI_ID_Data_Private_Preview = "dfe9c112-416e-4e0a-8012-4a3a29807782"
$KPI_ID_Mgmt_Public_Preview = "84715402-4f3c-4dca-b330-f05206abaec5"
$KPI_ID_Data_Public_Preview = "ad70777b-a1f5-4d77-8926-5c466d7a214d"
$KPI_ID_Mgmt_GA = "210c095f-b3a2-4cf4-a899-eaab4c3ed958"
$KPI_ID_Data_GA = "da768dff-8f90-4999-ad3a-adcd790911f3"

$KPI_ID_TO_TITLE = [ordered]@{
    $KPI_ID_Onboarding             = "Onboarding"
    $KPI_ID_Mgmt_Private_Preview   = "Management Plane - Private Preview API readiness"
    $KPI_ID_Data_Private_Preview   = "Data Plane - Private Preview API readiness"
    $KPI_ID_Mgmt_Public_Preview    = "Management Plane - API readiness and Beta SDK release"
    $KPI_ID_Data_Public_Preview    = "Data Plane - API readiness and Beta SDK release"
    $KPI_ID_Mgmt_GA                = "Management Plane - API readiness and Stable SDK release"
    $KPI_ID_Data_GA                = "Data Plane - API readiness and Stable SDK release"
}

$SERVICE_TREE_URL = "https://microsoftservicetree.com/products/"

$successfulAttestations = [ordered]@{}
foreach ($kpiId in $KPI_ID_TO_TITLE.Keys) {
    $successfulAttestations[$kpiId] = @()
}

$failedAttestations = @()

function AddAttestationEntry($targetId, $actionItemId, $status, $targetType, $url, $productName) {
    & (Join-Path $PSScriptRoot "Add-CPEX-Attestation-Entry.ps1") -TableName $TableName -ActionItemId $actionItemId -TargetId $targetId -TargetType $targetType -Status $status -EvidenceUrl $url

    $successfulAttestations[$actionItemId] += @{
        productId = $targetId
        productName = $productName
        status = if ($status -eq $COMPLETED) { "Completed" } else { "Not Applicable"}
    }
}

function SendEmailNotification($emailTo, $emailCC, $emailSubject, $emailBody) {
    try {
        $body = @{ EmailTo = $emailTo; CC = $emailCC; Subject = $emailSubject; Body = $emailBody} | ConvertTo-Json -Depth 3
        Write-Host "Sending Email - To: $emailTo`nCC: $emailCC`nSubject: $emailSubject`nBody: $emailBody"
        $response = Invoke-RestMethod -Uri $AzureSDKEmailUri -Method Post -Body $body -ContentType "application/json"
        Write-Host "Successfully Sent Email - To: $emailTo`nCC: $emailCC`nSubject: $emailSubject`nBody: $emailBody"
    } catch {
        Write-Error "Failed to send email.`nTo: $emailTo`nCC: $emailCC`nSubject: $emailSubject`nBody: $emailBody`nException message: $($_.Exception.Message)"
    }
}

function BuildSuccessEmailBody {
    $body = "<h2>Successful CPEX KPI Attestations</h2>"
    foreach ($kpiId in $successfulAttestations.Keys) {
        $title = $KPI_ID_TO_TITLE[$kpiId]
        $body += "<h3> $title ($kpiId)</h3><ul>"
        if ($successfulAttestations[$kpiId].Count -ne 0) {
            $body += "<table border='1' cellpadding='5' cellspacing='0'>"
            $body += "<tr><th>Product Name</th><th>Product ID</th><th>Status</th><th>Service Tree Product Link</th></tr>"
            
            foreach ($entry in $successfulAttestations[$kpiId]) {
                $productName = $entry.productName
                $productId = $entry.productId
                $status = $entry.status
                $link = "$SERVICE_TREE_URL$productId"
                
                $body += "<tr>"
                $body += "<td><strong>$productName</strong></td>"
                $body += "<td>$productId</td>"
                $body += "<td>$status</td>"
                $body += "<td><a href=$link>Link</a></td>"
                $body += "</tr>"
            }
            $body += "</table>"
        } else {
            $body += "No product received an attestation for this KPI."
        }

        $body += "</ul>"
    }
    
    return $body
}

function BuildFailureEmailBody {
    $body = "The following errors occurred during the latest CPEX KPI attestation automated run: `n`n"
    $body += "<table border='1' cellpadding='5' cellspacing='0'>"
    $body += "<tr><th>Product Name</th><th>Product ID</th><th>Service Tree Product Link</th><th>Affected KPI</th><th>Work Item ID</th><th>Error</th></tr>"

    foreach ($entry in $failedAttestations) {
        $productName = $entry.productName
        $productId = $entry.productId
        $link = "$SERVICE_TREE_URL$productId"
        $workItemId = $entry.workItemId
        $affectedKpi = $entry.affectedKpi
        $failure = $entry.error

        $body += "<tr>"
        $body += "<td><strong>$productName</strong></td>"
        $body += "<td>$productId</td>"
        $body += "<td><a href=$link>Link</a></td>"
        $body += "<td>$affectedKpi</td>"
        $body += "<td>$workItemId</td>"
        $body += "<td>$failure</td>"
        $body += "</tr>"
    }

    $body += "</table>"
    $body += "</ul>"

    return $body
}

$triages =  Get-TriagesForCPEXAttestation

foreach ($triage in $triages) {
    try {
        $fields = $triage.fields
        $dataScope = $fields["Custom.DataScope"]
        $mgmtScope = $fields["Custom.MgmtScope"]
        $productLifecycle = $fields["Custom.ProductLifecycle"]
        $dataAttestationStatus = $fields["Custom.DataplaneAttestationStatus"]
        $mgmtAttestationStatus = $fields["Custom.ManagementPlaneAttestationStatus"]
        $productServiceTreeId = $fields["Custom.ProductServiceTreeID"]
        $productType = $fields["Custom.ProductType"]
        $productName = $fields["Custom.ProductName"]
        $url = $triage.url

        if ($productType -eq "Sku") {
            $productType = "ProductSku"
        }

        AddAttestationEntry $productServiceTreeId $KPI_ID_Onboarding $COMPLETED $productType $url $productName

        # Maps the current lifecycle stage to the required DATAPLANE KPIs needed to advance to the next lifecycle stages
        $lifecycleToDataKpis = @{
            "In Dev" = @($KPI_ID_Data_Private_Preview, $KPI_ID_Data_Public_Preview, $KPI_ID_Data_GA)
            "Private Preview" = @($KPI_ID_Data_Public_Preview, $KPI_ID_Data_GA)
            "Public Preview" = @($KPI_ID_Data_GA)
        }

        if ($dataAttestationStatus -ne "Completed") {
            if ($dataScope -eq "Yes") {
                Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.DataplaneAttestationStatus" -status "Completed"
            } else {
                if ($lifecycleToDataKpis.ContainsKey($productLifecycle)) {
                    foreach ($kpiId in $lifecycleToDataKpis[$productLifecycle]) {
                        AddAttestationEntry $productServiceTreeId $kpiId $NA $productType $url $productName
                    }
                }
                Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.DataplaneAttestationStatus" -status "Not applicable"
            }
        }

        # Maps the current lifecycle stage to the required MANGEMENT PLANE KPIs needed to advance to the next lifecycle stages
        $lifecycleToMgmtKpis = @{
            "In Dev" = @($KPI_ID_Mgmt_Private_Preview, $KPI_ID_Mgmt_Public_Preview, $KPI_ID_Mgmt_GA)
            "Private Preview" = @($KPI_ID_Mgmt_Public_Preview, $KPI_ID_Mgmt_GA)
            "Public Preview" = @($KPI_ID_Mgmt_GA)
        }

        if ($mgmtAttestationStatus -ne "Completed") {
            if ($mgmtScope -eq "Yes") {
                Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.ManagementPlaneAttestationStatus" -status "Completed"
            } else {
                if ($lifecycleToMgmtKpis.ContainsKey($productLifecycle)) {
                    foreach ($kpiId in $lifecycleToMgmtKpis[$productLifecycle]) {
                        AddAttestationEntry $productServiceTreeId $kpiId $NA $productType $url $productName
                    }
                }
                Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.ManagementPlaneAttestationStatus" -status "Not applicable"
            }
        }
    } catch {
        Write-Warning "Error processing triage item [$($triage.id)]`nException message: $($_.Exception.Message)"

        $failedAttestations += @{
            productId = $productServiceTreeId
            productName = $productName
            workItemId = $triage.id
            affectedKpi = "Onboarding"
            error = $_.Exception.Message
        }
    }
}

$releasePlans = Get-ReleasePlansForCPEXAttestation

foreach ($releasePlan in $releasePlans) {
    try {
        $fields = $releasePlan.fields
        $dataScope = $fields["Custom.DataScope"]
        $mgmtScope = $fields["Custom.MgmtScope"]
        $lifecycle = $fields["Custom.ReleasePlanType"]
        $productServiceTreeId = $fields["Custom.ProductServiceTreeID"]
        $productType = $fields["Custom.ProductType"]
        $productName = $fields["Custom.ProductName"]
        $url = $releasePlan.url

        if ($productType -eq "Sku") {
            $productType = "ProductSku"
        }
        
        $kpiId = switch ($mgmtScope) {
            'Yes' {
                switch -Wildcard ($lifecycle) {
                    "*Public Preview*" { $KPI_ID_Mgmt_Public_Preview }
                    "*Private Preview*" { $KPI_ID_Mgmt_Private_Preview }
                    "*GA*" { $KPI_ID_Mgmt_GA }
                    default {
                        Write-Warning "Release Plan ID $($releasePlan.id): Management plane in scope, unknown lifecycle $($lifecycle)"
                        $null
                    }
                }
            }
            default {
                switch ($dataScope) {
                    'Yes' {
                        switch -Wildcard ($lifecycle) {
                            "*Public Preview*" { $KPI_ID_Data_Public_Preview }
                            "*Private Preview*" { $KPI_ID_Data_Private_Preview }
                            "*GA*" { $KPI_ID_Data_GA }
                            default {
                                Write-Warning "Release Plan ID $($releasePlan.id): Dataplane in scope, unknown lifecycle $($lifecycle)"
                                $null
                            }
                        }
                    }
                    default {
                        Write-Warning "Release Plan ID $($releasePlan.id): Both Management Plane and DataPlane not in scope"
                        $null
                    }
                }
            }
        }

        if ($kpiId) {
            AddAttestationEntry $productServiceTreeId $kpiId $COMPLETED $productType $url $productName
        }

        Update-AttestationStatusInWorkItem -workItemId $releasePlan.id -fieldName "Custom.AttestationStatus" -status "Completed"
    } catch {
        Write-Warning "Error processing release plan item [$($releasePlan.id)]`nException message: $($_.Exception.Message)"

        $failedAttestations += @{
            productId = $productServiceTreeId
            productName = $productName
            workItemId = $releasePlan.id
            affectedKpi = $KPI_ID_TO_TITLE[$kpiId]
            error = $_.Exception.Message
        }
    }
}

SendEmailNotification -emailTo $EMAIL_TO -emailCC $EMAIL_CC -emailSubject "CPEX Attestation Summary - Successful KPI Entries" -emailBody (BuildSuccessEmailBody)
if ($failedAttestations.Count -ne 0) {
    SendEmailNotification -emailTo $EMAIL_TO -emailCC $EMAIL_CC -emailSubject "CPEX Attestation Summary - Failed KPI Entries" -emailBody (BuildFailureEmailBody)
}