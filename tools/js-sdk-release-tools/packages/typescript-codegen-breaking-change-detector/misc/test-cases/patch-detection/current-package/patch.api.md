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

export class classPropertyChange {private a: string;}
export class classPropertyType {a: number;}
export class classAdd {a: string;}
export class classExpand {a: string;b: string;}
export class classNarrow {a: string;}
export class classConstructorParameterCount { constructor(a: string){} }
export class classConstructorParameterType { constructor(a: number, b: number){} }
export class classConstructorParameterOptional { constructor(a: string, b?: string){} }
export class classConstructorRemove { constructor(a?: number, b?: number, c?: number){} }
export class classConstructorAdd { constructor(a: number, b: number){} }
export class classMethodOptionalToRequired {func(a: string){}}
```
