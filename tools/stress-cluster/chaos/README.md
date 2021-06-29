azure-sdk-chaos is an environment and set of tools for performing long-running tests with the Azure SDK that involve OS and system-level faults.

The chaos environment is an AKS cluster (Azure Kubernetes Service) with several open-source chaos tools installed on it, currently managed by @benbp.

### Table of Contents

* [Installation](#installation)
* [Access](#access)
* [Getting Started](#getting-started)
* [Configuring faults](#configuring-faults)
* [Running the example test with a network fault](#running-the-example-test-with-a-network-fault)

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
az account set --subscription 'Azure SDK Test Resources'
# Download the kubeconfig for the cluster
az aks get-credentials -g rg-stress-test-cluster- -n stress-test
```

You should now be able to access the cluster. To verify, you should see a list of namespaces when running the command:

```
kubectl get namespaces
```

To access the chaos dashboard, run this command:

```
kubectl port-forward -n chaos-testing svc/chaos-dashboard 2333:2333
```

Then navigate to `localhost:2333` in your browser. You will need to keep the above command running in order to maintain dashboard access.

## Getting Started

### Quick Testing with no Dependencies

This section details how to deploy a simple job, without any dependencies on the cluster (e.g. azure credentials, app insights keys).

To get started, you will need to create a container image containing your long-running test, and a manifest to execute that image as a [kubernetes job](https://kubernetes.io/docs/concepts/workloads/controllers/job/).

The Dockerfile for your image should contain your test code/artifacts. See [docs on how to create a Dockerfile](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)

To create any resources in the cluster, you will need to create a namespace for them to live in:

```
kubectl create namespace <your name>
```

You will then need to build and push your container image to a registry the cluster has access to:

```
az acr login -n azuresdkdev
docker build . -t azuresdkdev.azurecr.io/<your name>/<test job image name>:<version>
docker push azuresdkdev.azurecr.io/<your name>/<test job image name>:<version>
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

### Creating a Stress Test

This section details how to create a formal stress test which creates azure resource deployments and publishes telemetry.

Stress tests are packaged as [helm charts](https://helm.sh/docs/topics/charts/) using helm, which is a "package manager" for Kubernetes manifests.
The usage of helm charts allows for two primary scenarios:

- Stress tests can easily take dependencies on core configuration and templates required to interact with the cluster
- Stress tests can easily be deployed and removed via the `helm` command line tool.

#### Layout

The basic layout for a stress test is the following (see `examples/stress_deployment_example` for an example):

```
<chart root directory>
  Dockerfile              # A Dockerfile for building the stress test image
  <misc scripts/configs>  # Any language specific files for building/running/deploying the test
  <source directories>    # Directory/directories containing code for stress tests
  <test-resources.bicep>  # An Azure Bicep template for deploying stress test azure resources.

  chart/                  # Directory containing the helm chart for deploying into the stress test cluster
    Chart.yaml            # A YAML file containing information about the chart
    values.yaml           # Any default configuration values for this chart
    charts/               # A directory containing any charts upon which this chart depends.
    templates/            # A directory of templates that, when combined with values,
                          # will generate valid Kubernetes manifest files.
                          # Most commonly this will contain a Kubernetes Job manifest and a chaos mesh manifest.
```

#### Helm Chart Dependencies

The <chart root>/chart/Chart.yaml file should look something like below. It must include the `stress-test-addons` dependency:

```
apiVersion: v2
name: <stress test name>
description: <description>
version: 0.1.0
appVersion: v0.1

dependencies:
- name: stress-test-addons
  version: 0.1.0
  repository: file://../../../../cluster/kubernetes/stress-test-addons
```

#### Job Manifest

The [Job](https://kubernetes.io/docs/concepts/workloads/controllers/job/) manifest should be a simple config 
that runs your stress test container with a startup command. There are a few [helm template include](https://helm.sh/docs/howto/charts_tips_and_tricks/)
functions that pull in config boilerplate from the `stress-test-addons` dependency in order to deploy
azure resources on startup and inject environment secrets.

```
# Configmap template that adds the stress test ARM template
{{ include "stress-test-addons.deploy-configmap" . }}
---
apiVersion: batch/v1
kind: Job
metadata:
  name: <stress test name>-{{ .Release.Name }}
  namespace: {{ .Release.Namespace }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        testName: <stress test name>
        owners: <owner aliases>
        chaos: "true"
    spec:
      restartPolicy: Never
      volumes:
        # Volume template for mounting secrets
        {{- include "stress-test-addons.deploy-volumes" . | nindent 8 }}
      initContainers:
        # Init container template for deploying azure resources on startup
        {{- include "stress-test-addons.init-deploy" . | nindent 8 }}
      containers:
        - name: <stress test name>
          image: <stress test container image>
          command: <startup command array>
          args: <startup args array>
          volumeMounts:
            # These hardcoded names/paths must be preserved
            - name: test-resources-outputs-{{ .Release.Name }}
              mountPath: /mnt/outputs
```

#### Chaos Manifest

Any [chaos experiment](https://chaos-mesh.org/docs/chaos_experiments/networkchaos_experiment) manifests
can be placed in `<stress test directory>/chart/templates/`. See Faults via Dashboard and Faults via Config below.

### Deploying a Stress Test

To build and deploy the stress test, first log in to access the cluster resources if not already set up:

```
az login
az account set --subscription 'Azure SDK Test Resources'
# Log in to the container registry for Docker access
az acr login -n stresstestregistry
# Download the kubeconfig for the cluster
az aks get-credentials -g rg-stress-test-cluster- -n stress-test
```

Then build/publish images and build ARM templates. Make sure the docker image matches what's referenced in the helm templates.

```
# Build and publish image
docker build . -t stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>
docker push stresstestregistry.azurecr.io/<your name>/<test job image name>:<version>

# Compile ARM template (if using Bicep files)
az bicep build -f ./test-resources.bicep --outfile ./chart/test-resources.json

# Install helm dependencies
helm dependency update ./chart
```

Then install the stress test into the cluster:

```
kubectl create namespace <your stress test namespace>
helm install <stress test name> ./chart -f ../../../cluster/kubernetes/environments/test.yaml
```

You can check the progress/status of your installation via:

```
helm list -n <stress test namespace>
```

To stop and remove the test:

```
helm uninstall <stress test name> -n <stress test namespace>
```

To check the status of the stress test job resources:

```
# List stress test pods
kubectl get pods -n <stress test namespace>
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

Faults can be configured via Kubernetes manifests. To see numerous examples of fault configs, head to the [Chaos Experiments docs](https://chaos-mesh.org/docs/chaos_experiments/podchaos_experiment).

## Running the example test with a network fault

Follow the below commands to execute a sample test.

Log in to the cluster:

```
az login
az acr login -n azuresdkdev
az aks get-credentials -g rg-stress-test-cluster- -n stress-test
```

Initialize your resources:

```
cd ./examples

kubectl create namespace <YOUR NAME>
docker build . -t azuresdkdev.azurecr.io/<YOUR NAME>/networkexample:v1
docker push azuresdkdev.azurecr.io/<YOUR NAME>/networkexample:v1
```

Edit the `examples/testjob.yaml` file, changing the `metadata.namespace` and `spec.template.spec.containers[0].image` fields to match against the resources you created above.

Deploy the job:

```
kubectl replace --force -f testjob.yaml
```

Verify the pods in the job have booted and are running ok:

```
⇉ ⇉ ⇉ kubectl get pod -n <YOUR NAMESPACE>
NAME                    READY   STATUS    RESTARTS   AGE
network-example-6vlkm   1/1     Running   0          5s
```

### Faults via Config

Edit the `examples/network_loss.yaml` file, changing the `metadata.namespace` and `spec.selector.namespaces` fields to contain your namespace.

Then apply the chaos experiment:

```
kubectl apply -f ./examples/network_loss.yaml
```

### Faults via Dashboard

Navigate to the chaos dashboard at `localhost:2333`

NOTE: The chaos mesh dashbaord is just a helper for generating manifest under the hood. You can create and submit these directly as well. See the [docs](https://chaos-mesh.org/docs/chaos_experiments/networkchaos_experiment).

1. From the UI, click `New Experiment`
1. Select `Network Attack` and then select `LOSS`
1. In the `Loss` textbox, enter `100`
1. Scroll down to `Scope`. Enter your namespace in the `Namespace Selectors` field, and enter `job-name: ping-example` under `Label Selectors`.
1. Enter a name for experiment like `<YOUR NAME>-network-example`.
1. Enable `Run Continuously`
1. Click through the multiple `Submit` buttons.

You should now be able to see packet loss in the test:

```
⇉ ⇉ ⇉ kubectl logs -n <YOUR NAMESPACE> -l test=network-example -f
...
Spider mode enabled. Check if remote file exists.
--2021-06-09 00:51:52--  http://www.bing.com/
Resolving www.bing.com (www.bing.com)... 204.79.197.200, 13.107.21.200, 2620:1ec:c11::200
Connecting to www.bing.com (www.bing.com)|204.79.197.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|13.107.21.200|:80... failed: Connection timed out.
Connecting to www.bing.com (www.bing.com)|2620:1ec:c11::200|:80... failed: Cannot assign requested address.
Giving up.
```
