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
    (path: "change_para_name", resourceGroupName: string, subscriptionId: string, clusterName: string): ClusterGetOld;
}

export type typesChange = "basic" | "rEmove";
export type typesAdd = "basic" | "rEmove";

export type typesExpand = string | number | boolean;
export type typesNarrow = string | number;

export interface A {a: string;}
export interface B {b: string;}
export interface C {c: string;}
export interface D {d: string;}
export function isUnexpected(response: A | B): response is A;
export function isUnexpected(response: C | E): response is C;

export function funcBasic(a: string): string
export function funcReturnType(a: string): number
export function funcParameterCount(a: string, b: string, c: string): string
export function funcParameterType(a: number): string
export function funcAdd(a: string): string

export interface ModularOperations {
    changeParameterName: (resourceGroupName1: string, fleetName: string, options?: A) => B;
}

export interface HighLevelClientOpGroup {
    changeParameterName(resourceGroupName2: string, fleetName: string, options?: A): B;
}

export interface HighLevelClientSomeInterface {
    method(resourceGroupName3: string, fleetName: string, options?: A): B;
    prop: string
}
```
