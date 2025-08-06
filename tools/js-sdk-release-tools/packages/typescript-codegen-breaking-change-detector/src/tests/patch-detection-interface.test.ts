import { describe, expect, test } from 'vitest';

import { patchInterface } from '../azure/patch/patch-detection';
import { createTestAstContext } from './utils';
import { DiffLocation, DiffReasons, AssignDirection } from '../azure/common/types';
import { SyntaxKind } from 'ts-morph';

describe('detect interface', () => {
  describe('detect on interface level', () => {
    test('remove interface', async () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = ``;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Interface);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('TestInterface');
    });

    test('add interface', async () => {
      const baselineApiView = ``;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Interface);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('TestInterface');
    });
  });

  describe('detect on call signature', () => {
    test('remove call signature', async () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('(para: string): void;');
    });

    test('add call signature', async () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { (para: string): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change parameter count', async () => {
      const baselineApiView = `export interface TestInterface { (para1: string, para2: number): void; }`;
      const currentApiView = `export interface TestInterface { (para1: string): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

    test('change return type', async () => {
      const baselineApiView = `export interface TestInterface { (para: string): void; }`;
      const currentApiView = `export interface TestInterface { (para: string): number; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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
    test('add classic property', async () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove classic property', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change classic property type', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop: number; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
      expect(diffPairs[0].target?.node.asKind(SyntaxKind.PropertySignature)?.getTypeNode()?.getText()).toBe('string');
      expect(diffPairs[0].source?.node.asKind(SyntaxKind.PropertySignature)?.getTypeNode()?.getText()).toBe('number');
    });

    test('detect property move to parent model', async () => {
      const baselineApiView = `
        export interface SystemData {} 
        export interface Target extends ProxyResource {
          systemdata?: SystemData;
          location?: string;
        }
        export interface ProxyResource{}
        `;
      const currentApiView = `
        export interface Target extends ProxyResource {
        location?: string;
        }
        export interface SystemData {} 
        export interface ProxyResource{systemdata?: SystemData;}
        `;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('ignore change property type', async () => {
      const baselineApiView = `export interface TestInterface { prop: Record<string, unknown>; }`;
      const currentApiView = `export interface TestInterface { prop: any; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change classic property type to equivalent type alias', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export type Str = string; export interface TestInterface { prop: Str; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change classic property type between equivalent JS/TS type', async () => {
      const baselineApiView = `export interface AAA {p: string}; export interface TestInterface { prop: Record<string, AAA>; }`;
      const currentApiView = `export interface AAA {p: string}; export interface TestInterface { prop: {[p: string]: AAA}; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change classic property name', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop2: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

    test('change classic property readonly to mutable', async () => {
      const baselineApiView = `export interface TestInterface { readonly prop: string; }`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.ReadonlyToMutable);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change classic property mutable to readonly', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { readonly prop: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change classic property required to optional (current to baseline)', async () => {
      const baselineApiView = `export interface TestInterface { prop?: string; }`;
      const currentApiView = `export interface TestInterface { prop: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.RequiredToOptional);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change classic property optional to required (current to baseline)', async () => {
      const baselineApiView = `export interface TestInterface { prop: string; }`;
      const currentApiView = `export interface TestInterface { prop?: string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.OptionalToRequired);
      expect(diffPairs[0].target?.name).toBe('prop');
    });
  });

  describe('detect on arrow function property', () => {
    test('add arrow function property', async () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop: () => void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove arrow function property', async () => {
      const baselineApiView = `export interface TestInterface { prop: () => void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change arrow function property name', async () => {
      const baselineApiView = `export interface TestInterface { prop: () => void; }`;
      const currentApiView = `export interface TestInterface { prop2: () => void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

    test('change arrow function property return type', async () => {
      const baselineApiView = `export interface TestInterface { prop: () => string; }`;
      const currentApiView = `export interface TestInterface { prop: () => number; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change arrow function property return type for paging case', async () => {
      const baselineApiView = `
        export interface Paging<T> {t: T};
        export interface Pet {a: string};
        export interface TestInterface { prop: () => Paging<Pet>; }`;
      const currentApiView = `
        export interface Paging<T> {t: T};
        export interface Cat {a: string};
        export interface TestInterface { prop: () => Cat; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change arrow function property parameter type', async () => {
      const baselineApiView = `export interface TestInterface { prop: (para: string) => string; }`;
      const currentApiView = `export interface TestInterface { prop: (para: number) => string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Parameter);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('para');
      expect(diffPairs[0].target?.node.asKind(SyntaxKind.Parameter)?.getTypeNode()?.getText()).toBe('string');
      expect(diffPairs[0].source?.node.asKind(SyntaxKind.Parameter)?.getTypeNode()?.getText()).toBe('number');
    });

    test('change arrow function property parameter name', async () => {
      const baselineApiView = `export interface TestInterface { prop: (para: string) => void; }`;
      const currentApiView = `export interface TestInterface { prop: (para2: string) => void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change arrow function property parameters count', async () => {
      const baselineApiView = `export interface TestInterface { prop: (para1: string, para2: number) => void; }`;
      const currentApiView = `export interface TestInterface { prop: (para1: string) => void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
      expect(diffPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });
  });

  describe('detect on member function property', () => {
    test('add member function property', async () => {
      const baselineApiView = `export interface TestInterface {}`;
      const currentApiView = `export interface TestInterface { prop(): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[0].source?.name).toBe('prop');
    });

    test('remove member function property', async () => {
      const baselineApiView = `export interface TestInterface { prop(): void; }`;
      const currentApiView = `export interface TestInterface {}`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change member function property name', async () => {
      const baselineApiView = `export interface TestInterface { prop(): void; }`;
      const currentApiView = `export interface TestInterface { prop2(): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
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

    test('change member function property return type', async () => {
      const baselineApiView = `export interface TestInterface { prop(): string; }`;
      const currentApiView = `export interface TestInterface { prop(): number; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ReturnType);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });

    test('change member function property parameter type', async () => {
      const baselineApiView = `export interface TestInterface { prop(para: string): string; }`;
      const currentApiView = `export interface TestInterface { prop(para: number): string; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Parameter);
      expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
      expect(diffPairs[0].target?.name).toBe('para');
      expect(diffPairs[0].target?.node.asKind(SyntaxKind.Parameter)?.getTypeNode()?.getText()).toBe('string');
      expect(diffPairs[0].source?.node.asKind(SyntaxKind.Parameter)?.getTypeNode()?.getText()).toBe('number');
    });

    test('change member function property parameter name', async () => {
      const baselineApiView = `export interface TestInterface { prop(para: string): void; }`;
      const currentApiView = `export interface TestInterface { prop(para2: string): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('change member function property parameters count', async () => {
      const baselineApiView = `export interface TestInterface { prop(para1: string, para2: number): void; }`;
      const currentApiView = `export interface TestInterface { prop(para1: string): void; }`;

      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(1);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Signature_ParameterList);
      expect(diffPairs[0].reasons).toBe(DiffReasons.CountChanged);
      expect(diffPairs[0].target?.name).toBe('prop');
    });
  });

  describe('detect property refactoring to nested object', () => {
    test('detect basic property refactoring with exact match', async () => {
      // Test basic refactoring with various property types and modifiers
      const baselineApiView = `export interface Target { 
        prop1: string; 
        prop2?: number; 
        readonly prop3: boolean;
        prop4: string | number;
        prop5: string;
      }`;
      const currentApiView = `export interface Nested { 
        prop1: string; 
        prop2?: number; 
        readonly prop3: boolean;
        prop4: string | number;
        prop5: string;
        prop6: boolean; // additional property in nested object
      } export interface Target { props: Nested; }`;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('detect property refactoring with type aliases', async () => {
      const baselineApiView = `export interface Target { prop1: string; prop2: number; }`;
      const currentApiView = `
        export type StringType = string;
        export type NumberType = number;
        export interface Nested { prop1: StringType; prop2: NumberType; } 
        export interface Target { props: Nested; }`;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      expect(diffPairs.length).toBe(0);
    });

    test('detect property refactoring with partial match - should not filter', async () => {
      const baselineApiView = `export interface Target { prop1: string; prop2: string; prop3: number; }`;
      const currentApiView = `export interface Nested { prop1: string; prop2: string; } export interface Target { props: Nested; }`;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      // Should have 4 diffs: 3 removed (prop1, prop2, prop3) + 1 added (props)
      expect(diffPairs.length).toBe(4);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop1');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Property);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[1].target?.name).toBe('prop2');
      expect(diffPairs[2].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[2].location).toBe(DiffLocation.Property);
      expect(diffPairs[2].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[2].target?.name).toBe('prop3');
      expect(diffPairs[3].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[3].location).toBe(DiffLocation.Property);
      expect(diffPairs[3].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[3].source?.name).toBe('props');
    });

    test('detect property refactoring with different types - should not filter', async () => {
      const baselineApiView = `export interface Target { prop1: string; prop2: number; }`;
      const currentApiView = `export interface Nested { prop1: string; prop2: string; } export interface Target { props: Nested; }`;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      // Should have 3 diffs: 2 removed + 1 added (types don't match exactly)
      expect(diffPairs.length).toBe(3);
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('prop1');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Property);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[1].target?.name).toBe('prop2');
      expect(diffPairs[2].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[2].location).toBe(DiffLocation.Property);
      expect(diffPairs[2].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[2].source?.name).toBe('props');
    });

    test('detect complex property refactoring with multiple nested levels', async () => {
      const baselineApiView = `export interface Target { 
        name: string; 
        age: number; 
        street: string; 
        city: string; 
        zipCode: string; 
      }`;
      const currentApiView = `
        export interface Address { street: string; city: string; zipCode: string; }
        export interface Person { name: string; age: number; }
        export interface Target { 
          person: Person;
          address: Address;
        }`;
      const astContext = await createTestAstContext(baselineApiView, currentApiView);
      const diffPairs = patchInterface('Target', astContext, AssignDirection.CurrentToBaseline);
      // Current implementation doesn't support multiple partial refactorings
      // Each added property only contains some of the removed properties
      expect(diffPairs.length).toBe(7); // 5 removed + 2 added
      expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[0].location).toBe(DiffLocation.Property);
      expect(diffPairs[0].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[0].target?.name).toBe('name');
      expect(diffPairs[1].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[1].location).toBe(DiffLocation.Property);
      expect(diffPairs[1].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[1].target?.name).toBe('age');
      expect(diffPairs[2].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[2].location).toBe(DiffLocation.Property);
      expect(diffPairs[2].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[2].target?.name).toBe('street');
      expect(diffPairs[3].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[3].location).toBe(DiffLocation.Property);
      expect(diffPairs[3].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[3].target?.name).toBe('city');
      expect(diffPairs[4].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[4].location).toBe(DiffLocation.Property);
      expect(diffPairs[4].reasons).toBe(DiffReasons.Removed);
      expect(diffPairs[4].target?.name).toBe('zipCode');
      expect(diffPairs[5].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[5].location).toBe(DiffLocation.Property);
      expect(diffPairs[5].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[5].source?.name).toBe('person');
      expect(diffPairs[6].assignDirection).toBe(AssignDirection.CurrentToBaseline);
      expect(diffPairs[6].location).toBe(DiffLocation.Property);
      expect(diffPairs[6].reasons).toBe(DiffReasons.Added);
      expect(diffPairs[6].source?.name).toBe('address');
    });
  });
});
// TODO: detect enum
