import { describe, expect, test } from 'vitest';

import { patchClass, patchFunction, patchRoutes, patchTypeAlias } from '../azure/patch/patch-detection';
import { createTestAstContext } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';

describe("patch current tool's breaking changes", async () => {
  test('detect function overloads', async () => {
    const baselineApiView = `
    export interface A {a: string;}
    export interface B {b: string;}
    export interface C {c: string;}
    export interface D {d: string;}
    export function isUnexpected(response: A | B): response is A;
    export function isUnexpected(response: C | D): response is A;`;

    const currentApiView = `
    export interface A {a: string;}
    export interface B {b: string;}
    export interface C {c: string;}
    export interface D {d: string;}
    export function isUnexpected(response: A | B): response is A;
    export function isUnexpected(response: C | E): response is C;`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    let breakingPairs = patchFunction('isUnexpected', astContext);

    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(2);

    expect(breakingPairs[0].location).toBe(DiffLocation.Signature_Overload);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);

    expect(breakingPairs[1].location).toBe(DiffLocation.Signature_Overload);
    expect(breakingPairs[1].reasons).toBe(DiffReasons.Added);
  });

  // TODO: seperate tests
  test('detect function', async () => {
    const baselineApiView = `
    export function funcBasic(a: string): string
    export function funcReturnType(a: string): string
    export function funcParameterCount(a: string, b: string): string
    export function funcParameterType(a: string): string
    export function funcRemove(a: string): string`;

    const currentApiView = `
    export function funcBasic(a: string): string
    export function funcReturnType(a: string): number
    export function funcParameterCount(a: string, b: string, c: string): string
    export function funcParameterType(a: number): string
    export function funcAdd(a: string): string`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);

    let breakingPairs = patchFunction('funcBasic', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(0);

    breakingPairs = patchFunction('funcReturnType', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
    expect(breakingPairs[0].target?.name).toBe('funcReturnType');

    breakingPairs = patchFunction('funcParameterCount', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.CountChanged);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
    expect(breakingPairs[0].target?.name).toBe('funcParameterCount');

    breakingPairs = patchFunction('funcParameterType', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
    expect(breakingPairs[0].target?.name).toBe('a');

    breakingPairs = patchFunction('funcRemove', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
    expect(breakingPairs[0].target?.name).toBe('funcRemove');

    breakingPairs = patchFunction('funcAdd', astContext);
    expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
    expect(breakingPairs[0].source?.name).toBe('funcAdd');
  });

  // TODO: seperate tests
  test('detect routes', async () => {
    const baselineApiView = `
        export interface ClustersGet {
        a: number;
    }
    export interface Routes {
        (path: "basic", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
        (path: "remove", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
        (path: "change_return_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
        (path: "change_para_count", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
        (path: "change_para_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;
    }`;

    const currentApiView = `
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
    }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchRoutes(astContext);
    expect(breakingPairs.length).toBe(5);

    expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
    expect(breakingPairs[0].source).toBeUndefined();
    expect(breakingPairs[0].target?.node.getText()).toBe(
      '(path: "remove", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;'
    );

    expect(breakingPairs[1].location).toBe(DiffLocation.Signature_ReturnType);
    expect(breakingPairs[1].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[1].target?.name).toBe(
      '(path: "change_return_type", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;'
    );

    expect(breakingPairs[2].location).toBe(DiffLocation.Signature_ParameterList);
    expect(breakingPairs[2].reasons).toBe(DiffReasons.CountChanged);
    expect(breakingPairs[2].target?.node.getText()).toBe(
      '(path: "change_para_count", subscriptionId: string, resourceGroupName: string, clusterName: string): ClustersGet;'
    );

    expect(breakingPairs[3].location).toBe(DiffLocation.Parameter);
    expect(breakingPairs[3].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[3].target?.node.getText()).toBe('clusterName: string');

    expect(breakingPairs[4].location).toBe(DiffLocation.Signature);
    expect(breakingPairs[4].reasons).toBe(DiffReasons.Added);
    expect(breakingPairs[4].source?.node.getText()).toBe(
      '(path: "add", subscriptionId: string, resourceGroupName: string, clusterName: string): ClusterGetOld;'
    );
    expect(breakingPairs[4].target).toBeUndefined();
  });

  // TODO: seperate tests
  test('detect union types', async () => {
    const baselineApiView = `
    export type typesChange = "basic" | "remove";
    export type typesRemove = "basic" | "remove";

    export type typesExpand = string | number;
    export type typesNarrow = string | number | boolean;`;

    const currentApiView = `export type typesChange = "basic" | "rEmove";
    export type typesAdd = "basic" | "rEmove";

    export type typesExpand = string | number | boolean;
    export type typesNarrow = string | number;`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    let breakingPairs = patchTypeAlias('typesChange', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].target?.name).toBe('typesChange');

    breakingPairs = patchTypeAlias('typesRemove', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
    expect(breakingPairs[0].target?.name).toBe('typesRemove');

    breakingPairs = patchTypeAlias('typesAdd', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
    expect(breakingPairs[0].source?.name).toBe('typesAdd');

    breakingPairs = patchTypeAlias('typesExpand', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].source?.name).toBe('typesExpand');

    breakingPairs = patchTypeAlias('typesNarrow', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].source?.name).toBe('typesNarrow');
  });

  describe('detect class', async () => {
    test('add constructors', async () => {
      const baselineApiView = `
      class AddClassConstructor {
      }`;
      const currentApiView = `
      class AddClassConstructor {
        constructor(p1: string, p2: string) {}
      }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('AddClassConstructor', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(0);
    });

    test('remove constructors', async () => {
      const baselineApiView = `
      class RemoveClassConstructor {
        constructor(remove: string) {}
        constructor(p1: string, p2: string) {}
      }`;
      const currentApiView = `
      class RemoveClassConstructor {
      }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('RemoveClassConstructor', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(2);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('constructor(remove: string) {}');
      expect(breakingPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[1].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[1].target?.name).toBe('constructor(p1: string, p2: string) {}');
    });

    test('change type of constructor\'s parameter', async () => {
      const baselineApiView = `
      class TestClass {
        constructor(p1: string, p2: string) {}
      }`;
      const currentApiView = `
      class TestClass {
        constructor(p2: string, p2: number) {}
      }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('constructor(p1: string, p2: string) {}');
    });

    test('change name of constructor\'s parameter', async () => {
      const baselineApiView = `
      class TestClass {
        constructor(p1: string, p2: string) {}
      }`;
      const currentApiView = `
      class TestClass {
        constructor(p2: string, p3: string) {}
      }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('constructor(p1: string, p2: string) {}');
    });

    test('change constructor\'s parameters list', async () => {
      const baselineApiView = `
      class TestClass {
        constructor(p1: string, p2: string, p3: string) {}
      }`;
      const currentApiView = `
      class TestClass {
        constructor(p2: string, p2: string) {}
      }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('constructor(p1: string, p2: string, p3: string) {}');
    });

    test('remove class', async () => {
      const baselineApiView = `class RemoveClass {}`;
      const currentApiView = ``;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('RemoveClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('RemoveClass');
    });

    test('add class', async () => {
      const baselineApiView = ``;
      const currentApiView = `class AddClass {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('AddClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].source?.name).toBe('AddClass');
    });
  });
});
