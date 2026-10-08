#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [string]$ResourceGroup = "rg-azsdk-custom-code-metrics",
    [string]$Location = "westus2",
    [string]$SiteName = "azsdk-custom-code-metrics",
    [Parameter(Mandatory)][ValidatePattern("^[a-zA-Z0-9][a-zA-Z0-9-]*(?:@microsoft\.com)?$")][string]$Owner,
    [ValidatePattern("\S")][string]$Purpose = "Azure SDK custom code metrics",
    [Parameter(Mandatory)][guid]$BootstrapPrincipalId,
    [switch]$ProvisionResourcesOnly,
    [switch]$PublicReports
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
if (-not $PSCmdlet.ShouldProcess("$SubscriptionId/$ResourceGroup", "Create the resource group and deploy metrics infrastructure and requested access assignments")) {
    return
}

function Invoke-AzureJson {
    param([string[]]$Arguments)
    $result = & az @Arguments --subscription $SubscriptionId --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) { throw "Azure command failed: az $($Arguments[0]) $($Arguments[1])." }
    return ($result -join "`n") | ConvertFrom-Json
}

$null = Invoke-AzureJson -Arguments @("group", "create", "--name", $ResourceGroup, "--location", $Location,
    "--tags", "Owners=$Owner", "Purpose=$Purpose", "Project=Azure SDK custom code metrics", "Environment=Playground")
$deployment = Invoke-AzureJson -Arguments @("deployment", "group", "create", "--resource-group", $ResourceGroup,
    "--name", "custom-code-metrics", "--template-file", (Join-Path $PSScriptRoot "infra" "main.bicep"),
    "--parameters", "location=$Location", "siteName=$SiteName", "owner=$Owner", "purpose=$Purpose",
    "bootstrapPrincipalId=$BootstrapPrincipalId",
    "deployRoleAssignments=$((-not $ProvisionResourcesOnly).ToString().ToLowerInvariant())",
    "publicReports=$($PublicReports.IsPresent.ToString().ToLowerInvariant())")
$outputs = [ordered]@{
    subscriptionId = $SubscriptionId
    resourceGroup = $ResourceGroup
    siteName = $SiteName
}
foreach ($property in $deployment.properties.outputs.PSObject.Properties) {
    $outputs[$property.Name] = $property.Value.value
}
[PSCustomObject]$outputs
if ($ProvisionResourcesOnly) {
    Write-Warning "Resources were provisioned without granting publishing access. An access administrator must configure the scoped roles before publishing."
}
if (-not $PublicReports) {
    Write-Warning "Reports are private. A policy-approved public reader API is required before deploying the hosted dashboard; do not point a browser at the private blob URL."
}
