import { describe, expect, test } from 'vitest';

import { patchClass } from '../azure/patch/patch-detection';
import { createTestAstContext } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';

describe('detect constructor', async () => {
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

  test("change type of constructor's parameter", async () => {
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

  test("change name of constructor's parameter", async () => {
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
    expect(breakingPairs.length).toBe(0);
  });

  test("change constructor's parameters list", async () => {
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
});

describe('detect on class level', async () => {
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

describe('detect classic properties', async () => {
  test('add properties', async () => {
    const baselineApiView = `
      class TestClass {
        prop1: string;
      }`;
    const currentApiView = `
      class TestClass {
        prop1: string;
        prop2: number;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Property);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Added);
    expect(breakingPairs[0].source?.name).toBe('prop2');
  });

  test('remove properties', async () => {
    const baselineApiView = `
      class TestClass {
        prop1: string;
        prop2: number;
      }`;
    const currentApiView = `
      class TestClass {
        prop2: number;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Property);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.Removed);
    expect(breakingPairs[0].target?.name).toBe('prop1');
  });

  test('change type of properties', async () => {
    const baselineApiView = `
      class TestClass {
        prop1: string;
      }`;
    const currentApiView = `
      class TestClass {
        prop1: number;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Property);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].target?.name).toBe('prop1');
  });
});

describe('detect member functions', async () => {
  test('change parameter type of member functions', async () => {
    const baselineApiView = `
      class TestClass {
        method1(param1: string): void;
      }`;
    const currentApiView = `
      class TestClass {
        method1(param1: number): void;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Parameter);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].target?.name).toBe('param1');
  });

  test('change return node type of member functions', async () => {
    const baselineApiView = `
      class TestClass {
        method1(param1: string): void;
      }`;
    const currentApiView = `
      class TestClass {
        method1(param1: string): string;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(breakingPairs[0].target?.name).toBe('method1');
  });

  test('change parameter count of member functions', async () => {
    const baselineApiView = `
      class TestClass {
        method1(param1: string, param2: string): void;
      }`;
    const currentApiView = `
      class TestClass {
        method1(param1: string): void;
      }`;

    const astContext = createTestAstContext(baselineApiView, currentApiView);
    const breakingPairs = patchClass('TestClass', astContext, AssignDirection.CurrentToBaseline);
    expect(breakingPairs.length).toBe(1);
    expect(breakingPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(breakingPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
    expect(breakingPairs[0].reasons).toBe(DiffReasons.CountChanged);
    expect(breakingPairs[0].target?.name).toBe('method1');
  });
});

describe('detect class', async () => {});
