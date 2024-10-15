import { mkdirp, pathExists, remove } from 'fs-extra';
import { describe, expect, test } from 'vitest';

import { join } from 'node:path';
import { createAstContext } from '../azure/detect-breaking-changes';
import { patchFunction, patchRoutes, patchUnionType } from '../azure/patch/patch-detection';
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

      breakingPairs = patchFunction('funcParameterCount', astContext);
      expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ParameterList);

      breakingPairs = patchFunction('funcParameterType', astContext);
      expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);

      breakingPairs = patchFunction('funcRemove', astContext);
      expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);

      breakingPairs = patchFunction('funcAdd', astContext);
      expect(breakingPairs.find((p) => p.assignDirection !== AssignDirection.CurrentToBaseline)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
      expect(breakingPairs[0].location).toBe(DiffLocation.Signature);
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

      expect(breakingPairs[1].location).toBe(DiffLocation.Signature_ReturnType);
      expect(breakingPairs[1].reasons).toBe(DiffReasons.TypeChanged);

      expect(breakingPairs[2].location).toBe(DiffLocation.Signature_ParameterList);
      expect(breakingPairs[2].reasons).toBe(DiffReasons.CountChanged);

      expect(breakingPairs[3].location).toBe(DiffLocation.Parameter);
      expect(breakingPairs[3].reasons).toBe(DiffReasons.TypeChanged);

      expect(breakingPairs[4].location).toBe(DiffLocation.Signature);
      expect(breakingPairs[4].reasons).toBe(DiffReasons.Added);
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
      let breakingPairs = patchUnionType('types', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);

      breakingPairs = patchUnionType('typesRemove', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);

      breakingPairs = patchUnionType('typesAdd', astContext, AssignDirection.CurrentToBaseline);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(breakingPairs[0].location).toBe(DiffLocation.TypeAlias);
      expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });
});
