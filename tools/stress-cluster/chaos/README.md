azure-sdk-chaos is an environment and set of tools for performing long-running tests with the Azure SDK that involve OS and system-level faults.

The chaos environment is an AKS cluster (Azure Kubernetes Service) with several open-source chaos tools installed on it, currently managed by @benbp.

# Table of Contents

  * [Installation](#installation)
  * [Access](#access)
  * [Quick Testing with no Dependencies](#quick-testing-with-no-dependencies)
  * [Creating a Stress Test](#creating-a-stress-test)
     * [Layout](#layout)
     * [Stress Test Secrets](#stress-test-secrets)
     * [Stress Test Azure Resources](#stress-test-azure-resources)
     * [Helm Chart Dependencies](#helm-chart-dependencies)
     * [Job Manifest](#job-manifest)
     * [Chaos Manifest](#chaos-manifest)
  * [Deploying a Stress Test](#deploying-a-stress-test)
  * [Configuring faults](#configuring-faults)
     * [Faults via Dashboard](#faults-via-dashboard)
     * [Faults via Config](#faults-via-config)
  * [Running the example test with a network fault](#running-the-example-test-with-a-network-fault)
     * [Configure Faults via Dashboard](#configure-faults-via-dashboard)


Technologies used:

1. [Azure AKS](https://docs.microsoft.com/en-us/azure/aks/)
1. [Kubernetes](https://kubernetes.io/)
1. [Chaos Mesh](https://chaos-mesh.org/)

## Installation

You will need the following tools to create and run tests:

1. [Docker](https://docs.docker.com/get-docker/)
1. [Kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl)
1. [Helm](https://helm.sh/)
1. [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)

## Access

To access the cluster, run the following:

```
az login
# Download the kubeconfig for the cluster
az aks get-credentials --subscription 'Azure SDK Test Resources' -g rg-stress-test-cluster- -n stress-test
```

You should now be able to access the cluster. To verify, you should see a list of namespaces when running the command:

```
kubectl get namespaces
```

## Quick Testing with no Dependencies

This section details how to deploy a simple job, without any dependencies on the cluster (e.g. azure credentials, app insights keys).

To get started, you will need to create a container image containing your long-running test, and a manifest to execute that image as a [kubernetes job](https://kubernetes.io/docs/concepts/workloads/controllers/job/).

The Dockerfile for your image should contain your test code/artifacts. See [docs on how to create a Dockerfile](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)

To create any resources in the cluster, you will need to create a namespace for them to live in:

```
kubectl create namespace <your alias>
kubectl label namespace <namespace> owners=<your alias>
```

You will then need to build and push your container image to a registry the cluster has access to:

```
az acr login -n stresstestregistry
docker build . -t stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>
docker push stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>
```

To define a job that utilizes your test, create a file called testjob.yaml, including the below contents (with fields replaced):

```
apiVersion: batch/v1
kind: Job
metadata:
  name: <your job name>
  namespace: <your namespace name>
spec:
  template:
    spec:
      containers:
      - name: <container name, pick anything>
        image: <container image name>
        command: ["test entrypoint command/binary"]
        args: [<args string array for your test command>]
      restartPolicy: Never
  backoffLimit: 1
```

To submit your test job, run:

```
# Submit/re-submit the test
kubectl replace --force -f testjob.yaml
```

To view the status of your test:

```
kubectl get jobs -n <your namespace name>
```

If there are any errors (whether due to configuration or commands):

```
kubectl describe pods -n <your namespace name> -l job-name=<your job name>
```

To view the logs from your test:

```
# Append -f to tail the logs
kubectl logs -n <your namespace name> -l job-name=<your job name>
```

To delete your test:

```
kubectl delete -f testjob.yaml
```

## Creating a Stress Test

This section details how to create a formal stress test which creates azure resource deployments and publishes telemetry.

Stress tests are packaged as [helm charts](https://helm.sh/docs/topics/charts/) using helm, which is a "package manager" for Kubernetes manifests.
The usage of helm charts allows for two primary scenarios:

- Stress tests can easily take dependencies on core configuration and templates required to interact with the cluster
- Stress tests can easily be deployed and removed via the `helm` command line tool.

### Layout

The basic layout for a stress test is the following (see `examples/stress_deployment_example` for an example):

```
<stress test root directory>
    Dockerfile                   # A Dockerfile for building the stress test image
    test-resources.[bicep|json]  # An Azure Bicep or ARM template for deploying stress test azure resources.
    parameters.json              # An ARM template parameters file that will be used at runtime along with the ARM template

    Chart.yaml                   # A YAML file containing information about the helm chart and its dependencies
    templates/                   # A directory of helm templates that will generate Kubernetes manifest files.
                                 # Most commonly this will contain a Job/Pod spec snippet and a chaos mesh manifest.

    # Optional files/directories

    values.yaml                  # Any default helm template values for this chart
    <misc scripts/configs>       # Any language specific files for building/running/deploying the test
    <source directories>         # Directories containing code for stress tests
    <bicep modules>              # Any additional bicep module files/directories referenced by test-resources.bicep
```

### Stress Test Secrets

For ease of implementation regarding merging secrets from various Keyvault sources, secret values injected into the stress
test container can be found in a file at path `$ENV_FILE` (usually `/mnt/outputs/.env`). This file follows the "dotenv" file syntax (i.e. lines of <key>=<value>), and
can be [loaded](https://www.npmjs.com/package/dotenv) [via](https://pypi.org/project/python-dotenv/)
[various](https://mvnrepository.com/artifact/io.github.cdimascio/dotenv-java) [packages](https://www.nuget.org/packages/dotenv.net/).

Stress tests should publish telemetry and logs to Application Insights via the $APPINSIGHTS_INSTRUMENTATIONKEY environment variable
injected into the container.

The following environment variables are currently populated by default into the env file, in addition to any
[bicep template outputs](https://github.com/Azure/bicep/blob/main/docs/spec/outputs.md) specified.

```
AZURE_CLIENT_ID=<value>
AZURE_CLIENT_SECRET=<value>
AZURE_TENANT_ID=<value>
AZURE_SUBSCRIPTION_ID=<value>
APPINSIGHTS_INSTRUMENTATIONKEY=<value>

# Bicep template outputs inserted here as well, for example
RESOURCE_GROUP=<value>
```

### Stress Test Azure Resources

Stress test resources can either be defined as azure bicep files, or an ARM template directly, provided there is
a `chart/test-resources.json` file in place before running `helm install`.
The stress test cluster and config boilerplate will handle running ARM deployments in an init container before
stress test container startup.

If using Azure Bicep files, they should be declared at the subscription `targetScope`, as opposed to the default
resource group scope. Additionally, they should create a resource group for the test, along with tags marking deletion
for the group after the intended duration of the stress test.

The bicep file should output at least the resource group name, which will be injected into the stress test env file.

```
targetScope = 'subscription'

param groupName string
param location string
param now string = utcNow('u')

resource group 'Microsoft.Resources/resourceGroups@2020-10-01' = {
    name: 'rg-stress-${groupName}-${uniqueString(now)}'
    location: location
    tags: {
        DeleteAfter: dateTimeAdd(now, 'PT8H')
    }
}

output RESOURCE_GROUP string = group.name
```

See the [Job Manifest section](#job-manifest) for an example spec containing config template includes for resource auto-deployment.

### Helm Chart Dependencies

The `<chart root>/chart/Chart.yaml` file should look something like below. It must include the `stress-test-addons` dependency:

```
apiVersion: v2
name: <stress test name>
description: <description>
version: 0.1.0
appVersion: v0.1

dependencies:
- name: stress-test-addons
  version: 0.1.1
  repository: https://stresstestcharts.blob.core.windows.net/helm/
```

### Job Manifest

The [Job](https://kubernetes.io/docs/concepts/workloads/controllers/job/) manifest should be a simple config 
that runs your stress test container with a startup command. There are a few [helm template include](https://helm.sh/docs/howto/charts_tips_and_tricks/)
functions that pull in config boilerplate from the `stress-test-addons` dependency in order to deploy
azure resources on startup and inject environment secrets.

Some required Job manifest fields like `Kind`, `metadata`, etc. are omitted for simplicity as they get added
in by the `stress-test-addons.job-template` include.  These can be overridden in the top level file if needed.

```
{{- include "stress-test-addons.deploy-job-template" (list . "stress.deploy-example") -}}
{{- define "stress.deploy-example" -}}
spec:
  template:
    metadata:
      labels:
        testName: "deploy-example"
    spec:
      containers:
        - name: deployment-example
          image: mcr.microsoft.com/azure-cli
          {{- include "stress-test-addons.container-env" . | nindent 10 }}
          command: ['bash', '-c']
          args:
            - |
                source $ENV_FILE &&
                az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET --tenant $AZURE_TENANT_ID &&
                az account set -s $AZURE_SUBSCRIPTION_ID &&
                az group show -g $RESOURCE_GROUP -o json
{{- end -}}
```

### Chaos Manifest

The most common way of configuring stress against test jobs is via [Chaos Mesh](https://chaos-mesh.org/).

Any chaos experiment manifests can be placed in the `<stress test directory>/chart/templates/`.

Chaos experiments can be targeted against test jobs via namespace and label selectors.

Given a pod metadata like:

```
metadata:
  labels:
    testInstance: "mytestname-{{ .Release.Name }}-{{ .Release.Revision }}"
    testName: mytestname
    chaos: "true"
```

The chaos experiment can be configured to target that pod and its parent namespace:

```
selector:
  labelSelectors:
    testInstance: "mytestname-{{ .Release.Name }}-{{ .Release.Revision }}"
    chaos: "true"
  namespaces:
    - {{ .Release.Namespace }}
```

For more detailed examples, see:

- [Chaos Experiments](https://chaos-mesh.org/docs/chaos_experiments/networkchaos_experiment) docs for all possible types
- `./examples/network_stress_example/chart/templates/network_loss.yaml` for an example network loss manifest within a helm chart
- The [Faults via Dashboard section](#faults-via-dashboard) for generating the configs from the UI

## Deploying a Stress Test

To build and deploy the stress test, first log in to access the cluster resources if not already set up:

```
az login
# Log in to the container registry for Docker access
az acr login -n stresstestregistry
# Download the kubeconfig for the cluster
az aks get-credentials -g rg-stress-test-cluster- -n stress-test --subscription 'Azure SDK Test Resources'
```

Then register the helm repository (this only needs to be done once):

```
helm repo add stress-test-charts https://stresstestcharts.blob.core.windows.net/helm/
helm repo update
```

Then build/publish images and build ARM templates. Make sure the docker image matches what's referenced in the helm templates.

```
# Build and publish image
docker build . -t stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>
docker push stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>

# Compile ARM template (if using Bicep files)
az bicep build -f ./test-resources.bicep

# Install helm dependencies
helm dependency update
```

Then install the stress test into the cluster:

```
kubectl create namespace <your stress test namespace> 
kubectl label namespace <namespace> owners=<owner alias>
helm install <stress test name> .
```

To install into a different cluster (test, prod, or dev):

```
az aks get-credentials --subscription '<cluster subscription>' -g rg-stress-test-cluster-<cluster suffix> -n stress-test
helm install <stress test name> . --set stress-test-addons.env=<cluster suffix>
```

You can check the progress/status of your installation via:

```
helm list -n <stress test namespace>
```

To update/re-deploy the test with changes:

```
helm upgrade <stress test name> .
```

To debug the yaml built by `helm install`, run:

```
helm template <stress test name> .
```

To stop and remove the test:

```
helm uninstall <stress test name> -n <stress test namespace>
```

To check the status of the stress test job resources:

```
# List stress test pods
kubectl get pods -n <stress test namespace> -l release=<stress test name>
# Get logs from azure-deployer init container
kubectl logs -n <stress test namespace> <stress test pod name> -c azure-deployer
# If empty, there may have been startup failures
kubectl describe pod -n <stress test namespace> <stress test pod name>
```

Once the `azure-deployer` init container is completed and the stress test pod is in a `Running` state,
you can quick check the local logs:

```
kubectl logs -n <stress test namespace> <stress test pod name>
```

## Configuring faults

Faults can be configured via kubernetes manifests or via the UI (which is a helper for building the manifests under the hood). For docs on the manifest schema, see [here](https://chaos-mesh.org/docs/user_guides/run_chaos_experiment).

### Faults via Dashboard

To configure faults via the UI, make sure you can access the chaos dashboard by running the below command, and navigating to `localhost:2333` in your browser.

```
kubectl port-forward -n chaos-testing svc/chaos-dashboard 2333:2333
```

From the chaos dashboard, you can click `New Experiment` and choose your fault and parameters from there.

When defining tests, a target must be supplied, which tells the chaos service which pod it needs to trigger the faults against.
Under the `Scope` fields, for `Namespace Selectors` you should select the namespace your job lives in. Under `Label Selectors` you should be able to find
a label like `job-name: <your job name>` in the drop down.

### Faults via Config

See [Chaos Manifest](#chaos-manifest).

## Running the example test with a network fault

Follow the below commands to execute a sample test.

```
cd ./examples/network_stress_example
# This will build the docker images and helm chart dependencies
./build.sh
# This will log in to the cluster and container registry, publish the image and the chart
./deploy.sh <your alias>
```

Verify the pods in the job have booted and are running ok (with chaos network failures):

```
⇉ ⇉ ⇉ kubectl get pod -n <your alias>
NAME                               READY   STATUS    RESTARTS   AGE
network-example-0629200737-bk647   1/1     Running   0          89s

⇉ ⇉ ⇉ kubectl logs -n <YOUR NAMESPACE> network-example-0629200737-bk647 -f
Spider mode enabled. Check if remote file exists.
--2021-06-09 00:51:52--  http://www.bing.com/
Resolving www.bing.com (www.bing.com)... 204.79.197.200, 13.107.21.200, 2620:1ec:c11::200
Connecting to www.bing.com (www.bing.com)|204.79.197.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|13.107.21.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|2620:1ec:c11::200|:80... failed: Cannot assign requested address.
Giving up.
```

### Configure Faults via Dashboard

Navigate to the chaos dashboard at `localhost:2333`

NOTE: The chaos mesh dashbaord is just a helper for generating manifest under the hood. You can create and submit these directly as well. See the [docs](https://chaos-mesh.org/docs/chaos_experiments/networkchaos_experiment).

1. From the UI, click `New Experiment`
1. Select `Network Attack` and then select `LOSS`
1. In the `Loss` textbox, enter `100`
1. Scroll down to `Scope`. Enter your namespace in the `Namespace Selectors` field, and find a Label Selector that matches your test (e.g. `testInstance: network-example-<your alias>`).
1. Enter a name for experiment like `<YOUR NAME>-<chaos type>`.
1. Enable `Run Continuously`
1. Click through the multiple `Submit` buttons.
