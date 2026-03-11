<#
.SYNOPSIS
Adds a CPEX KPI attestation entry to the Kusto database.

.DESCRIPTION
This script is designed to support both automated and manual workflows for CPEX KPI attestation.
It inserts a single attestation record into the specified Kusto table, capturing key metadata such as the action item ID, target product ID and type, attestation status, and evidence URL.
In automated scenarios, it can be invoked as part of a larger pipeline to log attestations programmatically.
For manual use, it enables engineers to insert individual entries when automation is not applicable or when validating edge cases.

.PARAMETER TableName
The name of the Kusto table where attestation entries will be written.

.PARAMETER ActionItemId
The unique identifier for the KPI action item being attested.

.PARAMETER TargetId
The Service Tree ID of the product being attested.

.PARAMETER TargetType
The type of the product (e.g., ProductSku, Feature, Offering).

.PARAMETER Status
The attestation status value (e.g., 0 for Incomplete, 1 for Completed, 3 for Not Applicable).

.PARAMETER EvidenceUrl
The URL pointing to the evidence supporting the attestation, i.e. link to release plan
#>

param (
    [Parameter(Mandatory = $true)]
    [ValidateSet('ProdKpiEvidenceStream', 'TestKpiEvidenceStream')]
    [string] $TableName,

    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "ba2c80d5-b8be-465f-8948-283229082fd1",
        "e0504da9-8897-41db-a75f-5027298ba410",
        "dfe9c112-416e-4e0a-8012-4a3a29807782",
        "84715402-4f3c-4dca-b330-f05206abaec5",
        "ad70777b-a1f5-4d77-8926-5c466d7a214d",
        "210c095f-b3a2-4cf4-a899-eaab4c3ed958",
        "da768dff-8f90-4999-ad3a-adcd790911f3"
    )]
    [string] $ActionItemId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$')]
    [string] $TargetId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Service', 'Offering', 'ProductSku', 'Feature')]
    [string] $TargetType,

    [Parameter(Mandatory = $true)]
    [ValidateSet(0, 1, 3)]
    [int] $Status,

    [Parameter(Mandatory = $true)]
    [string] $EvidenceUrl

)

function InvokeKustoCommand($command) {
    try {
        $clusterUri = "https://azsdk-cpex-attestation.westus2.kusto.windows.net"
        $databaseName = "CPEX_Attestation_DB"
        $accessToken = az account get-access-token --resource "https://api.kusto.windows.net" --query "accessToken" --output tsv
        $headers = @{ Authorization="Bearer $accessToken" }
        $body = @{ csl = $command; db = $databaseName } | ConvertTo-Json -Depth 3
        Invoke-RestMethod -Uri "$clusterUri/v1/rest/mgmt" -Headers $headers -Method Post -Body $body -ContentType "application/json"
    } catch {
        $errorMessage = "Failed to invoke Kusto command: $command`nException message: $($_.Exception.Message)"
        throw $errorMessage
    }

}

$command = @"
.append $TableName <|
print
    ActionItemId = "$ActionItemId",
    TargetId = "$TargetId",
    TargetType = "$TargetType",
    Status = int($Status),
    CreatedTime = datetime(now),
    EvidenceUrl = "$EvidenceUrl"
"@

Write-Host "Adding attestation entry for product [$targetId], with status [$status] for KPI action id [$actionItemId]."
InvokeKustoCommand $command
Write-Host "Added attestation entry for product [$targetId], with status [$status] for KPI action id [$actionItemId]."