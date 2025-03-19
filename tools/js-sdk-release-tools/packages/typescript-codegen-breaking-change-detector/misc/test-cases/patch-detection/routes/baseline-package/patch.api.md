# patch

``` ts
export interface ClustersGet {
    a: number;
}
export interface Routes {
    (path: "basic", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    (path: "remove", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    (path: "change_return_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    (path: "change_para_count", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    (path: "change_para_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
}
```
