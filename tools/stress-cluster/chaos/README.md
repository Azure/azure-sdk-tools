azure-sdk-chaos is an environment and set of tools for performing long-running tests with the Azure SDK that involve OS and system-level faults.

The chaos environment is an AKS cluster (Azure Kubernetes Service) with several open-source chaos tools installed on it, currently managed by @benbp.

# Table of Contents

  * [Installation](#installation)
  * [Access](#access)
  * [Quick Testing with no Dependencies](#quick-testing-with-no-dependencies)
  * [Creating a Stress Test](#creating-a-stress-test)
     * [Layout](#layout)
     * [Stress Test Metadata](#stress-test-metadata)
     * [Stress Test Secrets](#stress-test-secrets)
     * [Stress Test File Share](#stress-test-file-share)
     * [Stress Test Azure Resources](#stress-test-azure-resources)
     * [Helm Chart File](#helm-chart-file)
        * [Customize Docker Build](#customize-docker-build)
     * [Manifest Special Fields](#manifest-special-fields)
     * [Job Manifest](#job-manifest)
     * [Chaos Manifest](#chaos-manifest)
     * [Scenarios and values.yaml](#scenarios-and-valuesyaml)
     * [Node Size Requirements](#node-size-requirements)
  * [Deploying a Stress Test](#deploying-a-stress-test)
  * [Configuring faults](#configuring-faults)
     * [Faults via Dashboard](#faults-via-dashboard)
     * [Faults via Config](#faults-via-config)
     * [Debugging chaos resources and events](#debugging-chaos-resources-and-events)
     * [Running the example test with a network fault](#running-the-example-test-with-a-network-fault)


Technologies used:

1. [Azure AKS](https://docs.microsoft.com/en-us/azure/aks/)
1. [Kubernetes](https://kubernetes.io/)
1. [Chaos Mesh](https://chaos-mesh.org/)

## Installation

You will need the following tools to create and run tests:

1. [Docker](https://docs.docker.com/get-docker/)
1. [Kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl)
1. [Helm](https://helm.sh/docs/intro/install/)
1. [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
1. [Powershell Core](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.1#ubuntu-2004) (if using Linux)

## Access

To access the cluster, run the following. These commands are unnecessary for stress test deployment but can be useful
for verifying permissions and directly interacting with containers via the kubernetes command line tool `kubectl`. For
running the build and deployment script, see [Deploying a Stress Test](#deploying-a-stress-test).

```bash
# Authenticate to Azure
az login

# Download the kubeconfig for the cluster (creates a 'context' named 'stress-test')
az aks get-credentials --subscription "Azure SDK Developer Playground" -g rg-stress-cluster-test -n stress-test
```

You should now be able to access the cluster. To verify, you should see a list of namespaces when running the command:

```
kubectl get namespaces
```

## Quick Testing with no Dependencies

This section details how to deploy a simple job, without any dependencies on the cluster (e.g. azure credentials, app insights keys) or stress test scripts. It is used to illustrate how kubernetes and the tools work only. Stress test development should be done using the [deploy script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1).

To get started, you will need to create a container image containing your long-running test, and a manifest to execute that image as a [kubernetes job](https://kubernetes.io/docs/concepts/workloads/controllers/job/).

The Dockerfile for your image should contain your test code/artifacts. See [docs on how to create a Dockerfile](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)

To create any resources in the cluster, you will need to create a namespace for them to live in:

```bash
# For simplicity of tracking use your user alias as the name of your namespace.
kubectl create namespace <your alias>
```

You will then need to build and push your container image to an Azure Container Registry the cluster has access to.

Get the default container registry for the stress testing Kubernetes cluster:

```bash
az acr list -g rg-stress-cluster-test --subscription "Azure SDK Developer Playground" --query "[0].loginServer"
# Outputs: <registry server host name, ex: 'myregistry.azurecr.io'>
```

Login to the azure container registry. The below command will add a token to the registy to the local docker config. This must be refreshed daily.

```
az acr login -n <registry name>
```

Build and push development image to stress test cluster registry

```bash
docker build . -t "<registry server host name from above>/<your alias>/<test job image name>:<version>"
docker push "<registry server host name from above>/<your username>/<test job image name>:<version>"
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
        imagePullPolicy: Always
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

**To quickly bootstrap a stress test package configuration, see the [stress test generator](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/kubernetes/generator/README.md)**

### Layout

The basic layout for a stress test is the following (see [`examples/stress_deployment_example`](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/chaos/examples/stress-deployment-example) for an example):

```
<stress test root directory>
    Dockerfile                          # A Dockerfile for building the stress test image. Custom dockerfile names are also supported.
    stress-test-resources.[bicep|json]  # An Azure Bicep or ARM template for deploying stress test azure resources.

    Chart.yaml                          # A YAML file containing information about the helm chart and its dependencies
    templates/                          # A directory of helm templates that will generate Kubernetes manifest files.
                                        # Most commonly this will contain a Job/Pod spec snippet and a chaos mesh manifest.

    # Optional files/directories

    values.yaml                  # Any default helm template values for this chart, e.g. a `scenarios` list
    <misc scripts/configs>       # Any language specific files for building/running/deploying the test
    <source dirs, e.g. src/>     # Directories containing code for stress tests
    <bicep modules>              # Any additional bicep module files/directories referenced by stress-test-resources.bicep
```

### Stress Test Metadata

A stress test package should follow a few conventions that are used by the automation to auto-discover behavior.

Fields in `Chart.yaml`
1. The `name` field will get used as the helm release name. To deploy instances of the same stress test release in parallel, update this field.
1. The `annotations.stressTest` field must be set to true for the script to discover the test.
1. The `annotations.namespace` field must be set, and governs which kubernetes namespace the stress test package will be
   installed into as a helm release when deployed by CI. Locally, this defaults to your username instead.
1. Extra fields in `annotations` can be set arbitrarily, and used via the `-Filters` argument to the [stress test deploy
   script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1).

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

### Stress Test File Share

Stress tests are encouraged to use app insights logs and metrics as much as possible for diagnostics. However there
are some times where larger files (such as verbose logs, heap dumps, packet captures, etc.) need to be persisted for
a duration longer than the lifespan of the test itself.

All stress tests have an azure file share automatically mounted into the container by default. The path to this share
is available via the environment variable `$DEBUG_SHARE` and is global to all tests in the cluster. 
The `$DEBUG_SHARE` path includes the namespace and pod name of the test in order to avoid path overlaps with other
tests. The `$DEBUG_SHARE_ROOT` path is also available, which points to the root of the file share, but this directory
should only be used in special circumstances and with caution.

NOTE: The share directory path MUST be created by the test before using it.

After writing debug files to the share, the files can be viewed by navigating to the [file share
portal](https://aka.ms/azsdk/stress/share),
selecting the `namespace/<pod name>` directory, and clicking the download link for any files in that directory.

See
[stress-debug-share-example](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/chaos/examples/stress-debug-share-example)
for example usage.

### Stress Test Azure Resources

Stress test resources can either be defined as azure bicep files, or an ARM template directly named
`stress-test-resources.[json|bicep]`. If using bicep, the [stress test deploy
script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1)
will compile an ARM template named `stress-test-resources.json` from the bicep file.
The stress test cluster and config boilerplate will handle running ARM deployments in an init container before
stress test container startup.

The bicep/ARM file should output at least the resource group name, which will be injected into the stress test env file.

```
// Dummy parameter to handle defaults the script passes in
param testApplicationOid string = ''

resource config 'Microsoft.AppConfiguration/configurationStores@2020-07-01-preview' = {
  name: 'config-${resourceGroup().name}'
  location: resourceGroup().location
  sku: {
    name: 'Standard'
  }
}

output RESOURCE_GROUP string = resourceGroup().name
output AZURE_CLIENT_OID string = testApplicationOid
```

### Helm Chart File

The `<chart root>/Chart.yaml` file should look something like below. It must include the `stress-test-addons` dependency and the included annotations:

```
apiVersion: v2
name: <stress test name>
description: <description>
version: 0.1.0
appVersion: v0.1
annotations:
  stressTest: 'true'  # enable auto-discovery of this test via `find-all-stress-packages.ps1`
  namespace: <your stress test namespace, e.g. python>
  dockerbuilddir: <OPTIONAL: custom docker build directory when dependencies are located in a parent directory>
  dockerfile: <OPTIONAL: custom dockerfile path when file is not named Dockerfile>
  <optional key/value annotations for filtering>

dependencies:
- name: stress-test-addons
  version: 0.1.16
  repository: https://stresstestcharts.blob.core.windows.net/helm/
```

The `stress-test-addons` dependency is a [helm library chart](https://helm.sh/docs/topics/library_charts/), which
pre-defines a lot of the kubernetes config boilerplate needed to configure stress tests correctly.

#### Customize Docker Build

To customize the docker build behavior, update the following fields in `Chart.yaml`:

- `annotations.dockerbuilddir` - docker build can only reference files within its build directory context. To run the docker build from a higher level context, e.g. to include file dependencies in other locations, set this value.
- `annotations.dockerfile` - If a stress test directory has multiple dockerfiles that need to be used for different purposes, you can customize which one to build with this field.

### Manifest Special Fields

For kubernetes manifests in the stress test helm chart `templates` directory that are wrapped by any of the
`stress-test-addons` (see [examples](#job-manifest)[below](#chaos-manifest)) templates, several special helper fields
are made available in the template context.

- `{{ .Values.image }}`
  - The docker image published by the stress test deploy script
- `{{ .Stress.Scenario }}`
  - If using [Scenarios](#scenarios-and-valuesyaml), this value maps to the individual scenario for which a
    template is being generated.
- `{{ .Stress.ResourceGroupName }}`
  - If deploying live resources for a test job, the name of the resource group.
  - This can also be useful for pairing up template values with resource names. The resource group name will be generated based
    on the deployment and scenario values, and can also be referenced for naming resources in the bicep/ARM template via `resourceGroup().name`.

### Job Manifest

The [Job](https://kubernetes.io/docs/concepts/workloads/controllers/job/) manifest should be a simple config 
that runs your stress test container with a startup command. There are a few [helm template include](https://helm.sh/docs/howto/charts_tips_and_tricks/)
functions that pull in config boilerplate from the `stress-test-addons` dependency in order to deploy
azure resources on startup and inject environment secrets.

Some required Job manifest fields like `Kind`, `metadata`, etc. are omitted for simplicity as they get added
in by the `stress-test-addons.job-template` include.  These can be overridden in the top level file if needed.

In the example below, the "deploy-job-template.from-pod" template is used, which will inject field values for a
[pod spec](https://kubernetes.io/docs/concepts/workloads/pods/#pod-templates) into a job manifest.

```
{{- include "stress-test-addons.deploy-job-template.from-pod" (list . "stress.deploy-example") -}}
{{- define "stress.deploy-example" -}}
metadata:
  labels:
    testName: "deploy-example"
spec:
  containers:
    - name: deployment-example
      image: mcr.microsoft.com/azure-cli
      imagePullPolicy: Always
      command: ['bash', '-c']
      args:
        - |
            source $ENV_FILE &&
            az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET --tenant $AZURE_TENANT_ID &&
            az account set -s $AZURE_SUBSCRIPTION_ID &&
            az group show -g $RESOURCE_GROUP -o json
      {{- include "stress-test-addons.container-env" . | nindent 6 }}
{{- end -}}
```

### Chaos Manifest

The most common way of configuring stress against test jobs is via [Chaos Mesh](https://chaos-mesh.org/).
Any chaos experiment manifests can be placed in the `<stress test directory>/templates/`.
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

In the example below, the "stress-test-addons.chaos-wrapper.tpl" template is used, which will contain 
[special fields](#manifest-special-fields) useful for correlating chaos targets with individual test jobs and pods.

```
{{- include "stress-test-addons.chaos-wrapper.tpl" (list . "stress.azservicebus-network") -}}
{{- define "stress.azservicebus-network" -}}
apiVersion: chaos-mesh.org/v1alpha1
kind: NetworkChaos
spec:
  action: loss
  direction: to
  externalTargets:
    # Maps to the service bus resource cname, provided the resource group name, provided
    # the service bus namespace uses the resource group name as its name in the bicep template
    - "{{ .Stress.ResourceGroupName }}.servicebus.windows.net"
  mode: one
  selector:
    labelSelectors:
      # Maps to the test pod, provided it also sets a testInstance label of {{ .Stress.ResourceGroupName }}
      testInstance: {{ .Stress.ResourceGroupName }}
      chaos: "true"
    namespaces:
      - {{ .Release.Namespace }}
  loss:
    loss: "100"
    correlation: "100"
{{- end -}}
```

For more detailed examples, see:

- [Chaos Experiments](https://chaos-mesh.org/docs/chaos_experiments/networkchaos_experiment) docs for all possible types
- `./examples/network_stress_example/templates/network_loss.yaml` for an example network loss manifest within a helm chart
- The [Faults via Dashboard section](#faults-via-dashboard) for generating the configs from the UI

### Scenarios and values.yaml

In order to run multiple tests but re-use the same job yaml, a special config key called `scenarios` can be used. Under
the hood, the stress test tools will duplicate the job yaml for each scenario. A common pattern is to represent each
test case with a file and/or argument passed to the stress program via the container command.

For example, given a stress test package with multiple tests represented as separate files:

```
values.yaml
templates/
src/
  scenarioLongRunning.js
  scenarioPeekMessages.js
  scenarioBatchReceive.js
...
```

The pod command in the job manifest could be configured to run a variable file name:

```
command: ["node", "app/{{ .Stress.Scenario }}.js"]
```

In order to accomplish this, add the scenarios list to `values.yaml`

```
scenarios:
  - scenarioLongRunning
  - scenarioPeekMessages
  - scenarioBatchReceive
```

The underlying `stress-test-addons` helm library will handle a scenarios list automatically, and deploy multiple instances of the stress test job, one for each scenario.

### Node Size Requirements

The stress test cluster is deployed with several node SKUs (see [agentPoolProfiles declaration and
variables](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/azure/cluster/cluster.bicep)), with tests defaulting to the SKU labeled 'default'.
By adding the `nodeSelector` field to the job spec, you can override which nodes the test container will
be provisioned to. For support adding a custom or dedicated node SKU, reach out to the EngSys team.

Available common SKUs in stress test clusters:

- 'default' - Standard\_D2\_v3
- 'highMem' - Standard\_D4ds\_v4

To deploy a stress test to a custom node (see also
[example](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/examples/network-stress-example/templates/testjob.yaml)):

```
spec:
  nodeSelector:
    sku: 'highMem'
  containers:
    < container spec ... >
```

To add a new temporary nodepool (for cluster admins/engsys), see example below:

```
az aks nodepool add \
    -g <cluster group> \
    --cluster-name <cluster name> \
    -n <nodepool name> \
    --enable-encryption-at-host \
    --enable-cluster-autoscaler \
    --node-count 1 \
    --min-count 1 \
    --max-count 3 \
    --node-vm-size <azure vm sku> \
    --labels "sku=<nodepool sku label>"
```

## Deploying a Stress Test

The stress test deployment is best run via the [stress test deploy
script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1).
This script handles: cluster and container registry access, building the stress test helm package, installing helm
package dependencies, and building and pushing docker images. The script must be run via powershell or powershell core.

If using bash or another linux terminal, a [powershell core](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.1#ubuntu-2004) shell can be invoked via `pwsh`.

The first invocation of the script must be run with the `-Login` flag to set up cluster and container registry access.

```
cd <stress test search directory>

<repo root>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1 `
    -Login `
    -PushImages
```

To re-deploy more quickly, the script can be run without `-Login` and/or without `-PushImages` (if no code changes were
made).

```
<repo root>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1
```

To run multiple instances of the same test in parallel, add a different namespace override 
for each test deployment. If not specified, it will default to the shell username when run locally.

```
<repo root>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1 `
    -Namespace my-test-instance-2 `
```

You can check the progress/status of your installation via:

```
helm list -n <stress test namespace>
```

To debug the kubernetes manifests installed by the stress test, run the following from the stress test directory:

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

# Get logs from the init-azure-deployer init container, if deploying resources. Omit `-c init-azure-deployer` to get main container
logs.
kubectl logs -n <stress test namespace> <stress test pod name> -c init-azure-deployer

# If empty, there may have been startup failures
kubectl describe pod -n <stress test namespace> <stress test pod name>
```

If deploying resources, once the `init-azure-deployer` init container is completed and the stress test pod is in a `Running` state,
you can quick check the local logs:

```
kubectl logs -n <stress test namespace> <stress test pod name>
```

## Configuring faults

Faults can be configured via kubernetes manifests or via the UI (which is a helper for building the manifests under the hood). For docs on the manifest schema, see [here](https://chaos-mesh.org/docs/user_guides/run_chaos_experiment).

### Faults via Dashboard

NOTE: The chaos mesh dashboard is just a helper for generating manifest under the hood. You can create and submit these directly as well. See the [docs](https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes/).

To configure faults via the UI, make sure you can access the chaos dashboard by running the below command, and navigating to `localhost:2333` in your browser.

```
kubectl port-forward -n stress-infra svc/chaos-dashboard 2333:2333
```

From the chaos dashboard, you can click `New Experiment` and choose your fault and parameters from there.

When defining tests, a target must be supplied, which tells the chaos service which pod it needs to trigger the faults against.
Under the `Scope` fields, for `Namespace Selectors` you should select the namespace your job lives in. Under `Label Selectors` you should be able to find
a label like `job-name: <your job name>` in the drop down.

### Faults via Config

See [Chaos Manifest](#chaos-manifest).

### Debugging chaos resources and events

There are a few ways to check on the status of your chaos resources, after your stress test pod(s) reach a `Running` state.

From the [test cluster dashboard](https://aka.ms/azsdk/stress/dashboard), select your stress test pods from the dropdown
and verify there are entries in the logs in the **Chaos Daemon Events** table.

On the stress cluster, you can view the status of your chaos resources. For example, to check on all the network chaos
resources you have deployed:

```
kubectl get networkchaos -n <your alias>
```

Pick the one relevant to your test and print the detailed view:

```
kubectl get networkchaos -n <your alias> <networkchaos resource name> -o yaml
```

The yaml output should show a success or failure:

**Example Success**

```
  status:
    experiment:
      duration: 10.000411955s
      endTime: "2021-12-09T01:20:57Z"
      phase: Waiting
      podRecords:
      - action: loss
        hostIP: 10.240.0.7
        message: This is a source pod.network traffic control action duration 10s
        name: stress-python-eventhubs-stress-test-1-m2hhh
        namespace: yuling
        podIP: 10.244.1.40
      startTime: "2021-12-09T01:20:47Z"
    scheduler:
      nextRecover: "2021-12-09T01:21:27Z"
      nextStart: "2021-12-09T01:21:17Z"
```

**Example Failure**

```
status:
  experiment:
    phase: Failed
  failedMessage: 'lookup some-bad-host.foobar.net;:
    no such host'
  scheduler: {}
```

For chaos resource types other than network chaos, you can also query these by their `kind`. To list those available:

```
⇉ ⇉ ⇉ kubectl api-resources | grep chaos-mesh.org
awschaos                                       chaos-mesh.org/v1alpha1                true         AwsChaos
dnschaos                                       chaos-mesh.org/v1alpha1                true         DNSChaos
httpchaos                                      chaos-mesh.org/v1alpha1                true         HTTPChaos
iochaos                                        chaos-mesh.org/v1alpha1                true         IoChaos
jvmchaos                                       chaos-mesh.org/v1alpha1                true         JVMChaos
kernelchaos                                    chaos-mesh.org/v1alpha1                true         KernelChaos
networkchaos                                   chaos-mesh.org/v1alpha1                true         NetworkChaos
podchaos                                       chaos-mesh.org/v1alpha1                true         PodChaos
podiochaos                                     chaos-mesh.org/v1alpha1                true         PodIoChaos
podnetworkchaos                                chaos-mesh.org/v1alpha1                true         PodNetworkChaos
stresschaos                                    chaos-mesh.org/v1alpha1                true         StressChaos
timechaos                                      chaos-mesh.org/v1alpha1                true         TimeChaos
```

### Running the example test with a network fault

Follow the below commands to execute a sample test.

```
cd ./examples/network_stress_example
pwsh ../../../../../eng/common/scripts/stress-testing/deploy-stress-tests.ps1 -Login -PushImages
```

Verify the pods in the job have booted and are running ok (with chaos network failures):

```
⇉ ⇉ ⇉ kubectl get pod -n <your alias>
NAME                               READY   STATUS    RESTARTS   AGE
network-example-0629200737-bk647   1/1     Running   0          89s

⇉ ⇉ ⇉ kubectl logs -n <your alias> network-example-0629200737-bk647 -f
Spider mode enabled. Check if remote file exists.
--2021-06-09 00:51:52--  http://www.bing.com/
Resolving www.bing.com (www.bing.com)... 204.79.197.200, 13.107.21.200, 2620:1ec:c11::200
Connecting to www.bing.com (www.bing.com)|204.79.197.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|13.107.21.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|2620:1ec:c11::200|:80... failed: Cannot assign requested address.
Giving up.
```
