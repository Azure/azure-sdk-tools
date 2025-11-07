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
    [string] $TableName,

    [Parameter(Mandatory = $true)]
    [string] $ActionItemId,

    [Parameter(Mandatory = $true)]
    [string] $TargetId,

    [Parameter(Mandatory = $true)]
    [string] $TargetType,

    [Parameter(Mandatory = $true)]
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