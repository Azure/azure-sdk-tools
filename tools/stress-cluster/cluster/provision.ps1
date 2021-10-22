param (
    [string]$env = 'test'
)

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

    $sp = RunOrExitOnFailure az ad sp create-for-rbac `
        -o json `
        -n "stress-provisioner-$env" `
        --role Contributor `
        --scopes "/subscriptions/$($params.subscriptionId)"
    $spInfo = $sp | ConvertFrom-Json
    $oid = (RunOrExitOnFailure az ad sp show -o json --id $spInfo.appId | ConvertFrom-Json).objectId

    $credentials = @{
        AZURE_CLIENT_ID = $spInfo.appId
        AZURE_CLIENT_SECRET = $spInfo.password
        AZURE_CLIENT_OID = $oid
        AZURE_TENANT_ID = $spInfo.tenant
        AZURE_SUBSCRIPTION_ID = $params.subscriptionId
    }

    $dotenv = $credentials.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
    $secret = $dotenv -join "`n"

    RunOrExitOnFailure az keyvault secret set --vault-name $params.staticTestSecretsKeyvaultName --value $secret -n public
}

function UpdateOutputs([hashtable]$params) {
    $outputs = (az deployment sub show `
        -o json `
        -n stress-deploy-$env `
        --query properties.outputs `
        --subscription $params.subscriptionId
    ) | ConvertFrom-Json

    $valuesFile = "$PSScriptRoot/kubernetes/stress-test-addons/values.yaml"
    $values = ConvertFrom-Yaml -Ordered (Get-Content -Raw $valuesFile)

    $values.appInsightsKeySecretName.$env = $outputs.APPINSIGHTS_KEY_SECRET_NAME.value
    $values.debugStorageKeySecretName.$env = $outputs.DEBUG_STORAGE_KEY_SECRET_NAME.value
    $values.debugStorageAccountSecretName.$env = $outputs.DEBUG_STORAGE_ACCOUNT_SECRET_NAME.value
    $values.debugFileShareName.$env = $outputs.DEBUG_FILESHARE_NAME.value
    $values.staticTestSecretsKeyvaultName.$env = $outputs.STATIC_TEST_SECRETS_KEYVAULT.value
    $values.clusterTestSecretsKeyvaultName.$env = $outputs.CLUSTER_TEST_SECRETS_KEYVAULT.value
    $values.secretProviderIdentity.$env = $outputs.SECRET_PROVIDER_CLIENT_ID.value
    $values.tenantId.$env = $outputs.TENANT_ID.value

    $values | ConvertTo-Yaml | Out-File $valuesFile

    Write-Host "$valuesFile has been updated and must be checked in."
}

function DeployClusterResources([hashtable]$params) {
    Write-Host "Deploying stress cluster resources"
    RunOrExitOnFailure az deployment sub create `
        -o json `
        --subscription $params.subscriptionId `
        -n stress-deploy-$env `
        -l $params.clusterLocation `
        -f $PSScriptRoot/azure/main.bicep `
        --parameters $PSScriptRoot/azure/parameters/$env.json

    UpdateOutputs $params

    Write-Host "Importing cluster credentials"
    RunOrExitOnFailure az aks get-credentials `
        -n $params.clusterName `
        -g rg-stress-cluster-$($params.groupSuffix) `
        --overwrite `
        --subscription $params.subscriptionId

    Write-Host "Installing stress infrastructure charts"
    RunOrExitOnFailure helm repo add chaos-mesh https://charts.chaos-mesh.org
    RunOrExitOnFailure helm dependency update $PSScriptRoot/kubernetes/stress-infrastructure
    RunOrExitOnFailure kubectl create namespace stress-infra --dry-run=client -o yaml | kubectl apply -f -
    RunOrExitOnFailure helm upgrade --install stress-infra `
        -n stress-infra `
        $PSScriptRoot/kubernetes/stress-infrastructure
}

function LoadEnvParams() {
    $params = (Get-Content $PSScriptRoot/azure/parameters/$env.json | ConvertFrom-Json -AsHashtable).parameters

    if (!$params) {
        Write-Error "Error loading parameters file at $PSScriptRoot/azure/parameters/$env.json"
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

    $params = LoadEnvParams

    DeployStaticResources $params
    DeployClusterResources $params
}

# Don't call functions when the script is being dot sourced
if ($MyInvocation.InvocationName -ne ".") {
    $ErrorActionPreference = 'Stop'
    main
}
