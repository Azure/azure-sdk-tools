Set-StrictMode -Version 3
. (Join-Path $PSScriptRoot ".." common scripts DevOps-WorkItem-Helpers.ps1)

$Incomplete = 0
$Complete = 1
$NA = 3

$KPI_ID_Onboarding = "ba2c80d5-b8be-465f-8948-283229082fd1"
$KPI_ID_Mgmt_Private_Preview = "e0504da9-8897-41db-a75f-5027298ba410"
$KPI_ID_Data_Private_Preview = "dfe9c112-416e-4e0a-8012-4a3a29807782"
$KPI_ID_Mgmt_Public_Preview = "84715402-4f3c-4dca-b330-f05206abaec5"
$KPI_ID_Data_Public_Preview = "ad70777b-a1f5-4d77-8926-5c466d7a214d"
$KPI_ID_Mgmt_GA = "210c095f-b3a2-4cf4-a899-eaab4c3ed958"
$KPI_ID_Data_GA = "da768dff-8f90-4999-ad3a-adcd790911f3"

function InvokeKustoCommand($command) {
    $clusterUri = "https://azsdk-cpex-attestation.westus2.kusto.windows.net"
    $databaseName = "CPEX_Attestation_DB"
    $accessToken = az account get-access-token --resource "https://api.kusto.windows.net" --query "accessToken" --output tsv
    $headers = @{ Authorization="Bearer $accessToken" }
    Invoke-RestMethod -Uri "$clusterUri/v1/rest/mgmt" -Headers $headers -Method Post -Body (@{csl=$command; db=$databaseName} | ConvertTo-Json -Depth 3) -ContentType "application/json"
}

function AddAttestationEntry($targetId, $actionItemId, $status, $targetType, $url) {
    $tableName = "KpiEvidenceStream"

    $command = @"
.append $tableName <|
print
    TargetId = "$targetId",
    ActionItemId = "$actionItemId",
    Status = int($status),
    TargetType = "$targetType",
    CreatedTime = datetime(now),
    EvidenceUrl = "$url"
"@

    InvokeKustoCommand $command
}

$triages =  Get-TriagesForCPEXAttestation

foreach ($triage in $triages) {
    $fields = $triage.fields
    $dataScope = $fields["Custom.DataScope"]
    $mgmtScope = $fields["Custom.MgmtScope"]
    $productLifecycle = $fields["Custom.ProductLifecycle"]
    $dataAttestationStatus = $fields["Custom.DataplaneAttestationStatus"]
    $mgmtAttestationStatus = $fields["Custom.ManagementPlaneAttestationStatus"]
    $productServiceTreeId = $fields["Custom.ProductServiceTreeID"]
    $productType = $fields["Custom.ProductType"]
    $url = $triage.url

    if ($productType -eq "Sku") {
        $productType = "ProductSku"
    }

    AddAttestationEntry $productServiceTreeId $KPI_ID_Onboarding $Complete $productType $url

    $lifecycleToDataKpis = @{
        "In Dev" = @($KPI_ID_Data_Private_Preview, $KPI_ID_Data_Public_Preview, $KPI_ID_Data_GA)
        "Private Preview" = @($KPI_ID_Data_Public_Preview, $KPI_ID_Data_GA)
        "Public Preview" = @($KPI_ID_Data_GA)
    }

    if ($dataAttestationStatus -ne "Completed") {
        if ($dataScope -eq "Yes") {
            Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.DataplaneAttestationStatus" -status "Complete"
        } else {
            Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.DataplaneAttestationStatus" -status "Not applicable"

            if ($lifecycleToDataKpis.ContainsKey($lifecycle)) {
                foreach ($kpiId in $lifecycleToDataKpis[$lifecycle]) {
                    AddAttestationEntry $productServiceTreeId $kpiId $NA $productType $url
                }
            }
        }
    }

    $lifecycleToMgmtKpis = @{
        "In Dev" = @($KPI_ID_Mgmt_Private_Preview, $KPI_ID_Mgmt_Public_Preview, $KPI_ID_Mgmt_GA)
        "Private Preview" = @($KPI_ID_Mgmt_Public_Preview, $KPI_ID_Mgmt_GA)
        "Public Preview" = @($KPI_ID_Mgmt_GA)
    }

    if ($mgmtAttestationStatus -ne "Completed") {
        if ($mgmtScope -eq "Yes") {
            Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.ManagementPlaneAttestationStatus" -status "Complete"
        } else {
            Update-AttestationStatusInWorkItem -workItemId $triage.id -fieldName "Custom.ManagementPlaneAttestationStatus" -status "Not applicable"
            if ($lifecycleToMgmtKpis.ContainsKey($lifecycle)) {
                foreach ($kpiId in $lifecycleToMgmtKpis[$lifecycle]) {
                    AddAttestationEntry $productServiceTreeId $kpiId $NA $productType $url
                }
            }
        }
    }

}

$releasePlans = Get-ReleasePlansForCPEXAttestation

foreach ($releasePlan in $releasePlans) {
    $fields = $releasePlan.fields
    $dataScope = $fields["Custom.DataScope"]
    $mgmtScope = $fields["Custom.MgmtScope"]
    $lifecycle = $fields["Custom.ReleasePlanType"]
    $productServiceTreeId = $fields["Custom.ProductServiceTreeID"]
    $productType = $fields["Custom.ProductType"]
    $url = $releasePlan.url

    if ($productType -eq "Sku") {
        $productType = "ProductSku"
    }
    
    if ($dataScope -eq 'Yes') {
        switch -Wildcard ($lifecycle) {
            "*Public Preview*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Public_Preview $Complete $productType $url
            }
            "*Private Preview*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Private_Preview $Complete $productType $url
            }
            "*GA*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Data_GA $Complete $productType $url
            }
            default {
                Write-Output "Release Plan ID $($releasePlan.id): Dataplane in scope, unknown lifecycle $($lifecycle)"
            }
        }
    }

    if ($mgmtScope -eq 'Yes') {
        switch -Wildcard ($lifecycle) {
            "*Public Preview*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Public_Preview $Complete $productType $url
            }
            "*Private Preview*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Private_Preview $Complete $productType $url
            }
            "*GA*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_GA $Complete $productType $url
            }
            default {
                Write-Output "Release Plan ID $($releasePlan.id): Management plane in scope, unknown lifecycle $($lifecycle)"
            }
        }
    }

    Update-AttestationStatusInWorkItem -workItemId $releasePlan.id -fieldName "Custom.AttestationStatus" -status "Completed"
}