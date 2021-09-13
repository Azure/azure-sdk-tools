## Stress Watcher

Stress Watcher is a service that watches for Kubernetes resource changes and triggers actions on those events.
It is not a fully fledged Kubernetes controller, but rather a simple use of the [watch
API](https://kubernetes.io/docs/reference/using-api/api-concepts/#efficient-detection-of-changes) using the [Kubernetes
client for C#](https://github.com/kubernetes-client/csharp). This is intended to be used primarily [stress
test clusters](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md).


**Current functionality:**

1. Watch stress test pod events for a `Running` state, and start any chaos actions against them.


**Planned functionality:**

1. Watch stress test pod events for a `Completed` or `Error` state, and clean up Azure resources deployed by the test.


## Running

Running locally, the service requires a
[kubeconfig](https://kubernetes.io/docs/concepts/configuration/organize-cluster-access-kubeconfig/) file in order to
watch the Kubernetes API.

To gain access to the testing stress test cluster, see
[Access](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#access).

Once the kubeconfig has been set up:

**Run the service**

```
dotnet run
```

**Test the service**

```
dotnet test
```
