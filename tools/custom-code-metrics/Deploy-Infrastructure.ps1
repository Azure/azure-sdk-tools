#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [string]$ResourceGroup = "rg-azsdk-custom-code-metrics",
    [string]$Location = "westus2",
    [string]$SiteName = "azsdk-custom-code-metrics",
    [ValidatePattern("(?-i)^(?:[a-z0-9]{3,24})?$")][string]$StorageAccountName = "",
    [ValidatePattern("\S")][string]$Environment = "EngineeringSystem",
    [Parameter(Mandatory)][ValidatePattern("^[a-zA-Z0-9][a-zA-Z0-9-]*(?:@microsoft\.com)?$")][string]$Owner,
    [ValidatePattern("\S")][string]$Purpose = "Azure SDK custom code metrics",
    [Parameter(Mandatory)][guid]$BootstrapPrincipalId,
    [switch]$ProvisionResourcesOnly,
    [switch]$UseExistingResourceGroup,
    [switch]$PublicReports
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
if ($UseExistingResourceGroup -and -not $StorageAccountName) {
    throw "Existing-group deployment requires an explicit storage account name so collisions can be checked."
}
$operation = if ($UseExistingResourceGroup) {
    "Deploy new metrics resources without changing existing group metadata"
} else {
    "Create the resource group and deploy metrics infrastructure and requested access assignments"
}
if (-not $PSCmdlet.ShouldProcess("$SubscriptionId/$ResourceGroup", $operation)) {
    return
}

function Invoke-AzureJson {
    param([string[]]$Arguments)
    $result = & az @Arguments --subscription $SubscriptionId --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) { throw "Azure command failed: az $($Arguments[0]) $($Arguments[1])." }
    return ($result -join "`n") | ConvertFrom-Json
}

if ($UseExistingResourceGroup) {
    $group = Invoke-AzureJson -Arguments @("group", "show", "--name", $ResourceGroup)
    if ($group.id -ine "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup") {
        throw "The existing resource group does not match the requested subscription and name."
    }
    $names = @($SiteName, "id-azsdk-custom-code-metrics")
    if ($StorageAccountName) { $names += $StorageAccountName }
    $resources = @(Invoke-AzureJson -Arguments @("resource", "list", "--resource-group", $ResourceGroup))
    foreach ($resource in $resources) {
        if ($resource.name -in $names) {
            throw "A requested metrics resource already exists: $($resource.name). Refusing to overwrite existing resources."
        }
    }
} else {
    $null = Invoke-AzureJson -Arguments @("group", "create", "--name", $ResourceGroup, "--location", $Location,
        "--tags", "Owners=$Owner", "Purpose=$Purpose", "Project=Azure SDK custom code metrics", "Environment=$Environment")
}
$deploymentArguments = @("deployment", "group", "create", "--resource-group", $ResourceGroup,
    "--name", "custom-code-metrics", "--template-file", (Join-Path $PSScriptRoot "infra" "main.bicep"),
    "--mode", "Incremental",
    "--parameters", "location=$Location", "siteName=$SiteName", "owner=$Owner", "purpose=$Purpose", "environment=$Environment",
    "bootstrapPrincipalId=$BootstrapPrincipalId",
    "deployRoleAssignments=$((-not $ProvisionResourcesOnly).ToString().ToLowerInvariant())",
    "publicReports=$($PublicReports.IsPresent.ToString().ToLowerInvariant())")
if ($StorageAccountName) {
    $deploymentArguments += "storageAccountName=$StorageAccountName"
}
$deployment = Invoke-AzureJson -Arguments $deploymentArguments
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
