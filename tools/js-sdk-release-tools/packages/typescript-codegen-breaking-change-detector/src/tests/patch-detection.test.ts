import { describe, expect, test } from 'vitest';

import { patchClass, patchFunction, patchRoutes, patchTypeAlias } from '../azure/patch/patch-detection';
import { createTestAstContext } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';

describe("patch current tool's breaking changes", async () => {
  test('detect function overloads', async () => {
    const currentApiView = ``;
    const baselineApiView = ``;

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
    const currentApiView = ``;
    const baselineApiView = ``;

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
    const currentApiView = ``;
    const baselineApiView = ``;

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
    const currentApiView = ``;
    const baselineApiView = ``;

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
    test('removing constructors', async () => {
      const currentApiView = ``;
      const baselineApiView = ``;

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

    test('removing class', async () => {
      const currentApiView = ``;
      const baselineApiView = ``;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('RemoveClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('RemoveClass');
    });

    test('adding class', async () => {
      const currentApiView = ``;
      const baselineApiView = ``;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('AddClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].source?.name).toBe('AddClass');
    });

    test('remove methods (including arrow functions and classic methods)', async () => {
      const baselineApiView = `
        class RemoveMethodClass {
          removeMethod(a: string): void;
          removeArrowFunc(a: string): void;
        }`;
      const currentApiView = `class RemoveMethodClass {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('RemoveMethodClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(2);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('removeMethod');
      expect(breakingPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[1].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[1].target?.name).toBe('removeArrowFunc');
    });

    test('remove classic properties', async () => {
      const baselineApiView = `
        export class RemovePropClass {
          removeProp: string;
        }`;
      const currentApiView = `
        export class RemovePropClass {
        }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const breakingPairs = patchClass('RemovePropClass', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Property);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('removeProp');
    });
  });
});
