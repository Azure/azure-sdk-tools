This directory contains a helm chart for deploying stress cluster services.  Currently this includes:

1. [chaos-mesh](https://chaos-mesh.org/docs/)
1. [stress watcher](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/services/Stress.Watcher)

These services will be deployed by default on new cluster buildout, see [docs](https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/cluster#deploying-clusters).

For development of services in this chart like `stress watcher`, they can be deployed independently of the cluster and chaos mesh resources:

```
../../provision.ps1 -Development -Namespace <your alias> -Environment <test or dev if building your own cluster>
```

To cleanup development resources:

```
helm uninstall stress-infra -n <your alias>
kubectl delete ns <your alias>
```
