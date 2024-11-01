import { mkdirp, pathExists, remove } from 'fs-extra';
import { describe, expect, test } from 'vitest';

import { join } from 'node:path';
import { createAstContext } from '../azure/detect-breaking-changes';
import {
  findOperationContextPairsInHighLevelClient,
  findOperationContextPairsInModularClient,
  findOperationContextPairsInRestLevelClient,
  patchFunction,
  patchOperationParameterName,
  patchRoutes,
  patchTypeAlias,
} from '../azure/patch/patch-detection';
import { createTempFolder, getFormattedDate } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';

const testCaseDir = '../../misc/test-cases/patch-detection';

describe("patch current tool's breaking changes", async () => {
  test('detect function overloads', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      const tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);
      let breakingPairs = patchFunction('isUnexpected', astContext);

      expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
      expect(breakingPairs.length).toBe(2);

      expect(breakingPairs[0].location).toBe(DiffLocation.Signature_Overload);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);

      expect(breakingPairs[1].location).toBe(DiffLocation.Signature_Overload);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.Added);
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });

  test('detect function', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      const tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);

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
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });

  test('detect routes', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      const tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);
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
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });

  test('detect union types', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      const tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);
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
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });

  test('detect parameter name change', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      const tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);

      // rest level client
      let breakingPairs = patchOperationParameterName(astContext, findOperationContextPairsInRestLevelClient);
      expect(breakingPairs.length).toBe(2);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.BaselineToCurrent);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.NameChanged);
      expect(breakingPairs[0].target?.name).toBe('resourceGroupName');

      expect(breakingPairs[1].assignDirection).toBe(AssignDirection.BaselineToCurrent);
      expect(breakingPairs[1].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.NameChanged);
      expect(breakingPairs[1].target?.name).toBe('subscriptionId');

      // modular client
      breakingPairs = patchOperationParameterName(astContext, findOperationContextPairsInModularClient);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.BaselineToCurrent);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.NameChanged);
      expect(breakingPairs[0].target?.name).toBe('resourceGroupName1');

      // high level client
      breakingPairs = patchOperationParameterName(astContext, findOperationContextPairsInHighLevelClient);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.BaselineToCurrent);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.NameChanged);
      expect(breakingPairs[0].target?.name).toBe('resourceGroupName2');
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });
});
