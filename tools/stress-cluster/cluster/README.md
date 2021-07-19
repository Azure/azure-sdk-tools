This directory contains [Azure Bicep](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview)
files for deploying Azure resources (mainly [AKS clusters](https://azure.microsoft.com/en-us/services/kubernetes-service/)
to support stress testing (for dev/test and/or production).

Azure Bicep comes pre-installed with the Azure CLI, and is a DSL for generating ARM templates.

# Dependencies

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
    - If using app insights, install the az extension: `az extension add --name application-insights`
- [kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl) (if accessing clusters)
- [helm](https://helm.sh) (if installing stress infrastructure)
- [kind](https://github.com/kubernetes-sigs/kind/releases) (if testing locally)
- [Docker](https://docs.docker.com/get-docker/) (if deploying/testing locally)

# Cluster Deployment Quick Start

## Deploying a Dev Cluster

First, update the `./azure/parameters/dev.json` parameters file with the values marked `// add me`, then:

```
az deployment sub create -o json -n <your name> -l westus -f ./azure/main.bicep --parameters ./azure/parameters/dev.json

# wait until resource group and AKS cluster are deployed
az aks get-credentials stress-azuresdk -g rg-stress-test-cluster-<group suffix parameter>
```

## Deploying a Local Cluster

NOTE: Chaos-Mesh may not work on all local deployments (e.g. Docker Desktop on Windows via WSL).
It may be easier to test services, manifests and containers locally with KIND, and test chaos
in an Azure AKS cluster (shared or personal).

```
# Ensure docker is running
kind create cluster
```

## Deploying Stress Infrastructure into Cluster

```
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm dependency update ./kubernetes/stress-infrastructure
helm install stress-infra -n stress-infra --create-namespace ./kubernetes/stress-infrastructure
```


# Development

Examples detailing the Azure Bicep DSL can be found [here](https://github.com/Azure/bicep/tree/main/docs/examples).

Bicep also has a [VSCode extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep).

To validate file changes/compilation:

```
az bicep build -f ./azure/main.bicep
```

To deploy and access resources:

```
# Edit ./azure/parameters/dev.json, replacing // add me values
# Add -c to dry run changes with a chance to confirm
az deployment sub create -o json -n <your name> -l westus -f ./azure/main.bicep --parameters ./azure/parameters/dev.json

# Copy the relevant outputs from the deployment to ./kubernetes/environments/<environment yaml file>
# for deploying stress tests later on
az deployment sub show -o json -n <your name> --query properties.outputs

az aks list -g rg-stress-test-cluster-<group suffix parameter>
az aks get-credentials stress-test -g rg-stress-test-cluster-<group suffix parameter>

# Verify cluster access
kubectl get pods

# Install stress infrastructure components
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm dependency update ./kubernetes/stress-infrastructure
helm install stress-infra -n stress-infra --create-namespace ./kubernetes/stress-infrastructure
kubectl get pods --namespace stress-infra
```

To access the chaos-mesh dashboard, run the below command then navigate to `localhost:2333` in the browser:

```
kubectl port-forward -n stress-infra svc/chaos-dashboard 2333:2333
```

To remove AKS cluster stress testing resources:

```
helm uninstall stress-infra --namespace stress-infra
```

To remove Azure resources:

```
az group delete <resource group name>
```

# Building out the Main/Prod Testing Cluster

If not already done, enable the relevant preview features in the subscription and CLI:
- [AKS-AzureKeyVaultSecretsProvider](https://docs.microsoft.com/en-us/azure/aks/csi-secrets-store-driver#register-the-aks-azurekeyvaultsecretsprovider-preview-feature)

## Initializing static identities

The "official" stress testing clusters rely on a separately created keyvault containing secrets with subscription credentials for stress test resource deployments.
The identities/credentials in these keyvaults can't be created via ARM/Bicep, and should be managed independently of the individual environments.

To initialize these resources, if they don't exist:

```
az group create rg-StressTestSecrets
az keyvault create -n StressTestSecrets -g rg-StressTestSecrets
az ad sp create-for-rbac -n 'stress-test-provisioner' --role Contributor --scopes '/subscriptions/<subscription id>'
```

Create an env file with the service principal values created above:

```
AZURE_CLIENT_ID=<app id>
AZURE_CLIENT_SECRET=<password/secret>
AZURE_TENANT_ID=<tenant id>
```

Upload it to the static keyvault:

```
az keyvault secret set --vault-name StressTestSecrets  -f ./<env file> -n public
```

## Building Out Stress Test Cluster Resources

Various environment configurations are located in `./azure/parameters/<env>.json` to be configured when deploying.

Deploy the cluster and related components (app insights, container registry, keyvault, access policies, etc.)

```
az deployment sub create -o json -n stress-test-deploy -l westus -f ./azure/main.bicep --parameters ./azure/parameters/test.json
```

Gain access to the cluster and install the stress infrastructure components:

```
az aks get-credentials stress-test -g rg-stress-test-cluster-<group suffix>

helm repo add chaos-mesh https://charts.chaos-mesh.org
helm dependency update ./kubernetes/stress-infrastructure
helm install stress-infra -n stress-infra --create-namespace ./kubernetes/stress-infrastructure
```

Copy the deployment outputs to `./kubernetes/environments/<environment yaml file>` and check in the changes.

```
az deployment sub show -o json -n <your name> --query properties.outputs
```
