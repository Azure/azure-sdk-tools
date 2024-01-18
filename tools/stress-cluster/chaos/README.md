azure-sdk-chaos is an environment and set of tools for performing long-running tests with the Azure SDK that involve OS and system-level faults.

The chaos environment is an AKS cluster (Azure Kubernetes Service) with several open-source chaos tools installed on it, currently managed by @benbp.

# Table of Contents

  * [Installation](#installation)
  * [Deploying a Stress Test](#deploying-a-stress-test)
     * [Locking a test to run for a minimum number of days](#locking-a-test-to-run-for-a-minimum-number-of-days)
  * [Creating a Stress Test](#creating-a-stress-test)
     * [Layout](#layout)
     * [Stress Test Metadata](#stress-test-metadata)
     * [Stress Test Secrets and Environment](#stress-test-secrets-and-environment)
     * [Stress Test File Share](#stress-test-file-share)
     * [Stress Test Azure Resources](#stress-test-azure-resources)
       * [Deploying to a Custom Subscription](#deploying-to-a-custom-subscription)
     * [Helm Chart File](#helm-chart-file)
        * [Customize Docker Build](#customize-docker-build)
     * [Manifest Special Fields](#manifest-special-fields)
     * [Job Manifest](#job-manifest)
        * [Run multiple pods in parallel within a test job](#run-multiple-pods-in-parallel-within-a-test-job)
        * [Built-In Labels](#built-in-labels)
     * [Chaos Manifest](#chaos-manifest)
     * [Scenarios and scenarios-matrix.yaml](#scenarios-and-scenarios-matrixyaml)
     * [Node Size Requirements](#node-size-requirements)
  * [Configuring faults](#configuring-faults)
     * [Faults via Dashboard](#faults-via-dashboard)
     * [Faults via Config](#faults-via-config)
     * [Debugging chaos resources and events](#debugging-chaos-resources-and-events)
     * [Running the example test with a network fault](#running-the-example-test-with-a-network-fault)


Technologies used:

1. [Azure AKS](https://docs.microsoft.com/azure/aks/)
1. [Kubernetes](https://kubernetes.io/)
1. [Chaos Mesh](https://chaos-mesh.org/)

## Installation

You will need the following tools to create and run tests:

1. [Docker](https://docs.docker.com/get-docker/)
1. [Kubectl](https://kubernetes.io/docs/tasks/tools/#kubectl)
1. [Helm](https://helm.sh/docs/intro/install/)
1. [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
1. [Powershell Core](https://docs.microsoft.com/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.1) (if using Linux)

## Deploying a Stress Test

The stress test deployment is best run via the [stress test deploy
script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1).
This script handles: cluster and container registry access, building the stress test helm package, installing helm
package dependencies, and building and pushing docker images. The script must be run via powershell or powershell core.

If using bash or another linux terminal, a [powershell core](https://docs.microsoft.com/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7.1) shell can be invoked via `pwsh`.

```
cd <stress test search directory>
<repo root>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1
```

To re-deploy more quickly, the script can be run with `-SkipLogin` and/or with `-SkipPushImages` (if no code changes were
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

To check the status of the stress test containers:

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

### Locking a test to run for a minimum number of days

Occasionally the Kubernetes cluster can cause disruptions to long running tests. This will show up as a test pod
disappearing in the cluster (though all logs and other telemetry will still be available in app insights). This can
happen when nodes are auto-upgraded or scaled down to reduce resource usage.

If a test must be run for a long time, it can be disruptive when a node reboot/shutdown happens. This can be prevented
by setting the `-LockDeletionForDays` parameter. When this parameter is set, the test pods will be deployed alongside a
[PodDisruptionBudget](https://kubernetes.io/docs/tasks/run-application/configure-pdb/) that prevents nodes hosting the
pods from being removed. After the set number of days, this pod disruption budget will be deleted and the test will be
interruptable again. The test will not automatically shut down after this time, but it will no longer be locked.

```
<repo root>/eng/common/scripts/stress-testing/deploy-stress-tests.ps1 -LockDeletionForDays 7
```

To see when a pod's deletion lock will expire:

```
kubectl get pod -n <namespace> <pod name> -o jsonpath='{.metadata.annotations.deletionLockExpiry}'
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
    scenarios-matrix.yaml               # A YAML file containing configuration and custom values for stress test(s)
    templates/                          # A directory of helm templates that will generate Kubernetes manifest files.
                                        # Most commonly this will contain a Job/Pod spec snippet and a chaos mesh manifest.

    # Optional files/directories

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

### Stress Test Secrets and Environment

For ease of implementation regarding merging secrets from various Keyvault sources, secret values injected into the stress
test container can be found in a file at path `$ENV_FILE` (usually `/mnt/outputs/.env`). This file follows the "dotenv" file syntax (i.e. lines of <key>=<value>), and
can be [loaded](https://www.npmjs.com/package/dotenv) [via](https://pypi.org/project/python-dotenv/)
[various](https://mvnrepository.com/artifact/io.github.cdimascio/dotenv-java) [packages](https://www.nuget.org/packages/dotenv.net/).

Stress tests should publish telemetry and logs to Application Insights via the `$APPINSIGHTS_CONNECTION_STRING` environment variable
injected into the container. An `$APPINSIGHTS_INSTRUMENTATIONKEY` environment variable is also made available for
backwards compatibility, but using the connection string is recommended as the app insights service is deprecating the
instrumentation key approach.

The following environment variables are currently populated by default into the env file, in addition to any
[bicep template outputs](https://docs.microsoft.com/azure/azure-resource-manager/bicep/outputs) specified.

```
AZURE_CLIENT_ID=<value>
AZURE_CLIENT_OID=<value>
AZURE_CLIENT_SECRET=<value>
AZURE_TENANT_ID=<value>
AZURE_SUBSCRIPTION_ID=<value>
APPINSIGHTS_CONNECTION_STRING=<value>
APPINSIGHTS_INSTRUMENTATIONKEY=<value>

# Bicep template outputs inserted here as well, for example
RESOURCE_GROUP=<value>
```

Additionally, several values are made available as environment variables via the `stress-test-addons.container-env` template (see [job manifest](#job-manifest)):

- `GIT_COMMIT` - Matches the git commit of the repository in which the stress test was deployed from. Useful for telemetry queries.
- `ENV_FILE` - Path to the env file that can be dot sourced to load deployment and other secrets.
- `SCENARIO_NAME` - The identifier for the specific test config instance from the scenario matrix.
- `POD_NAME` - The name of the host pod, useful for custom telemetry.
- `POD_NAMESPACE` - The kubernetes namespace the container is running in, useful for custom telemetry.
- `DEBUG_SHARE` - See [stress test file share](#stress-test-file-share)
- `DEBUG_SHARE_ROOT` - See [stress test file share](#stress-test-file-share)

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
portal](https://aka.ms/azsdk/stress/fileshare),
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
// Unique short string safe for naming resources like storage, service bus.
param BaseName string = ''

resource config 'Microsoft.AppConfiguration/configurationStores@2020-07-01-preview' = {
  name: 'stress-${BaseName}'
  location: resourceGroup().location
  sku: {
    name: 'Standard'
  }
}

output RESOURCE_GROUP string = resourceGroup().name
output APP_CONFIG_NAME string = config.name
```

#### Deploying to a Custom Subscription

By default, the stress test environment will load credentials targeting the subscription that the stress cluster is deployed to.
However it is possible to have tests deploy Azure resources to a custom subscription. This can be useful in cases where resources require ARM feature flags
that do not exist in the cluster subscription, or if azure resource deployments should be billed to a different team owning the custom subscription.

To set up a custom subscription:

1. Create a service principal with Contributor access to your subscription (or Owner if your bicep file needs to set RBAC policies).
2. Set a secret with your desired name in the static keyvault provisioned for the stress cluster. The keyvault name can be found in the [addons values config](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/kubernetes/stress-test-addons/values.yaml) for the desired environment under the key `staticTestSecretsKeyvaultName`.

The secret contents should look like:

```
AZURE_CLIENT_SECRET=<Service Principal password>
AZURE_CLIENT_ID=<Service Principal app id>
AZURE_CLIENT_OID=<Service Principal object id>
AZURE_TENANT_ID=<AAD tenant ID>
AZURE_SUBSCRIPTION_ID=<Subscription ID>
```

3. Update your scenarios-matrix.yaml file to set the `subscriptionConfig` field for the scenarios that should deploy to the custom subscription.
   The value should match the secret name in keyvault. See [scenarios-matrix.yaml](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/examples/stress-deployment-example/scenarios-matrix.yaml) or below for examples.

Override subscription for ALL scenarios
```
matrix:
  subscriptionConfig: <your subscription config secret name>
  scenarios:
    myScenario1:
      foo: bar1
    myScenario2:
      foo: bar2
```

Override subscription for individual scenarios
```
  scenarios:
    deploy-default:
      foo: bar
    deploy-custom:
      subscriptionConfig: <your subscription config secret name>
      foo: bar
```

As an example, for the above samples, the following command would set up the custom subscription for use in the Azure SDK Engineering System `pg` cluster:

```
az keyvault secret set --vault-name stress-secrets-pg -n <your subscription config secret name> -f <path to sub config>
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

dependencies:
- name: stress-test-addons
  version: 0.2.0
  repository: https://stresstestcharts.blob.core.windows.net/helm/
```

The `stress-test-addons` dependency is a [helm library chart](https://helm.sh/docs/topics/library_charts/), which
pre-defines a lot of the kubernetes config boilerplate needed to configure stress tests correctly.

#### Customize Docker Build

To customize the docker build behavior, update the following fields in [`scenarios-matrix.yaml`](#scenarios-and-scenarios-matrixyaml):

- `dockerbuilddir` - docker build can only reference files within its build directory context. To run the docker build from a higher level context, e.g. to include file dependencies in other locations, set this value.
- `dockerfile` - If a stress test directory has multiple dockerfiles that need to be used for different purposes or if the stress test directory does not have a file named Dockerfile, you can customize which one to build with this field.

### Manifest Special Fields

For kubernetes manifests in the stress test helm chart `templates` directory that are wrapped by any of the
`stress-test-addons` (see [examples](#job-manifest)[below](#chaos-manifest)) templates, several special helper fields
are made available in the template context.

- `{{ .Stress.imageTag }}`
  - The docker image published by the stress test deploy script.
  - The docker image is referenced from the [scenarios-matrix.yaml](#scenarios-and-scenarios-matrixyaml).
- `{{ .Stress.Scenario }}`
  - If using [Scenarios](#scenarios-and-scenarios-matrixyaml), this value maps to the individual scenario for which a
    template is being generated.
- `{{ .Stress.ResourceGroupName }}`
  - If deploying live resources for a test job, the name of the resource group.
- `{{ .Stress.BaseName }}`
  - Use this value to generate a random name, prefixes or suffixes for azure resources.
  - The value consists of random alpha characters and will always start with a lowercase letter for maximum compatibility.
  - This can be referenced for naming resources in the bicep/ARM template by adding `param BaseName string = ''` and passing the `BaseName` to resource names.
    See [example template](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/examples/stress-deployment-example/stress-test-resources.bicep).
  - Useful for pairing up template values with resource names, e.g. a DNS target to block for network chaos.

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
    # Add this label to keep test resources around after test completion until their DeleteAfter tag expiry
    # Skip.RemoveTestResources: "true"
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

#### Run multiple pods in parallel within a test job

In some cases it may be necessary to run multiple instances of the same process/container in parallel as part of a test,
for example an eventhub test that needs to run 3 consumers, each in their own container. This can be achieved by adding
a `parallel` field in the matrix config. The parallel feature leverages the
[job completion mode](https://kubernetes.io/docs/concepts/workloads/controllers/job/#completion-mode) feature. Test
commands in the container can read the `JOB_COMPLETION_INDEX` environment variable to make decisions. For example,
a messaging test that needs to run a single producer and multiple consumers can have logic that runs the producer when
`JOB_COMPLETION_INDEX` is 0, and a consumer when it is not 0.

See a full working example of parallel pods [here](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/examples/parallel-pod-example).

See the below example to enable parallel pods via the matrix config (`scenarios-matrix.yaml`):

```
# scenarios-matrix.yaml
matrix:
  scenarios:
    parallel-example-a:
      description: "Example for running multiple test containers in parallel"
      # Adding this field into a matrix entry determines
      # how many pods will run in parallel
      parallel: 3
    parallel-example-b:
      description: "Example for running multiple test containers in parallel"
      parallel: 2
    non-parallel-example:
      description: "This scenario is not run multiple pods in parallel"
```

NOTE: when multiple pods are run, each pod will invoke its own azure deployment init container. When many of these containers
are run, it can cause race conditions with the arm/bicep deployment. There is logic in the deploy container to 
run the full deployment in pod 0 only, and to wait on deployment completion for pods > 0. After the deployment completes,
pods > 0 start their bicep deployment, which ends up being a no-op. As a result, the main container of pod 0 will start
a little bit earlier than pods > 0.

#### Built-In Labels

- `chaos` - set this to "true" to enable chaos for your pod
- `Skip.RemoveTestResources` - set this to "true" to prevent resources from being deleted immediately after test completion
- `gitCommit` - this will be automatically set on pod and job labels based on the repository commit the stress test was deployed from. Useful for telemetry queries.

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
    - "{{ .Stress.BaseName }}.servicebus.windows.net"
  mode: one
  selector:
    labelSelectors:
      # Maps to the test pod, provided it also sets a testInstance label of {{ .Stress.BaseName }}
      testInstance: {{ .Stress.BaseName }}
      chaos: "true"
    namespaces:
      - {{ .Release.Namespace }}
  loss:
    loss: "100"
    correlation: "100"
{{- end -}}
```

For more detailed examples, see:

- [Chaos Experiments](https://chaos-mesh.org/docs/simulate-network-chaos-on-kubernetes/) docs for all possible types
- `./examples/network_stress_example/templates/network_loss.yaml` for an example network loss manifest within a helm chart
- The [Faults via Dashboard section](#faults-via-dashboard) for generating the configs from the UI

### Scenarios and scenarios-matrix.yaml

In order to run multiple tests with different custom values (e.g. docker image, image build directory, test target ...) but re-use the same job yaml, a special config matrix called `scenarios` can be used. 
Under the hood, the stress test tools will duplicate the job yaml for each scenario.

For example, given a stress test package with multiple tests represented as separate files each running on a different docker image:

```
templates/
src/
  scenarioLongRunning.js
  scenarioPeekMessages.js
  scenarioBatchReceive.js
Dockerfiles/
  DockerfileLR
  DockerFilePM
  DockerfileBR
...
```

The pod command in the job manifest could be configured to run a variable file name:

```
command: ["node", "app/{{ .Stress.testTarget }}.js"]
```

While the [stress test deploy script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1) translates and uploads the docker image with an image tag that could be referenced in the job manifest:

```
image:  {{ .Stress.imageTag }}
```

In order to accomplish this, add the configuration to `scenarios-matrix.yaml`

```
matrix:
  image:
    - Dockerfiles/DockerfileLR
    - Dockerfiles/DockerfilePM
    - Dockerfiles/DockerfileBR
  scenarios:
    LongRunning:
      testTarget: scenarioLongRunning
    PeekMessages:
      testTarget: scenarioPeekMessages
    BatchReceive:
      testTarget: scenarioBatchReceive
```

The [`deploy-stress-tests.ps1`](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/deploy-stress-tests.ps1) script will generate a generatedValues.yaml file which contains the scenarios matrix that lists out all the custom configuration for each test instance.
The generatedValues.yaml for the example above would look like this where image tag will depend on your stress test registry name, namespace, release name, repo base name, docker file name and deploy id:
```
scenarios:
- testTarget: scenarioLongRunning
  Scenario: DockerfilesDockerfileLR-LongRunning
  image: Dockerfiles/DockerfileLR
  imageTag: <...azurecr.io...>
- testTarget: scenarioPeekMessages
  Scenario: DockerfilesDockerfilePM-PeekMessages
  image: Dockerfiles/DockerfilePM
  imageTag: <...azurecr.io...>
- testTarget: scenarioBatchReceive
  Scenario: DockerfilesDockerfileBR-BatchReceive
  image: Dockerfiles/DockerfileBR
  imageTag: <...azurecr.io...>
```

To test the matrix generation locally, you can also run the [generate-scenario-matrix script](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/stress-testing/generate-scenario-matrix.ps1)
```
generate-scenario-matrix.ps1 -matrixFilePath <path-to>/scenarios-matrix.yaml -Selection "sparse"
```

Stress test owners can also reference the custom config values they put in the scenarios matrix as shown below:
```
{{ .Stress.<custom_config_key> }}
```

All of the custom configuration values will also be passed to the docker image as build-args during build stage.
Users can reference the values by first defining the build arg in the dockerfile (multiple args will require multiple lines), then referencing the args with a dollar sign and curly braces
```
ARG VERSION
FROM python:${VERSION}
```

A more detailed information on the logic behind the matrix generation can be found in the [README for job-matrix](https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/job-matrix/README.md).

The `stress-test-addons` helm library will handle a scenarios matrix automatically, and deploy multiple instances of the stress test job, one for each scenario with customized values.

### Node Size Requirements

The stress test cluster may be deployed with several node SKUs (see [agentPoolProfiles declaration and
variables](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/cluster/azure/cluster/cluster.bicep)), with tests defaulting to the SKU labeled 'default'.
By adding the `nodeSelector` field to the job spec, you can override which nodes the test container will
be provisioned to. For support adding a custom or dedicated node SKU, reach out to the EngSys team.

Available common SKUs in stress test clusters:

- 'default' - Standard\_D4ds\_v4

To deploy a stress test to a custom node (see also
[example](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/examples/network-stress-example/templates/testjob.yaml)):

```
spec:
  nodeSelector:
    sku: '<nodepool sku label>'
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

## Configuring faults

Faults can be configured via kubernetes manifests or via the UI (which is a helper for building the manifests under the hood). For docs on the manifest schema, see [here](https://chaos-mesh.org/docs/define-chaos-experiment-scope/).

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

From the [playground cluster dashboard](https://aka.ms/azsdk/stress/dashboard), select your stress test pods from the dropdown
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
pwsh ../../../../../eng/common/scripts/stress-testing/deploy-stress-tests.ps1
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
