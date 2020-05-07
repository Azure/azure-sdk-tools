#!/usr/bin/env pwsh

# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

#Requires -Version 6.0
#Requires -PSEdition Core
#Requires -Modules @{ModuleName='Az.Accounts'; ModuleVersion='1.6.4'}
#Requires -Modules @{ModuleName='Az.Resources'; ModuleVersion='1.8.0'}

[CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param (
    # Limit $BaseName to enough characters to be under limit plus prefixes, and https://docs.microsoft.com/azure/architecture/best-practices/resource-naming.
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^[-a-zA-Z0-9\.\(\)_]{0,80}(?<=[a-zA-Z0-9\(\)])$')]
    [string] $BaseName,

    [Parameter(Mandatory = $true)]
    [string] $ServiceDirectory,

    [Parameter()]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $TestApplicationId,

    [Parameter()]
    [string] $TestApplicationSecret,

    [Parameter()]
    [ValidateRange(0, [int]::MaxValue)]
    [int] $DeleteAfterHours,

    [Parameter()]
    [string] $Location = '',

    [Parameter()]
    [string] $Environment = 'AzureCloud',

    [Parameter()]
    [hashtable] $AdditionalParameters,

    [Parameter(ParameterSetName = 'SubscriptionConfiguration', Mandatory = $true)]
    [string] $SubscriptionConfiguration = '',

    [Parameter(ParameterSetName = 'SubscriptionConfiguration', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $KeyVaultName,

    [Parameter(ParameterSetName = 'SubscriptionConfiguration', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $KeyVaultTenantId,

    [Parameter(ParameterSetName = 'SubscriptionConfiguration', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $KeyVaultAppId,

    [Parameter(ParameterSetName = 'SubscriptionConfiguration', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $KeyVaultAppSecret,

    [Parameter()]
    [switch] $CI = ($null -ne $env:SYSTEM_TEAMPROJECTID),

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $OutFile
)

# By default stop for any error.
if (!$PSBoundParameters.ContainsKey('ErrorAction')) {
    $ErrorActionPreference = 'Stop'
}

function Log($Message) {
    Write-Host ('{0} - {1}' -f [DateTime]::Now.ToLongTimeString(), $Message)
}

function Retry([scriptblock] $Action, [int] $Attempts = 5) {
    $attempt = 0
    $sleep = 5

    while ($attempt -lt $Attempts) {
        try {
            $attempt++
            return $Action.Invoke()
        } catch {
            if ($attempt -lt $Attempts) {
                $sleep *= 2

                Write-Warning "Attempt $attempt failed: $_. Trying again in $sleep seconds..."
                Start-Sleep -Seconds $sleep
            } else {
                Write-Error -ErrorRecord $_
            }
        }
    }
}

# Support actions to invoke on exit.
$exitActions = @({
    if ($exitActions.Count -gt 1) {
        Write-Verbose 'Running registered exit actions'
    }
})

trap {
    # Like using try..finally in PowerShell, but without keeping track of more braces or tabbing content.
    $exitActions.Invoke()
}

# Enumerate test resources to deploy. Fail if none found.
$repositoryRoot = "$PSScriptRoot/../../.." | Resolve-Path
$root = [System.IO.Path]::Combine($repositoryRoot, "sdk", $ServiceDirectory) | Resolve-Path
$templateFileName = 'test-resources.json'
$templateFiles = @()

Write-Verbose "Checking for '$templateFileName' files under '$root'"
Get-ChildItem -Path $root -Filter $templateFileName -Recurse | ForEach-Object {
    $templateFile = $_.FullName

    Write-Verbose "Found template '$templateFile'"
    $templateFiles += $templateFile
}

if (!$templateFiles) {
    Write-Warning -Message "No template files found under '$root'"
    exit
}

# If there is a value for $SubscriptionConfiguration look it up and set
# script-level variables for subsequent steps.
if ($SubscriptionConfiguration) {
    Write-Verbose "Using subscription configuration $SubscriptionConfiguration from KeyVault $KeyVaultName..."
    $keyVaultSecret = ConvertTo-SecureString -String $KeyVaultAppSecret -AsPlainText -Force
    $keyvaultCredential = [System.Management.Automation.PSCredential]::new($KeyVaultAppId, $keyVaultSecret)

    $keyVaultAccount = Retry {
        Connect-AzAccount -Tenant $KeyVaultTenantId -Credential $keyvaultCredential -ServicePrincipal -Environment $Environment
    }

    $exitActions += {
        Write-Verbose "Logging out of service principal '$($keyVaultAccount.Context.Account)'"
        $null = Disconnect-AzAccount -AzureContext $keyVaultAccount.Context
    }

    $subscriptionSecret = Get-AzKeyVaultSecret -VaultName $KeyVaultName -Name $SubscriptionConfiguration
    $subscriptionParameters = ($subscriptionSecret.SecretValueText | ConvertFrom-JSON)

    $SubscriptionId = $subscriptionParameters.SubscriptionId
    $TenantId = $subscriptionParameters.TenantId
    $TestApplicationId = $subscriptionParameters.TestApplicationId
    $TestApplicationSecret = $subscriptionParameters.TestApplicationSecret
    $TestApplicationOid = $subscriptionParameters.TestApplicationOid
    $ProvisionerApplicationId = $subscriptionParameters.ProvisionerApplicationId
    $ProvisionerApplicationSecret = $subscriptionParameters.ProvisionerApplicationSecret
    $Environment = $subscriptionParameters.Environment

    $null = Disconnect-AzAccount -AzureContext $keyVaultAccount.Context

    Write-Verbose "Subscription parameters set. Using Subscription: $SubscriptionId"
}


# If no location is specified use safe default locations for the given
# environment. If no matching environment is found $Location remains an empty
# string.
if (!$Location) {
    $Location = @{
        'AzureCloud' = 'westus2';
        'AzureUSGovernment' = 'usgovvirginia';
        'AzureChinaCloud' = 'chinaeast2';
    }[$Environment]

    Write-Verbose "Location was not set. Using default location for environment: '$Location'"
}

# Log in if requested; otherwise, the user is expected to already be authenticated via Connect-AzAccount.
if ($ProvisionerApplicationId) {
    $null = Disable-AzContextAutosave -Scope Process

    Log "Logging into service principal '$ProvisionerApplicationId'"
    $provisionerSecret = ConvertTo-SecureString -String $ProvisionerApplicationSecret -AsPlainText -Force
    $provisionerCredential = [System.Management.Automation.PSCredential]::new($ProvisionerApplicationId, $provisionerSecret)

    # Use the given subscription ID if provided.
    $subscriptionArgs = if ($SubscriptionId) {
        @{SubscriptionId = $SubscriptionId}
    }

    $provisionerAccount = Retry {
        Connect-AzAccount -Tenant $TenantId -Credential $provisionerCredential -ServicePrincipal -Environment $Environment @subscriptionArgs
    }

    $exitActions += {
        Write-Verbose "Logging out of service principal '$($provisionerAccount.Context.Account)'"
        $null = Disconnect-AzAccount -AzureContext $provisionerAccount.Context
    }
}

# Get test application OID from ID if not already provided.
if ($TestApplicationId -and !$TestApplicationOid) {
    $testServicePrincipal = Retry {
        Get-AzADServicePrincipal -ApplicationId $TestApplicationId
    }

    if ($testServicePrincipal -and $testServicePrincipal.Id) {
        $script:TestApplicationOid = $testServicePrincipal.Id
    }
}


# If the ServiceDirectory is an absolute path use the last directory name
# (e.g. D:\foo\bar\ -> bar)
$serviceName = if (Split-Path -IsAbsolute  $ServiceDirectory) {
    Split-Path -Leaf $ServiceDirectory
} else {
    $ServiceDirectory
}

# Format the resource group name based on resource group naming recommendations and limitations.
$resourceGroupName = if ($CI) {
    $BaseName = 't' + (New-Guid).ToString('n').Substring(0, 16)
    Write-Verbose "Generated base name '$BaseName' for CI build"

    "rg-{0}-$BaseName" -f ($serviceName -replace '[\\\/:]', '-').Substring(0, [Math]::Min($serviceName.Length, 90 - $BaseName.Length - 4)).Trim('-')
} else {
    "rg-$BaseName"
}

# Tag the resource group to be deleted after a certain number of hours if specified.
$tags = @{
    Creator = if ($env:USER) { $env:USER } else { "${env:USERNAME}" }
    ServiceDirectory = $ServiceDirectory
}

if ($PSBoundParameters.ContainsKey('DeleteAfterHours')) {
    $deleteAfter = [DateTime]::UtcNow.AddHours($DeleteAfterHours)
    $tags.Add('DeleteAfter', $deleteAfter.ToString('o'))
}

if ($CI) {
    # Add tags for the current CI job.
    $tags += @{
        BuildId = "${env:BUILD_BUILDID}"
        BuildJob = "${env:AGENT_JOBNAME}"
        BuildNumber = "${env:BUILD_BUILDNUMBER}"
        BuildReason = "${env:BUILD_REASON}"
    }

    # Set the resource group name variable.
    Write-Host "Setting variable 'AZURE_RESOURCEGROUP_NAME': $resourceGroupName"
    Write-Host "##vso[task.setvariable variable=AZURE_RESOURCEGROUP_NAME;]$resourceGroupName"
}

Log "Creating resource group '$resourceGroupName' in location '$Location'"
$resourceGroup = Retry {
    New-AzResourceGroup -Name "$resourceGroupName" -Location $Location -Tag $tags -Force:$Force
}

if ($resourceGroup.ProvisioningState -eq 'Succeeded') {
    # New-AzResourceGroup would've written an error and stopped the pipeline by default anyway.
    Write-Verbose "Successfully created resource group '$($resourceGroup.ResourceGroupName)'"
}

# Populate the template parameters and merge any additional specified.
$templateParameters = @{
    baseName = $BaseName
    testApplicationId = $TestApplicationId
    testApplicationOid = "$TestApplicationOid"
}

if ($TenantId) {
    $templateParameters.Add('tenantId', $TenantId)
}
if ($TestApplicationSecret) {
    $templateParameters.Add('testApplicationSecret', $TestApplicationSecret)
}
if ($AdditionalParameters) {
    $templateParameters += $AdditionalParameters
}

# Try to detect the shell based on the parent process name (e.g. launch via shebang).
$shell, $shellExportFormat = if (($parentProcessName = (Get-Process -Id $PID).Parent.ProcessName) -and $parentProcessName -eq 'cmd') {
    'cmd', 'set {0}={1}'
} elseif (@('bash', 'csh', 'tcsh', 'zsh') -contains $parentProcessName) {
    'shell', 'export {0}={1}'
} else {
    'PowerShell', '$env:{0} = ''{1}'''
}

foreach ($templateFile in $templateFiles) {
    # Deployment fails if we pass in more parameters than are defined.
    Write-Verbose "Removing unnecessary parameters from template '$templateFile'"
    $templateJson = Get-Content -LiteralPath $templateFile | ConvertFrom-Json
    $templateParameterNames = $templateJson.parameters.PSObject.Properties.Name

    $templateFileParameters = $templateParameters.Clone()
    foreach ($key in $templateParameters.Keys) {
        if ($templateParameterNames -notcontains $key) {
            Write-Verbose "Removing unnecessary parameter '$key'"
            $templateFileParameters.Remove($key)
        }
    }

    $preDeploymentScript = $templateFile | Split-Path | Join-Path -ChildPath 'test-resources-pre.ps1'
    if (Test-Path $preDeploymentScript) {
        Log "Invoking pre-deployment script '$preDeploymentScript'"
        &$preDeploymentScript -ResourceGroupName $resourceGroupName @PSBoundParameters
    }

    Log "Deploying template '$templateFile' to resource group '$($resourceGroup.ResourceGroupName)'"
    $deployment = Retry {
        New-AzResourceGroupDeployment -Name $BaseName -ResourceGroupName $resourceGroup.ResourceGroupName -TemplateFile $templateFile -TemplateParameterObject $templateFileParameters
    }

    if ($deployment.ProvisioningState -eq 'Succeeded') {
        # New-AzResourceGroupDeployment would've written an error and stopped the pipeline by default anyway.
        Write-Verbose "Successfully deployed template '$templateFile' to resource group '$($resourceGroup.ResourceGroupName)'"
    }

    $serviceDirectoryPrefix = $serviceName.ToUpperInvariant() + "_"

    $context = Get-AzContext;

    # Add default values
    $deploymentOutputs = @{
        "$($serviceDirectoryPrefix)CLIENT_ID" = $TestApplicationId;
        "$($serviceDirectoryPrefix)CLIENT_SECRET" = $TestApplicationSecret;
        "$($serviceDirectoryPrefix)TENANT_ID" = $context.Tenant.Id;
        "$($serviceDirectoryPrefix)SUBSCRIPTION_ID" =  $context.Subscription.Id;
        "$($serviceDirectoryPrefix)RESOURCE_GROUP" = $resourceGroup.ResourceGroupName;
        "$($serviceDirectoryPrefix)LOCATION" = $resourceGroup.Location;
        "$($serviceDirectoryPrefix)ENVIRONMENT" = $context.Environment.Name;
    }

    foreach ($key in $deployment.Outputs.Keys) {
        $variable = $deployment.Outputs[$key]

        # Work around bug that makes the first few characters of environment variables be lowercase.
        $key = $key.ToUpperInvariant()

        if ($variable.Type -eq 'String' -or $variable.Type -eq 'SecureString') {
            $deploymentOutputs[$key] = $variable.Value
        }
    }

    if ($OutFile)
    {
        if (!$IsWindows)
        {
            Write-Host "File option is supported only on Windows"
        }

        $outputFile = "$templateFile.env"

        $environmentText = $deploymentOutputs | ConvertTo-Json;
        $bytes = ([System.Text.Encoding]::UTF8).GetBytes($environmentText)
        $protectedBytes = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)

        Set-Content $outputFile -Value $protectedBytes -AsByteStream -Force

        Write-Host "Test environment settings`n $environmentText`nstored into encrypted $outputFile"
    }
    else
    {
        
        if (!$CI) {
            # Write an extra new line to isolate the environment variables for easy reading.
            Log "Persist the following environment variables based on your detected shell ($shell):`n"
        }

        foreach ($key in $deploymentOutputs.Keys)
        {
            $value = $deploymentOutputs[$key]
            
            if ($CI) {
                # Treat all ARM template output variables as secrets since "SecureString" variables do not set values.
                # In order to mask secrets but set environment variables for any given ARM template, we set variables twice as shown below.
                Write-Host "Setting variable '$key': ***"
                Write-Host "##vso[task.setvariable variable=_$key;issecret=true;]$($value)"
                Write-Host "##vso[task.setvariable variable=$key;]$($value)"
            } else {
                Write-Host ($shellExportFormat -f $key, $value)
            }
        }

        if ($key) {
            # Isolate the environment variables for easy reading.
            Write-Host "`n"
            $key = $null
        }
    }

    $postDeploymentScript = $templateFile | Split-Path | Join-Path -ChildPath 'test-resources-post.ps1'
    if (Test-Path $postDeploymentScript) {
        Log "Invoking post-deployment script '$postDeploymentScript'"
        &$postDeploymentScript -ResourceGroupName $resourceGroupName -DeploymentOutputs $deploymentOutputs @PSBoundParameters
    }
}

$exitActions.Invoke()

<#
.SYNOPSIS
Deploys live test resources defined for a service directory to Azure.

.DESCRIPTION
Deploys live test resouces specified in test-resources.json files to a resource
group.

This script searches the directory specified in $ServiceDirectory recursively
for files named test-resources.json. All found test-resources.json files will be
deployed to the test resource group.

If no test-resources.json files are located the script exits without making
changes to the Azure environment.

A service principal must first be created before this script is run and passed
to $TestApplicationId and $TestApplicationSecret. Test resources will grant this
service principal access.

This script uses credentials already specified in Connect-AzAccount or those
specified in $ProvisionerApplicationId and $ProvisionerApplicationSecret.

.PARAMETER BaseName
A name to use in the resource group and passed to the ARM template as 'baseName'.
Limit $BaseName to enough characters to be under limit plus prefixes specified in
the ARM template. See also https://docs.microsoft.com/azure/architecture/best-practices/resource-naming

Note: The value specified for this parameter will be overriden and generated
by New-TestResources.ps1 if $CI is specified.

.PARAMETER ServiceDirectory
A directory under 'sdk' in the repository root - optionally with subdirectories
specified - in which to discover ARM templates named 'test-resources.json'.
This can also be an absolute path or specify parent directories.

.PARAMETER TestApplicationId
The AAD Application ID to authenticate the test runner against deployed
resources. Passed to the ARM template as 'testApplicationId'.

This application is used by the test runner to execute tests against the
live test resources.

.PARAMETER TestApplicationSecret
Optional service principal secret (password) to authenticate the test runner
against deployed resources. Passed to the ARM template as
'testApplicationSecret'.

This application is used by the test runner to execute tests against the
live test resources.

.PARAMETER DeleteAfterHours
Optional. Positive integer number of hours from the current time to set the
'DeleteAfter' tag on the created resource group. The computed value is a
timestamp of the form "2020-03-04T09:07:04.3083910Z".

If this value is not specified no 'DeleteAfter' tag will be assigned to the
created resource group.

An optional cleanup process can delete resource groups whose "DeleteAfter"
timestamp is less than the current time.

This isused for CI automation.

.PARAMETER Location
Optional location where resources should be created. If left empty, the default
is based on the cloud to which the template is being deployed:

* AzureCloud -> 'westus2'
* AzureUSGovernment -> 'usgovvirginia'
* AzureChinaCloud -> 'chinaeast2'

.PARAMETER Environment
Name of the cloud environment. The default is the Azure Public Cloud
('PublicCloud')

.PARAMETER AdditionalParameters
Optional key-value pairs of parameters to pass to the ARM template(s).

.PARAMETER SubscriptionConfiguration
Name of a subscription configuration secret in a Key Vault. Stored as a JSON
object with the expected properties:
    * SubscriptionId
    * TenantId
    * TestApplicationId
    * TestApplicationSecret
    * TestApplicationOid
    * ProvisionerApplicationId
    * ProvisoinerApplicationSecret
    * Environment

.PARAMETER KeyVaultName
Name of the Key Vault which holds the subscription configuration

.PARAMETER KeyVaultTenantId
AAD tenant ID for an app that has access to the Key Vault

.PARAMETER KeyVaultAppId
AAD app ID for an app that has access to the Key Vault

.PARAMETER KeyVaultAppSecret
AAD app secret for an app that has access to the Key Vault

.PARAMETER CI
Indicates the script is run as part of a Continuous Integration / Continuous
Deployment (CI/CD) build (only Azure Pipelines is currently supported).

.PARAMETER Force
Force creation of resources instead of being prompted.

.PARAMETER OutFile
Save test environment settings into a test-resources.json.env file next to test-resources.json. File is protected via DPAPI. Supported only on windows.
The environment file would be scoped to the current repository directory.

.EXAMPLE
Connect-AzAccount -Subscription "REPLACE_WITH_SUBSCRIPTION_ID"
$testAadApp = New-AzADServicePrincipal -Role Owner -DisplayName 'azure-sdk-live-test-app'
New-TestResources.ps1 `
    -BaseName 'uuid123' `
    -ServiceDirectory 'keyvault' `
    -TestApplicationId $testAadApp.ApplicationId.ToString() `
    -TestApplicationSecret (ConvertFrom-SecureString $testAadApp.Secret -AsPlainText)

Run this in a desktop environment to create new AAD apps and Service Principals
that can be used to provision resources and run live tests.

Requires PowerShell 7 to use ConvertFrom-SecureString -AsPlainText or convert
the SecureString to plaintext by another means.

.EXAMPLE
New-TestResources.ps1 `
    -BaseName 'Generated' `
    -ServiceDirectory $(ServiceDirectory) `
    -Location '$(Location)' `
    -DeleteAfterHours 24 `
    -AdditionalParameters @{ additionalParam1 = 'value'; additionalParam2 = 'value' } `
    -SubscriptionConfiguration $(SubscriptionConfigurationName) `
    -KeyVaultName $(KeyVaultName) `
    -KeyVaultTenantId $(KeyVaultTenantId) `
    -KeyVaultAppId $(KeyVaultAppId) `
    -KeyVaultAppSecret $(KeyVaultSecret) `
    -CI `
    -Force `
    -Verbose

Run this in an Azure DevOps CI (with approrpiate variables configured) before
executing live tests. The script will output variables as secrets (to enable
log redaction).

.LINK
Remove-TestResources.ps1
#>
