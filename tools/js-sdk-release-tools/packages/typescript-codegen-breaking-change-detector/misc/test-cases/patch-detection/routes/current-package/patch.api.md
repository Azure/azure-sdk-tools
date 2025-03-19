# patch

``` ts
export interface ClustersGet {
    a: string;
}
export interface ClusterGetOld {
    a: number;
}
export interface Routes {
    (path: "basic", subscriptionId: string, resourceGroupName: string, clusterName: string): ClusterGetOld;
    (path: "add", subscriptionId: string, resourceGroupName: string, clusterName: string): ClusterGetOld;
    (path: "change_return_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    (path: "change_para_count", subscriptionId: string, resourceGroupName: string): ClusterGetOld;
    (path: "change_para_type", subscriptionId: string, resourceGroupName: string, clusterName: number): ClusterGetOld;
}
```
