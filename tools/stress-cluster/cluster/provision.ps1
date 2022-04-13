param (
    [string]$Environment = 'test',
    [string]$Namespace = 'stress-infra',
    [switch]$Development = $false
)

$STATIC_TEST_DOTENV_NAME="public"
$VALUES_FILE = "$PSScriptRoot/kubernetes/stress-test-addons/values.yaml"

function Run()
{
    Write-Host "`n==> $args`n" -ForegroundColor Green
    $command, $arguments = $args
    & $command $arguments
    if ($LASTEXITCODE) {
        Write-Error "Command '$args' failed with code: $LASTEXITCODE" -ErrorAction 'Continue'
    }
}

function RunOrExitOnFailure()
{
    Run @args
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}

function DeployStaticResources([hashtable]$params) {
    Write-Host "Deploying static resources"

    RunOrExitOnFailure az group create `
        -n $params.staticTestSecretsKeyvaultGroup `
        -l $params.clusterLocation `
        --subscription $params.subscriptionId
    $kv = Run az keyvault show `
        -n $params.staticTestSecretsKeyvaultName `
        -g $params.staticTestSecretsKeyvaultGroup `
        --subscription $params.subscriptionId
    if (!$kv) {
        RunOrExitOnFailure az keyvault create `
            -n $params.staticTestSecretsKeyvaultName `
            -g $params.staticTestSecretsKeyvaultGroup `
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
    $oid = (RunOrExitOnFailure az ad sp show -o json --id $spInfo.appId | ConvertFrom-Json).objectId

    $credentials = @{
        AZURE_CLIENT_ID = $spInfo.appId
        AZURE_CLIENT_SECRET = $spInfo.password
        AZURE_CLIENT_OID = $oid
        AZURE_TENANT_ID = $spInfo.tenant
        AZURE_SUBSCRIPTION_ID = $params.subscriptionId
    }

    # Powershell on windows does not play nicely passing strings with newlines as secret values
    # to the Azure CLI keyvault command, so use a file here instead.
    $envFile = Join-Path ([System.IO.Path]::GetTempPath()) "/static.env"
    $dotenv = $credentials.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)`n" }
    (-join $dotenv) | Out-File $envFile
    Run az keyvault secret set --vault-name $params.staticTestSecretsKeyvaultName --file $envFile -n $STATIC_TEST_DOTENV_NAME
    Remove-Item -Force $envFile
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }

    SetEnvProvisioner $spInfo
}

function GetEnvValues() {
    $values = ConvertFrom-Yaml -Ordered (Get-Content -Raw $VALUES_FILE)
    return $values
}

function SetEnvValues([object]$values) {
    $values | ConvertTo-Yaml | Out-File $VALUES_FILE
    Write-Warning "Update https://aka.ms/azsdk/stress/dashboard link page with new dashboard link: $($outputs.DASHBOARD_LINK.value)"
    Write-Warning "$VALUES_FILE has been updated and must be checked in."
}

function SetEnvProvisioner([object]$provisioner) {
    $values = GetEnvValues
    $values.provisionerAppId.$Environment = $provisioner.appId
    SetEnvValues $values
}

function SetEnvOutputs([hashtable]$params) {
    $outputs = (az deployment sub show `
        -o json `
        -n "stress-deploy-$($params.groupSuffix)" `
        --query properties.outputs `
        --subscription $params.subscriptionId
    ) | ConvertFrom-Json

    $values = GetEnvValues

    $values.appInsightsKeySecretName.$Environment = $outputs.APPINSIGHTS_KEY_SECRET_NAME.value
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

function DeployClusterResources([hashtable]$params) {
    Write-Host "Deploying stress cluster resources"
    RunOrExitOnFailure az deployment sub create `
        -o json `
        --subscription $params.subscriptionId `
        -n "stress-deploy-$($params.groupSuffix)" `
        -l $params.clusterLocation `
        -f $PSScriptRoot/azure/main.bicep `
        --parameters $PSScriptRoot/azure/parameters/$Environment.json

    SetEnvOutputs $params

    Write-Host "Importing cluster credentials"
    RunOrExitOnFailure az aks get-credentials `
        -n $params.clusterName `
        -g rg-stress-cluster-$($params.groupSuffix) `
        --overwrite `
        --subscription $params.subscriptionId
}

function DeployHelmResources() {
    Write-Host "Installing stress infrastructure charts"
    RunOrExitOnFailure helm repo add chaos-mesh https://charts.chaos-mesh.org
    RunOrExitOnFailure helm dependency update $PSScriptRoot/kubernetes/stress-infrastructure
    RunOrExitOnFailure kubectl create namespace $Namespace --dry-run=client -o yaml | kubectl apply -f -

    # Skip installing chaos mesh charts in development mode (i.e. when testing stress watcher only).
    $deployChaosMesh = "$(!$Development)".ToLower()

    RunOrExitOnFailure helm upgrade --install stress-infra `
        -n $Namespace `
        --set stress-test-addons.env=$Environment `
        --set deploy.chaosmesh=$deployChaosMesh `
        $PSScriptRoot/kubernetes/stress-infrastructure
}

function LoadEnvParams() {
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

function main() {
    # . (Join-Path $PSScriptRoot "../Helpers" PSModule-Helpers.ps1)
    # Install-ModuleIfNotInstalled "powershell-yaml" "0.4.1" | Import-Module

    if (!$Development) {
        $params = LoadEnvParams
        DeployStaticResources $params
        DeployClusterResources $params
    }
    DeployHelmResources
}

# Don't call functions when the script is being dot sourced
if ($MyInvocation.InvocationName -ne ".") {
    $ErrorActionPreference = 'Stop'
    main
}
