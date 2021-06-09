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
1. Azure CLI

## Access

To access the cluster, run the following:

```
az login
az account set --subscription 'Azure SDK Developer Playground'
# Download the kubeconfig for the cluster
az aks get-credentials -g bebroder-aks-sdk-dev -n aks-sdk-dev
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

To get started, you will need to create a container image containing your long-running test, and a manifest to execute that image as a [kubernetes job](https://kubernetes.io/docs/concepts/workloads/controllers/job/).

The Dockerfile for your image should contain your test code/artifacts. See [docs on how to create a Dockerfile](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)

To create any resources in the cluster, you will need to create a namespace for them to live in:

```
kubectl create namespace <your name>
```

You will then need to build and push your container image to a registry the cluster has access to:

```
az acr login -n azuresdkdev
docker build . -t azuresdkdev.azurecr.io/<<your name>/test job image name>:<version>
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
az aks get-credentials -g bebroder-aks-sdk-dev -n aks-sdk-dev
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
