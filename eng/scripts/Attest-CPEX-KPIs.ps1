Set-StrictMode -Version 3
. (Join-Path $PSScriptRoot "\..\common\scripts\Helpers\DevOps-WorkItem-Helpers.ps1")

$Incomplete = 0
$Complete = 1
$NA = 3

# Add KPI Ids after launch criteria creation
$KPI_ID_Onboarding_Private_Preview = "Onboarding_Private_Preview"
$KPI_ID_Mgmt_Private_Preview = "Mgmt_Private_Preview"
$KPI_ID_Data_Private_Preview = "Data_Private_Preview"
$KPI_ID_Mgmt_Public_Preview = "Mgmt_Public_Preview"
$KPI_ID_Data_Public_Preview = "Data_Public_Preview"
$KPI_ID_Mgmt_GA = "Mgmt_GA"
$KPI_ID_Data_GA = "Data_GA"

function InvokeKustoCommand($command) {
    $clusterUri = "https://azsdk-cpex-attestation.westus2.kusto.windows.net"
    $databaseName = "CPEX-attestations"
    $accessToken = az account get-access-token --resource "https://api.kusto.windows.net" --query "accessToken" --output tsv
    $headers = @{ Authorization="Bearer $accessToken" }
    Invoke-RestMethod -Uri "$clusterUri/v1/rest/mgmt" -Headers $headers -Method Post -Body (@{csl=$command; db=$databaseName} | ConvertTo-Json -Depth 3) -ContentType "application/json"
}

function AddAttestationEntry($targetId, $actionItemId, $status, $targetType, $url) {
    $tableName = "KPI_Attestations"

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
    $productServiceTreeId = $fields["Custom.ProductServiceTreeID"]
    $productType = $fields["Custom.ProductType"]
    $url = $triage.url

    AddAttestationEntry $productServiceTreeId $KPI_ID_Onboarding_Private_Preview $Complete $productType $url

     if ($dataScope -eq 'Yes') {
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Public_Preview $Incomplete $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Private_Preview $Incomplete $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_GA $Incomplete $productType $url
    }

    if ($mgmtScope -eq 'Yes') {
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Public_Preview $Incomplete $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Private_Preview $Incomplete $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_GA $Incomplete $productType $url
    }

    if ($dataScope -eq 'No') {
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Public_Preview $NA $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Private_Preview $NA $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Data_GA $NA $productType $url
    }

    if ($mgmtScope -eq 'No') {
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Public_Preview $NA $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_Private_Preview $NA $productType $url
        AddAttestationEntry $productServiceTreeId $KPI_ID_Mgmt_GA $NA $productType $url
    }

    Update-AttestationStatusInReleasePlan $triage.id "Completed"
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

    if ($dataScope -eq 'Yes') {
        switch -Wildcard ($lifecycle) {
            "*Public Preview*" {
                AddAttestationEntry $productServiceTreeId $KPI_ID_Data_Public_Preview $Complete $productType $url
            }
            "*Private Preview" {
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
            "*Private Preview" {
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

    Update-AttestationStatusInReleasePlan $releasePlan.id "Completed"
}