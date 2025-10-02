<#
.SYNOPSIS
Automates CPEX KPI attestation by updating work items and logging evidence to Kusto.

.DESCRIPTION
This script processes triage and release plan work items to validate whether the product meets the required KPI criteria for their lifecycle stage.
It adds attestation entries to a Kusto table and updates work item fields accordingly. It also sends email notifications and includes error handling for service interactions.

.PARAMETER TableName
The name of the Kusto table where attestation entries will be written. Default is "ProdKpiEvidenceStream".

.PARAMETER ReleasePlanWorkItemId
Specifies a single release plan work item to process for KPI attestation. If omitted, all eligible items are considered.

.PARAMETER TriageWorkItemId
Specifies a single triage work item to process for KPI attestation. If omitted, all eligible items are considered.

.PARAMETER TargetServiceTreeId
Specifies the Serivice Tree Id of the product whose attestation status should be evaluated. Used to scope triage and release plan queries to a specific product. 
#>
param (
    [Parameter(Mandatory = $false)]
    [string] $TableName = "ProdKpiEvidenceStream",

    [Parameter(Mandatory = $false)]
    [string] $ReleasePlanWorkItemId,

    [Parameter(Mandatory = $false)]
    [string] $TriageWorkItemId,

    [Parameter(Mandatory = $false)]
    [string] $TargetServiceTreeId = "407ed0da-e187-49c4-a65b-f23cc058265f"
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

$KPI_ID_TO_TITLE = @{
    $KPI_ID_Onboarding             = "Onboarding"
    $KPI_ID_Mgmt_Private_Preview   = "Management Plane - Private Preview API readiness"
    $KPI_ID_Data_Private_Preview   = "Data Plane - Private Preview API readiness"
    $KPI_ID_Mgmt_Public_Preview    = "Management Plane - API readiness and Beta SDK release"
    $KPI_ID_Data_Public_Preview    = "Data Plane - API readiness and Beta SDK release"
    $KPI_ID_Mgmt_GA                = "Management Plane - API readiness and Stable SDK release"
    $KPI_ID_Data_GA                = "Data Plane - API readiness and Stable SDK release"
}

$SERVICE_TREE_URL = "https://microsoftservicetree.com/products/"

$successfulAttestations = @{}
$failedAttestations = @()

function InvokeKustoCommand($command) {
    try {
        $clusterUri = "https://azsdk-cpex-attestation.westus2.kusto.windows.net"
        $databaseName = "CPEX_Attestation_DB"
        $accessToken = az account get-access-token --resource "https://api.kusto.windows.net" --query "accessToken" --output tsv
        $headers = @{ Authorization="Bearer $accessToken" }
        $body = @{ csl = $command; db = $databaseName } | ConvertTo-Json -Depth 3
        Invoke-RestMethod -Uri "$clusterUri/v1/rest/mgmt" -Headers $headers -Method Post -Body $body -ContentType "application/json"
    } catch {
        Write-Error "Failed to invoke Kusto command: $command"
        Write-Error "Exception message: $($_.Exception.Message)"
        throw "Terminating due to failure in invoking kusto command"
    }

}

function AddAttestationEntry($targetId, $actionItemId, $status, $targetType, $url, $productName) {
    $command = @"
.append $TableName <|
print
    ActionItemId = "$actionItemId",
    TargetId = "$targetId",
    TargetType = "$targetType",
    Status = int($status),
    CreatedTime = datetime(now),
    EvidenceUrl = "$url"
"@

    Write-Host "Adding attestation entry for product [$targetId], with status [$status] for KPI action id [$actionItemId]."
    InvokeKustoCommand $command
    Write-Host "Added attestation entry for product [$targetId], with status [$status] for KPI action id [$actionItemId]."

    if (-not $successfulAttestations.ContainsKey($actionItemId)) {
        $successfulAttestations[$actionItemId] = @()
    }

    $successfulAttestations[$actionItemId] += @{
        productId = $targetId
        productName = $productName
        status = if ($status -eq $COMPLETED) { "Completed" } else { "Not Applicable"}
    }
}

function SendEmailNotification($emailTo, $emailCC, $emailSubject, $emailBody) {
    try {
        $emailerUri = $env:azure_sdk_emailer_uri
        $body = @{ EmailTo = $emailTo; EmailCC = $emailCC; Subject = $emailSubject; Body = $emailBody} | ConvertTo-Json -Depth 3
        $response = Invoke-RestMethod -Uri $emailerUri -Method Post -Body $body -ContentType "application/json"
    } catch {
        Write-Error "Failed to send email."
        Write-Error "To: $emailTo"
        Write-Error "CC: $emailCC"
        Write-Error "Subject: $emailSubject"
        Write-Error "Body: $emailBody"
        Write-Error "Exception message: $($_.Exception.Message)"
    }
}

function BuildSuccessEmailBody {
    if ($successfulAttestations.Count -eq 0) {
        return "No products were marked as Completed or Not Applicable for any KPI in this latest run."
    }

    $body = "<h2>Successful CPEX KPI Attestations</h2>"
    foreach ($kpiId in $successfulAttestations.Keys) {
        if ($successfulAttestations[$kpiId].Count -ne 0) {
            $title = $KPI_ID_TO_TITLE[$kpiId]
            $body += "<h3> $title ($kpiId)</h3><ul>"
            foreach ($entry in $successfulAttestations[$kpiId]) {
                $productName = $entry.productName
                $productId = $entry.productId
                $status = $entry.status
                $link = "$SERVICE_TREE_URL$productId"
                $body += "<li><strong>$productName</strong></li> (<a href=$link>$productId</a>): $status"
            }
        }

        $body += "</ul>"
    }
    
    return $body
}

function BuildFailureEmailBody {
    if ($failedAttestations.Count -eq 0) {
        return "No errors occurred during the latest CPEX KPI attestation automated run. All entries were processed sucessfully."
    }

    $body = "The following errors occurred during the latest CPEX KPI attestation automated run: `n`n"
    
    foreach ($entry in $failedAttestations) {
        $productName = $entry.productName
        $productId = $entry.productId
        $link = "$SERVICE_TREE_URL$productId"
        $workItemId = $entry.workItemId
        $failure = $entry.error
        $body += "<li><strong>$productName</strong></li> (<a href=$link>$productId</a>)"
        $body += "Work Item Id: $workItemId</ul>"
        $body += "Error: $failure</ul>"
    }

    $body += "</ul>"

    return $body
}

$triages =  Get-TriagesForCPEXAttestation -triageWorkItemId $TriageWorkItemId -targetServiceTreeId $TargetServiceTreeId

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
        Write-Error "Error processing triage item [$($triage.id)]"
        Write-Error "Exception message: $($_.Exception.Message)"

        $failedAttestations += @{
            productId = $productServiceTreeId
            productName = $productName
            workItemId = $triage.id
            error = $_.Exception.Message
        }
    }
}

