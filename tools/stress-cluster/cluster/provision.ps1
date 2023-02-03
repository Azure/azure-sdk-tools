[CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
param (
    [string]$Environment = 'dev',
    [string]$Namespace = 'stress-infra',
    [switch]$Development = $false,
    # If provisioning an existing cluster and updating nodes, it must be done exclusively
    [switch]$UpdateNodes = $false,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $TenantId,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$')]
    [string] $ProvisionerApplicationId,

    [Parameter(ParameterSetName = 'Provisioner', Mandatory = $true)]
    [string] $ProvisionerApplicationSecret,

    [ValidateScript({
        if (!(Test-Path $_)) {
            throw "LocalAddonsPath $LocalAddonsPath does not exist"
        }
        return $true
    })]
    [System.IO.FileInfo]$LocalAddonsPath,

    # Enables passing full json credential config without throwing unrecognized parameter errors
    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArguments
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot "../../../eng/common/scripts/Helpers" PSModule-Helpers.ps1)
Install-ModuleIfNotInstalled -WhatIf:$false "powershell-yaml" "0.4.1" | Import-Module

$STATIC_TEST_DOTENV_NAME="public"
$VALUES_FILE = "$PSScriptRoot/kubernetes/stress-test-addons/values.yaml"
$STRESS_CLUSTER_RESOURCE_GROUP = ""

function _run([string]$CustomWhatIfFlag)
{
    if ($WhatIfPreference -and [string]::IsNullOrEmpty($CustomWhatIfFlag)) {
        Write-Host "`n==> [What if] $args`n" -ForegroundColor Green
        return
    } else {
        $cmdArgs = $args
        if ($WhatIfPreference) {
            $cmdArgs += $CustomWhatIfFlag
        }
        Write-Host "`n==> $cmdArgs`n" -ForegroundColor Green
        Invoke-Expression "$($cmdArgs -join ' ')"
    }
    if ($LASTEXITCODE) {
        Write-Error "Command '$args' failed with code: $LASTEXITCODE" -ErrorAction 'Continue'
    }
}

function Run()
{
    _run '' @args
}

function RunSupportingWhatIfFlag([string]$CustomWhatIfFlag)
{
    if ($WhatIfPreference) {
        _run $CustomWhatIfFlag @args
    } else {
        _run '' @args
    }
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

function RunOrExitOnFailure()
{
    $LASTEXITCODE = 0
    _run '' @args
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

function DeployStaticResources([hashtable]$params)
{
    Write-Host "Deploying static resources"

    $formattedTags = $params.tags.GetEnumerator() | ForEach-Object { "'$($_.Name)=$($_.Value)'" }
    $formattedTags = $formattedTags -join ' '

    RunOrExitOnFailure az group create `
        -n $params.staticTestKeyvaultGroup `
        -l $params.clusterLocation `
        --subscription $params.subscriptionId `
        --tags $formattedTags

    $kv = Run az keyvault show `
        -n $params.staticTestKeyvaultName `
        -g $params.staticTestKeyvaultGroup `
        --subscription $params.subscriptionId
    if (!$kv) {
        RunOrExitOnFailure az keyvault create `
            -n $params.staticTestKeyvaultName `
            -g $params.staticTestKeyvaultGroup `
            --subscription $params.subscriptionId
    }

    $values = GetEnvValues
    if ($values.provisionerAppId.$Environment) {
        $preExistingProvisionerApp = Run az ad sp show -o json --id $values.provisionerAppId.$Environment
        if ($preExistingProvisionerApp) {
            Write-Host "Found pre-existing provisioner application '$($values.provisionerAppId.$Environment)'"
            return
        } else {
            Write-Host "Failed to find provisioner application '$($values.provisionerAppId.$Environment)'"
        }
    }

    $spName = "stress-provisioner-$($params.groupSuffix)"
    Write-Host "Creating new provisioner application '$spName'."

    $sp = RunOrExitOnFailure az ad sp create-for-rbac `
        -o json `
        -n $spName `
        --role Owner `
        --scopes "/subscriptions/$($params.subscriptionId)"
    $spInfo = $sp | ConvertFrom-Json
    # Force check to see if the service principal was succesfully created and propagated
    $oid = (RunOrExitOnFailure az ad sp show -o json --id $spInfo.appId | ConvertFrom-Json).id

    $credentials = @{
        AZURE_CLIENT_ID = $spInfo.appId
        AZURE_CLIENT_SECRET = $spInfo.password
        AZURE_CLIENT_OID = $oid
        AZURE_TENANT_ID = $spInfo.tenant
        AZURE_SUBSCRIPTION_ID = $params.subscriptionId
        STRESS_CLUSTER_RESOURCE_GROUP = $STRESS_CLUSTER_RESOURCE_GROUP
    }

    # Powershell on windows does not play nicely passing strings with newlines as secret values
    # to the Azure CLI keyvault command, so use a file here instead.
    $envFile = Join-Path ([System.IO.Path]::GetTempPath()) "/static.env"
    $dotenv = $credentials.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)`n" }
    (-join $dotenv) | Out-File $envFile
    Run az keyvault secret set --vault-name $params.staticTestKeyvaultName --file $envFile -n $STATIC_TEST_DOTENV_NAME
    if (Test-Path $envFile) {
        Remove-Item -Force $envFile
    }
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }

    SetEnvProvisioner $spInfo
}

function GetEnvValues()
{
    $values = ConvertFrom-Yaml -Ordered (Get-Content -Raw $VALUES_FILE)
    return $values
}

function SetEnvValues([object]$values)
{
    $values | ConvertTo-Yaml | Out-File $VALUES_FILE
    Write-Warning "Update https://aka.ms/azsdk/stress/dashboard link page with new dashboard link: $($outputs.DASHBOARD_LINK.value)"
    Write-Warning "$VALUES_FILE has been updated and must be checked in."
}

function SetEnvProvisioner([object]$provisioner)
{
    $values = GetEnvValues
    $values.provisionerAppId.$Environment = $provisioner.appId
    SetEnvValues $values
}

function SetEnvOutputs([hashtable]$params)
{
    $outputs = (az deployment sub show `
        -o json `
        -n "stress-deploy-$($params.groupSuffix)" `
        --query properties.outputs `
        --subscription $params.subscriptionId
    ) | ConvertFrom-Json

    $values = GetEnvValues

    $values.appInsightsKeySecretName.$Environment = $outputs.APPINSIGHTS_KEY_SECRET_NAME.value
    $values.appInsightsConnectionStringSecretName.$Environment = $outputs.APPINSIGHTS_CONNECTION_STRING_SECRET_NAME.value
    $values.debugStorageKeySecretName.$Environment = $outputs.DEBUG_STORAGE_KEY_SECRET_NAME.value
    $values.debugStorageAccountSecretName.$Environment = $outputs.DEBUG_STORAGE_ACCOUNT_SECRET_NAME.value
    $values.debugFileShareName.$Environment = $outputs.DEBUG_FILESHARE_NAME.value
    $values.staticTestSecretsKeyvaultName.$Environment = $outputs.STATIC_TEST_SECRETS_KEYVAULT.value
    $values.clusterTestSecretsKeyvaultName.$Environment = $outputs.CLUSTER_TEST_SECRETS_KEYVAULT.value
    $values.secretProviderIdentity.$Environment = $outputs.SECRET_PROVIDER_CLIENT_ID.value
    $values.subscription.$Environment = $STATIC_TEST_DOTENV_NAME
    $values.tenantId.$Environment = $outputs.TENANT_ID.value

    SetEnvValues $values
}

function DeployClusterResources([hashtable]$params)
{
    Write-Host "Deploying stress cluster resources"
    RunSupportingWhatIfFlag "--what-if" az deployment sub create `
        -o json `
        --subscription $params.subscriptionId `
        -n "stress-deploy-$($params.groupSuffix)" `
        -l $params.clusterLocation `
        -f $PSScriptRoot/azure/main.bicep `
        --parameters $PSScriptRoot/azure/parameters/$Environment.json `
        --parameters groupName=$STRESS_CLUSTER_RESOURCE_GROUP `
        --parameters updateNodes=$UpdateNodes

    SetEnvOutputs $params

    Write-Host "Importing cluster credentials"
    RunSupportingWhatIfFlag "--only-show-errors" az aks get-credentials `
        -n $params.clusterName `
        -g $STRESS_CLUSTER_RESOURCE_GROUP `
        --overwrite `
        --subscription $params.subscriptionId
}

function DeployHelmResources()
{
    Write-Host "Installing stress infrastructure charts"
    $LastWhatIfPreference = $WhatIfPreference

    $WhatIfPreference = $false
    $chartRepoName = 'stress-test-charts'
    if ($LocalAddonsPath) {
        $absAddonsPath = Resolve-Path $LocalAddonsPath
        if (!(helm plugin list | Select-String 'file')) {
            RunOrExitOnFailure helm plugin add (Join-Path $absAddonsPath file-plugin)
        }
        RunOrExitOnFailure helm repo add --force-update $chartRepoName file://$absAddonsPath
    } else {
        RunOrExitOnFailure helm repo add --force-update $chartRepoName https://stresstestcharts.blob.core.windows.net/helm/
    }
    RunOrExitOnFailure helm repo add chaos-mesh https://charts.chaos-mesh.org
    RunOrExitOnFailure helm dependency update $PSScriptRoot/kubernetes/stress-infrastructure

    $WhatIfPreference = $LastWhatIfPreference
    Run kubectl create namespace $Namespace --dry-run=client -o yaml | kubectl apply -f -

    # Skip installing chaos mesh charts in development mode (i.e. when testing stress watcher only).
    $deployChaosMesh = "$(!$Development)".ToLower()

    RunSupportingWhatIfFlag "--dry-run" helm upgrade --install stress-infra `
        -n $Namespace `
        --set stress-test-addons.env=$Environment `
        --set deploy.chaosmesh=$deployChaosMesh `
        $PSScriptRoot/kubernetes/stress-infrastructure
}

# Steps to install preview features that may not be available in the bicep deployment
function RegisterAKSFeatures([string]$group, [string]$cluster) {
    if ($UpdateNodes) {
        return
    }
    RunOrExitOnFailure az extension add --name aks-preview
    RunOrExitOnFailure az extension update --name aks-preview
    RunOrExitOnFailure az feature register --namespace Microsoft.ContainerService --name EnableImageCleanerPreview
    $i = 0
    do {
        sleep $i
        $feature = RunOrExitOnFailure az feature show --namespace Microsoft.ContainerService --name EnableImageCleanerPreview -o json
        $feature = $feature | ConvertFrom-Json
        Write-Host "Waiting for 'EnableImageCleanerPreview' feature to register. This may take several minutes."
        $i = 30
    } while ($feature.properties.state -eq "Registering")
    RunOrExitOnFailure az provider register --namespace Microsoft.ContainerService
    RunOrExitOnFailure az aks update -g $group -n $cluster --enable-image-cleaner --image-cleaner-interval-hours 24
}

function LoadEnvParams()
{
    try {
        $params = (Get-Content $PSScriptRoot/azure/parameters/$Environment.json | ConvertFrom-Json -AsHashtable).parameters
    } catch {
        Write-Error "Error loading parameters file at $PSScriptRoot/azure/parameters/$Environment.json. Check that any lines marked '// add me' are filled in and that the file exists."
        exit 1
    }

    $paramHash = @{}
    foreach ($p in $params.GetEnumerator()) {
        $paramHash[$p.Key] = $p.Value.value
    }

    return $paramHash
}

function main()
{
    # Force a reset of $LASTEXITCODE 0 so that when running in -WhatIf mode
    # we don't inadvertently check a $LASTEXITCODE value from before the script invocation
    if ($WhatIfPreference) {
        helm -h > $null
    }

    if ($Environment -NotIn "prod", "pg" -and !$LocalAddonsPath) {
        throw "When using a custom environment you must set -LocalAddonsPath to provide the stress-infrastructure release with environment values"
    }

    if ($PSCmdlet.ParameterSetName -eq 'Provisioner') {
        az login `
            --service-principal `
            --username $ProvisionerApplicationId `
            --password $ProvisionerApplicationSecret`
            --tenant $TenantId
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
    }

    if (!$Development) {
        $params = LoadEnvParams
        $STRESS_CLUSTER_RESOURCE_GROUP = "rg-stress-cluster-$($params.groupSuffix)"
        DeployStaticResources $params
        DeployClusterResources $params
        RegisterAKSFeatures $STRESS_CLUSTER_RESOURCE_GROUP $params.clusterName
    }
    DeployHelmResources
}

main
