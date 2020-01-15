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
    [string] $ProvisionerApplicationTenantId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $SubscriptionId,

    [Parameter()]
    [switch] $Force
)

Write-Verbose "Logging in"
az login --service-principal --username=$ProvisionerApplicationId --password=$ProvisionerApplicationSecret --tenant=$ProvisionerApplicationTenantId
Write-Verbose "Setting account"
az account set --subscription=$SubscriptionId

Write-Verbose "Fetching groups"
$allGroups = az group list | ConvertFrom-Json

Write-Verbose "Total Resource Groups: $($allGroups.Length)"

$now = [DateTime]::UtcNow

$toDelete = $allGroups |
    where {
        $parsedTime = [DateTime]::MaxValue
        $canParse = [DateTime]::TryParse($_.tags.DeleteAfter, [ref]$parsedTime)
        $canParse -and ($now -gt $parsedTime)
    }

Write-Verbose "Groups to delete: $($toDelete.Length)"

$toDelete | foreach {
    if ($Force -or $PSCmdlet.ShouldProcess("$($_.Name) (UTC: $($_.tags.DeleteAfter))", "Delete Group")) {
        Write-Verbose "Deleting group: $($_.Name)"
        az group delete --name $_.Name --yes --no-wait
    }
}