$releasePlans = Get-ReleasePlansForCPEXAttestation -releasePlanWorkItemId $ReleasePlanWorkItemId -targetServiceTreeId $TargetServiceTreeId

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
                        Write-Output "Release Plan ID $($releasePlan.id): Management plane in scope, unknown lifecycle $($lifecycle)"
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
                                Write-Output "Release Plan ID $($releasePlan.id): Dataplane in scope, unknown lifecycle $($lifecycle)"
                                $null
                            }
                        }
                    }
                    default {
                        Write-Output "Release Plan ID: Both Management Plane and DataPlane not in scope"
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
        Write-Error "Error processing release plan item [$($releasePlan.id)]"
        Write-Error "Exception message: $($_.Exception.Message)"
        $failedAttestations += @{
            productId = $productServiceTreeId
            productName = $productName
            workItemId = $releasePlan.id
            error = $_.Exception.Message
        }
    }
}

SendEmailNotification -emailTo $EMAIL_TO -emailCC $EMAIL_CC -emailSubject "CPEX Attestation Summary - Successful KPI Entries" -emailBody (BuildSuccessEmailBody)
SendEmailNotification -emailTo $EMAIL_TO -emailCC $EMAIL_CC -emailSubject "CPEX Attestation Summary - Failed KPI Entries" -emailBody (BuildFailureEmailBody)