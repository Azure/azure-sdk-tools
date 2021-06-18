This directory contains [Azure Bicep](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview)
files for deploying Azure resources (mainly [AKS clusters](https://azure.microsoft.com/en-us/services/kubernetes-service/)
to support stress testing (for dev/test and/or production).

Azure Bicep comes pre-installed with the Azure CLI, and is a DSL for generating ARM templates.

# Dependencies

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
    - If using app insights, install the az extension: `az extension add --name application-insights`
- [kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl) (if accessing clusters)
- [kind](https://github.com/kubernetes-sigs/kind/releases) (if deploying/testing locally)
- [Docker](https://docs.docker.com/get-docker/) (if deploying/testing locally)
    - If using Windows Subsystem for Linux (WSL), prefer [Docker Desktop](https://docs.docker.com/docker-for-windows/wsl/)

# Cluster Deployment Quick Start

## Deploying a Personal Dev Cluster

First, update the `./parameters/dev.json` parameters file with the values marked `// add me`, then:

```
az deployment sub create -o json -n <your name> -l westus -f ./main.bicep --parameters ./parameters/dev.json

# wait until resource group and AKS cluster are deployed
az aks get-credentials stress-azuresdk -g rg-stress-test-cluster-<group suffix parameter>
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm install chaos-mesh chaos-mesh/chaos-mesh --namespace=chaos-testing --create-namespace --set dashboard.securityMode=false
```

## Deploying a Local Cluster

NOTE: Chaos-Mesh may not work on all local deployments (e.g. Docker Desktop on Windows via WSL).
It may be easier to test services, manifests and containers locally with KIND, and test chaos
in an Azure AKS cluster (shared or personal).

```
# Ensure docker is running
kind create cluster

# wait until KIND cluster is created, then if chaos mesh is supported:
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm install chaos-mesh chaos-mesh/chaos-mesh --namespace=chaos-testing \
  --set dashboard.securityMode=false \
  --set chaosDaemon.runtime=containerd \
  --set chaosDaemon.socketPath=/run/containerd/containerd.sock
```

# Development

Examples detailing the Azure Bicep DSL can be found [here](https://github.com/Azure/bicep/tree/main/docs/examples).

Bicep also has a [VSCode extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep).

To validate file changes/compilation:

```
az bicep build -f ./main.bicep
```

To deploy and access resources:

```
# Edit ./parameters/dev.json, replacing // add me values
# Add -c to dry run changes with a chance to confirm
az deployment sub create -o json -n <your name> -l westus -f ./main.bicep --parameters ./parameters/dev.json

az aks list -g rg-stress-test-cluster-<group suffix parameter>
az aks get-credentials stress-azuresdk -g rg-stress-test-cluster-<group suffix parameter>

# Verify cluster access
kubectl get pods

# Install chaos-mesh
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm install chaos-mesh chaos-mesh/chaos-mesh --namespace=chaos-testing --create-namespace --set dashboard.securityMode=false
kubectl get pods --namespace chaos-testing
```

To access the chaos-mesh dashboard, run the below command then navigate to `localhost:2333` in the browser:

```
kubectl port-forward -n chaos-testing svc/chaos-dashboard 2333:2333
```

To remove AKS cluster stress testing resources:

```
helm uninstall chaos-mesh --namespace chaos-testing
```

To remove Azure resources:

```
az group delete <resource group name>
```

# Building out the Main/Prod Testing Cluster

TODO: Additional steps for initializing resources and secrets for Application Insights.

If not already done, enable the relevant preview features in the subscription and CLI:
- [AKS-AzureKeyVaultSecretsProvider](https://docs.microsoft.com/en-us/azure/aks/csi-secrets-store-driver#register-the-aks-azurekeyvaultsecretsprovider-preview-feature)

```
az deployment sub create -o json -n stress-test-prod -l westus -f ./main.bicep --parameters ./parameters/prod.json
# wait until resource group and AKS cluster are deployed
az aks get-credentials stress-azuresdk -g rg-stress-test-cluster-prod
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm install chaos-mesh chaos-mesh/chaos-mesh --namespace=chaos-testing --create-namespace --set dashboard.securityMode=false
```
