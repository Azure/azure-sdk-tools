import { mkdirp, pathExists, remove } from 'fs-extra';
import { describe, expect, test } from 'vitest';

import { join } from 'node:path';
import { createAstContext } from '../azure/detect-breaking-changes';
import { patchFunction, patchRoutes, patchUnionType } from '../azure/patch/patch-detection';
import { createTempFolder, getFormattedDate } from './utils';
import { BreakingLocation, BreakingReasons, ModelType } from '../azure/common/types';

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
      const breakingPairs = patchFunction('isUnexpected', astContext);

      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);

      expect(breakingPairs[0].location).toBe(BreakingLocation.FunctionOverload);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.Removed);
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
      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(0);

      breakingPairs = patchFunction('funcReturnType', astContext);
      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.TypeChanged);
      expect(breakingPairs[0].location).toBe(BreakingLocation.FunctionReturnType);

      breakingPairs = patchFunction('funcParameterCount', astContext);
      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.CountChanged);
      expect(breakingPairs[0].location).toBe(BreakingLocation.FunctionParameterList);

      breakingPairs = patchFunction('funcParameterType', astContext);
      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.TypeChanged);
      expect(breakingPairs[0].location).toBe(BreakingLocation.FunctionParameter);

      breakingPairs = patchFunction('funcRemove', astContext);
      expect(breakingPairs.find((p) => p.modelType !== ModelType.Output)).toBeUndefined();
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.Removed);
      expect(breakingPairs[0].location).toBe(BreakingLocation.Function);
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
      expect(breakingPairs.length).toBe(4);

      expect(breakingPairs[0].location).toBe(BreakingLocation.Call);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.Removed);

      expect(breakingPairs[1].location).toBe(BreakingLocation.FunctionReturnType);
      expect(breakingPairs[1].reasons).toBe(BreakingReasons.TypeChanged);

      expect(breakingPairs[2].location).toBe(BreakingLocation.FunctionParameterList);
      expect(breakingPairs[2].reasons).toBe(BreakingReasons.CountChanged);

      expect(breakingPairs[3].location).toBe(BreakingLocation.FunctionParameter);
      expect(breakingPairs[3].reasons).toBe(BreakingReasons.TypeChanged);
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
      const breakingPairs = patchUnionType('types', astContext, ModelType.Output);
      expect(breakingPairs.length).toBe(1);
      expect(breakingPairs[0].modelType).toBe(ModelType.Output);
      expect(breakingPairs[0].location).toBe(BreakingLocation.TypeAlias);
      expect(breakingPairs[0].reasons).toBe(BreakingReasons.TypeChanged);
    } finally {
      if (tempFolder) remove(tempFolder);
    }
  });
});
