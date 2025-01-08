[CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
param (
    [string]$Environment = 'dev',
    [string]$Namespace = 'stress-infra',
    [switch]$Development = $false,
    # If provisioning an existing cluster and updating nodes, it must be done exclusively
    [switch]$UpdateNodes = $false,

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
Install-ModuleIfNotInstalled -WhatIf:$false "powershell-yaml" "0.4.7" | Import-Module

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
    $values.clusterTestSecretsKeyvaultName.$Environment = $outputs.CLUSTER_TEST_SECRETS_KEYVAULT.value
    $values.secretProviderIdentity.$Environment = $outputs.SECRET_PROVIDER_CLIENT_ID.value
    $values.infraWorkloadAppServiceAccountName.$Environment = $outputs.INFRA_WORKLOAD_APP_SERVICE_ACCOUNT_NAME.value
    $values.infraWorkloadAppClientId.$Environment = $outputs.INFRA_WORKLOAD_APP_CLIENT_ID.value
    $values.infraWorkloadAppObjectId.$Environment = $outputs.INFRA_WORKLOAD_APP_OBJECT_ID.value
    $values.workloadAppIssuer.$Environment = $outputs.WORKLOAD_APP_ISSUER.value
    $values.clusterGroup.$Environment = $outputs.RESOURCE_GROUP.value
    $values.subscriptionId.$Environment = $outputs.SUBSCRIPTION_ID.value
    $values.tenantId.$Environment = $outputs.TENANT_ID.value

    # The workload apps can be found in the stress resource group as Managed Identity types
    $clientNames = ($outputs.WORKLOAD_APPS.value | ConvertFrom-Json -AsHashtable).name -join ','
    $values.workloadAppClientNamePool.$Environment = $clientNames

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

    if (!$WhatIfPreference) {
        SetEnvOutputs $params
    }

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
    #$deployChaosMesh = "$(!$Development)".ToLower()
    $deployChaosMesh = "false"

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

    if (!$Development) {
        $params = LoadEnvParams
        $STRESS_CLUSTER_RESOURCE_GROUP = "rg-stress-cluster-$($params.groupSuffix)"

        RunOrExitOnFailure az account set -s $params.subscriptionId

        DeployClusterResources $params
        RegisterAKSFeatures $STRESS_CLUSTER_RESOURCE_GROUP $params.clusterName
    }
    DeployHelmResources
}

main
