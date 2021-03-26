#!/usr/bin/env pwsh

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

#Requires -Version 6.0
#Requires -PSEdition Core

[CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param (
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $ProvisionerApplicationId,

    [Parameter(Mandatory = $true)]
    [string] $ProvisionerApplicationSecret,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [ValidateNotNullOrEmpty()]
    [string] $TenantId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $SubscriptionId,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    $Environment = "AzureCloud",

    [Parameter()]
    [switch] $Force,

    [Parameter(ValueFromRemainingArguments = $true)]
    $IgnoreUnusedArguments
)

Write-Verbose "Logging in"
az cloud set --name $Environment
az login --service-principal --username=$ProvisionerApplicationId --password=$ProvisionerApplicationSecret --tenant=$TenantId
Write-Verbose "Setting account"
az account set --subscription=$SubscriptionId

Write-Verbose "Fetching groups"
$allGroups = az group list | ConvertFrom-Json

Write-Host "Total Resource Groups: $($allGroups.Count)"

$now = [DateTime]::UtcNow

$noDeleteAfter = $allGroups.Where({ !($_.tags -and $_.tags.PSObject.Members.name -contains "DeleteAfter") })
Write-Host "Subscription contains $($noDeleteAfter.Count) resource groups with no DeleteAfter tags"
$noDeleteAfter | ForEach-Object { Write-Verbose $_.name }

$hasDeleteAfter = $allGroups.Where({ $_.tags -and $_.tags.PSObject.Members.name -contains "DeleteAfter" })

$toDelete = $hasDeleteAfter.Where({ $now -gt [DateTime]$_.tags.DeleteAfter })

Write-Host "Groups to delete: $($toDelete.Count)"

foreach ($rg in $toDelete)
{
  if ($Force -or $PSCmdlet.ShouldProcess("$($rg.Name) (UTC: $($rg.tags.DeleteAfter))", "Delete Group")) {
    Write-Verbose "Deleting group: $($rg.Name)"
    Write-Verbose "  tags $($rg.tags)"
    az group delete --name $rg.Name --yes --no-wait
  }
}
