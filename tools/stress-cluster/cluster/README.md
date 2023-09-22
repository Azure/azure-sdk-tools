Table of Contents
* [Layout](#layout)
* [Dependencies](#dependencies)
* [Deploying Cluster(s)](#deploying-clusters)
   * [Dev Cluster](#dev-cluster)
   * [Playground Cluster](#playground-cluster)
   * [Prod Cluster](#prod-cluster)
   * [Local Cluster](#local-cluster)
* [Deploying Stress Test Addons](#deploying-stress-test-addons)
* [Development](#development)
   * [Bicep templates](#bicep-templates)
   * [Helm templates](#helm-templates)


# Layout

This directory contains all configuration used for stress test cluster buildout (azure and kubernetes buildout), as well
as a set of common stress test config boilerplate (helm library).

The `./azure` directory contains [Azure Bicep](https://docs.microsoft.com/azure/azure-resource-manager/bicep/overview)
files for deploying Azure resources (mainly [AKS clusters](https://azure.microsoft.com/services/kubernetes-service/)
to support stress testing (for dev/playground and/or production).

Azure Bicep comes pre-installed with the Azure CLI, and is a DSL for generating ARM templates.

The `./kubernetes/stress-infrastructure` directory contains a helm chart for deploying the core services
that must be installed into any stress cluster: chaos-mesh (for chaos) and stress-watcher (for event handling like chaos
resource start and resource group cleanup).

The `./kubernetes/stress-test-addons` directory contains a [library chart](https://helm.sh/docs/topics/library_charts/)
for use by stress test packages. This common set of config boilerplate simplifies stress test authoring, and makes it
easier to make and roll out config changes to tests across repos by using helm chart dependency versioning.


# Dependencies

- [Powershell Core](https://docs.microsoft.com/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.1)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl)
- [helm](https://helm.sh)
- [kind](https://github.com/kubernetes-sigs/kind/releases) (if testing locally)
- [Docker](https://docs.docker.com/get-docker/) (if deploying/testing locally)


# Deploying Cluster(s)

The cluster-specific configurations can be found at `./azure/parameters/<environment>.json`.

Almost all stress test infrastructure is local to the cluster resource group, including storage accounts, keyvaults,
log workspaces and the AKS cluster. There is also a set of static resources, including a subscription service principal
and a keyvault containing the credential configuration. These are shared across clusters located in the same subscription
and are provisioned independently of the bicep templates.

Cluster buildout and deployment involves three main steps which are automated in `./provision.ps1`:

1. Provision static resources (service principal, role assignments, static keyvault).
1. Provision cluster resources (`main.bicep` entrypoint, standard ARM subscription deployment).
    - NOTE: if the nodepool configuration for the AKS cluster needs to be updated, it cannot be done
      alongside a deployment to the cluster itself. In order to update the nodepool configuration only, pass
      the `-UpdateNodes` parameter to the provision script.
1. Provision stress infrastructures resources into the Azure Kubernetes Service cluster via helm
   (`./kubernetes/stress-infrastructure` helm chart).


## Dev Cluster

First, update the `./azure/parameters/dev.json` parameters file with the values marked `// add me`, then run:

```
./provision.ps1 -env dev -LocalAddonsPath `pwd`/kubernetes/stress-test-addons
```

To deploy stress test packages to the dev environment
(e.g. the [examples](https://github.com/Azure/bicep/tree/main/docs/examples)), pass in `-Environment dev` (see below).
The provision script will update the `./kubernetes/stress-test-addons/values.yaml` file with all the relevant
resource values from the newly provisioned dev environment that are required by the stress test common configuration.

Avoid checking in the updated dev values, they are for local use only.

```
<tools repo>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1 -Environment dev
```

## Playground Cluster

The playground cluster is the main ad-hoc cluster made available to SDK developers and partners. Changes to this cluster
should be made carefully and announced in advance in order not to disrupt people's work.

```
./provision.ps1 -env pg
```

## Prod Cluster

The "prod" cluster is the main cluster used for auto-deployment of checked-in stress tests via the StressTestRelease pipeline.
Currently, new instances of all stress tests across the language repositories are deployed on a weekly cadence.
Changes to the prod cluster should ideally be made around the stress test deployment cycle so as to avoid disruption
of test metrics.

```
./provision.ps1 -env prod
```

## Local Cluster

For quick testing of various kubernetes configurations, it can be faster and cheaper to use a local cluster.
Not all components of stress testing work in local clusters, however. If testing these components is necessary, the
recommended action is to spin up a dev cluster.

NOTE: chaos-mesh may not work on all local deployments (e.g. Docker Desktop on Windows via WSL).
It may be easier to test services, manifests and containers locally with KIND, and test chaos
in an Azure AKS cluster (shared or personal).

```
# Ensure docker is running
kind create cluster
```

# Deploying Stress Test Addons
Steps for deploying the stress test addons helm chart:
1. Increment the version number in stress test addons' [Chart.yaml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/kubernetes/stress-test-addons/Chart.yaml) (e.g. 0.1.0 -> 0.1.1).
1. Run [deploy.ps1](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/kubernetes/stress-test-addons/deploy.ps1).
1. Update all the helm chart versions for stress-test-addons dependency references in `azure-sdk-tools/tools/stress-cluster/chaos/examples/**/Chart.yaml`.
1. Run azure-sdk-tools\eng\common\scripts\stress-testing\deploy-stress-tests.ps1 script in the [examples](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/chaos/examples) directory, this will update all the nested helm charts (the -SkipLogin parameter can be used to speed up the script or if interactive login isn't supported by the shell).
   1. Run `kubectl get pods -n examples -w` to monitor the status of each pod and look for Running/Completed and make sure there are no errors.
1. Update all the stress tests' Chart.yaml files across the other repos in the same manner.

# Development

## Bicep templates

Examples detailing the Azure Bicep DSL can be found [here](https://github.com/Azure/bicep/tree/main/docs/examples).

Bicep also has a [VSCode extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep).

To validate file changes/compilation:

```
az bicep build -f ./azure/main.bicep
```

## Helm templates

When making changes to `stress-test-addons`, it is easiest to validate them by building one of the [example projects
](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/chaos/examples).

First, update the `dependencies section of the example's `Chart.yaml` file to point to your local changes on disk:

```
dependencies:
- name: stress-test-addons
  version: <latest version on disk in stress-test-addons Chart.yaml>
  repository: https://stresstestcharts.blob.core.windows.net/helm/
  repository: file:///<path to azure-sdk-tools repo>/tools/stress-cluster/cluster/kubernetes/stress-test-addons
```

Then you can test out the template changes by running, in the example stress test package directory:

```
helm template testrelease .
```

If there are any issues, the helm command will print any errors. If there are no errors, the rendered yaml
may still be an invalid kubernetes manifest, so the example stress test should also be deployed to validate
the full set of changes:

```
<tools repo>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1
```

For more helm debugging info, see [here](https://helm.sh/docs/chart_template_guide/debugging/).
