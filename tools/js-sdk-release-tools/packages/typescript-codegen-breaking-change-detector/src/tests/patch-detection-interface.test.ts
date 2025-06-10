import { describe, expect, test } from 'vitest';

import { patchInterface } from '../azure/patch/patch-detection';
import { createTestAstContext } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';

describe('detect interface', () => {
  describe('detect on interface level', () => {
    test('remove interface', () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = ``;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Interface);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('TestInterface');
    });

    test('add interface', () => {
      const baselineApiView = ``;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Interface);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('TestInterface');
    });
  });

  describe('detect on call signature', () => {
    test('remove call signature', () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('(para: string): void;');
    });

    test('add call signature', () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { (para: string): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('(para: string): void;');
    });

    test('change parameter type', async () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface { (para: number): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('(para: string): void;');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Signature);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('(para: number): void;');
    });

    test('change parameter name', async () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface { (para2: string): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change parameter count', () => {
      const baselineApiView = `export interface TestInterface { (para1: string, para2: number): void; }`;
      const currentApiView = `export interface TestInterface { (para1: string): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('(para1: string, para2: number): void;');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Signature);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('(para1: string): void;');
    });

    test('change return type', () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface { (para: string): number; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('(para: string): void;');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Signature);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('(para: string): number;');
    });
  });

  describe('detect on classic property', () => {
    test('add classic property', () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove classic property', () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change classic property type', () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop: number; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change classic property name', () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop2: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Property);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('prop2');
    });

    test('change classic property readonly to mutable', () => {
      const baselineApiView = `export interface TestInterface { readonly prop: string; }`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.ReadonlyToMutable);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    // TODO: this should be a breaking change?
    test('change classic property mutable to readonly', () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { readonly prop: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change classic property required to optional', () => {
      const baselineApiView = `export interface TestInterface { prop?: string; }`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.RequiredToOptional);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    // TODO: this should be breaking change?
    test('change classic property optional to required', () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop?: string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });
  });

  describe('detect on arrow function property', () => {
    test('add arrow function property', () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop: () => void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove arrow function property', () => {
      const baselineApiView = `export interface TestInterface { prop: () => void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change arrow function property name', () => {
      const baselineApiView = `export interface TestInterface { prop: () => void; }`;
      const currentApiView = `export interface TestInterface { prop2: () => void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Signature);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('prop2');
    });

    test('change arrow function property return type', () => {
      const baselineApiView = `export interface TestInterface { prop: () => string; }`;
      const currentApiView = `export interface TestInterface { prop: () => number; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change arrow function property parameter type', () => {
      const baselineApiView = `export interface TestInterface { prop: (para: string) => string; }`;
      const currentApiView = `export interface TestInterface { prop: (para: number) => string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Parameter);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('para');
    });

    test('change arrow function property parameter name', () => {
      const baselineApiView = `export interface TestInterface { prop: (para: string) => void; }`;
      const currentApiView = `export interface TestInterface { prop: (para2: string) => void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change arrow function property parameters count', () => {
      const baselineApiView = `export interface TestInterface { prop: (para1: string, para2: number) => void; }`;
      const currentApiView = `export interface TestInterface { prop: (para1: string) => void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
      expect(diffPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });
  });

  describe('detect on member function property', () => {
    test('add member function property', () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop(): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove member function property', () => {
      const baselineApiView = `export interface TestInterface { prop(): void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change member function property name', () => {
      const baselineApiView = `export interface TestInterface { prop(): void; }`;
      const currentApiView = `export interface TestInterface { prop2(): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(2);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Signature);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[1].source?.name).toBe('prop2');
    });

    test('change member function property return type', () => {
      const baselineApiView = `export interface TestInterface { prop(): string; }`;
      const currentApiView = `export interface TestInterface { prop(): number; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change member function property parameter type', () => {
      const baselineApiView = `export interface TestInterface { prop(para: string): string; }`;
      const currentApiView = `export interface TestInterface { prop(para: number): string; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Parameter);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('para');
    });

    test('change member function property parameter name', () => {
      const baselineApiView = `export interface TestInterface { prop(para: string): void; }`;
      const currentApiView = `export interface TestInterface { prop(para2: string): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change member function property parameters count', () => {
      const baselineApiView = `export interface TestInterface { prop(para1: string, para2: number): void; }`;
      const currentApiView = `export interface TestInterface { prop(para1: string): void; }`;

      const astContext = createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
      expect(diffPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });
  });
});
// TODO: detect enum
