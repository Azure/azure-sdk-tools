import { mkdirp, pathExists, remove } from 'fs-extra';
import { describe, expect, test } from 'vitest';

import { join } from 'node:path';
import { createAstContext } from '../azure/detect-breaking-changes';
import { patchFunction, patchRoutes, patchTypeAlias, patchClass} from '../azure/patch/patch-detection';
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

  test('detect class', async () => {
    const currentApiViewPath = join(__dirname, testCaseDir, 'current-package/patch.api.md');
    const baselineApiViewPath = join(__dirname, testCaseDir, 'baseline-package/patch.api.md');
    const date = getFormattedDate();

    let tempFolder: string | undefined = undefined;
    try {
      tempFolder = await createTempFolder(`.tmp/temp-${date}`);
      const astContext = await createAstContext(baselineApiViewPath, currentApiViewPath, tempFolder);
      let breakingPairs = patchClass('classMethodOptionalToRequired', astContext);
      expect(breakingPairs.length).toBe(0);

      breakingPairs = patchClass('classPropertyChange', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Property);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.ModifierFlag);
      expect(breakingPairs[0].target?.name).toBe('a');

      breakingPairs = patchClass('classPropertyType', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Property);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(breakingPairs[0].target?.name).toBe('a');

      breakingPairs = patchClass('classRemove', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('classRemove');

      breakingPairs = patchClass('classAdd', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Class);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].source?.name).toBe('classAdd');

      breakingPairs = patchClass('classExpand', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Property);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].source?.name).toBe('b');

      breakingPairs = patchClass('classNarrow', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.Property);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].target?.name).toBe('b');

      breakingPairs = patchClass('classConstructorParameterCount', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(breakingPairs[0].target?.node.getText()).toBe(
        'constructor(a: string, b: string){}'
      );

      breakingPairs = patchClass('classConstructorParameterType', astContext);
      expect(breakingPairs.length).toBe(2);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(breakingPairs[0].target?.node.getText()).toBe('a: string');

      expect(breakingPairs[1].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.TypeChanged);
      expect(breakingPairs[1].target?.node.getText()).toBe('b: string');

      breakingPairs = patchClass('classConstructorRemove', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].location).toBe(DiffLocation.Constructor);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].source).toBeUndefined();
      expect(breakingPairs[0].target?.node.getText()).toBe(
        'constructor(a?: number, b?: number, c?: number){}'
      );

      breakingPairs = patchClass('classConstructorAdd', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].location).toBe(DiffLocation.Constructor);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].target).toBeUndefined();
      expect(breakingPairs[0].source?.node.getText()).toBe(
        'constructor(a: number, b: number){}'
      );

      breakingPairs = patchClass('classConstructorParameterOptional', astContext);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.RequiredToOptional);
      expect(breakingPairs[0].target?.node.getText()).toBe(
        'b?: string'
      );;
      expect(breakingPairs[0].source?.node.getText()).toBe(
        'b: string'
      );
      
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });
});
