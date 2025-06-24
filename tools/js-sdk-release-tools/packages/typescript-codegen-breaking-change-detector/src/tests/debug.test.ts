import {
  ModuleKind,
  Project,
  ScriptTarget,
  SyntaxKind,
  Type,
  Node,
  TypeParameter,
  SourceFile,
  InterfaceDeclaration,
  TypeChecker,
  VariableDeclaration,
  FunctionDeclaration,
  TypeAliasDeclaration,
  ClassDeclaration,
} from 'ts-morph';
import ts, { JsxEmit } from 'typescript';
import { describe, expect, test } from 'vitest';
import { createTestAstContext } from './utils';
import { patchClass, patchEnum, patchFunction, patchInterface, patchTypeAlias } from '../azure/patch/patch-detection';
import { AssignDirection, DiffLocation, DiffReasons } from '../azure/common/types';
import { isPropertyMethod } from '../utils/ast-utils';
import { Variable } from '@typescript-eslint/scope-manager';
import { createAstContext } from '../azure/detect-breaking-changes';

// TODO: remove
describe('debug', () => {
  test('HLC => MC migration', async () => {
    const preprocessHighLevelClientCode = (code: string) => {
      const project = new Project({
        compilerOptions: {
          jsx: JsxEmit.Preserve,
          target: ScriptTarget.ES5,
          module: ModuleKind.CommonJS,
          strict: true,
          esModuleInterop: true,
          lib: ['es2015', 'es2017', 'esnext'],
          experimentalDecorators: true,
          rootDir: '.',
        },
      });
      const sourceFile = project.createSourceFile('index.ts', code, { overwrite: true });

      sourceFile
        .getInterfaces()
        .filter((i) => {
          const isEveryMemberMethod = i.getMembers().every((m) => isPropertyMethod(m.getSymbolOrThrow()));
          return isEveryMemberMethod;
        })
        .forEach((g) => {
          const methodSigs = g.getMembers().filter((m) => m.getKind() === SyntaxKind.MethodSignature);
          g.rename(g.getName() + 'Operations');
          for (const method of methodSigs) {
            const methodSig = method.asKindOrThrow(SyntaxKind.MethodSignature);
            const name = methodSig.getName();
            const params = methodSig.getParameters().map((p) => ({
              name: p.getName(),
              type: p.getTypeNodeOrThrow().getText(),
            }));
            const returnType = methodSig.getReturnTypeNodeOrThrow().getText();
            methodSig.remove();
            g.addProperty({
              name,
              type: `(${params.map((p) => `${p.name}: ${p.type}`).join(', ')}) => ${returnType}`,
            });
          }
        });
      return sourceFile.getFullText();
    };
  });

  test('Generic Type', async () => {
    const baselineApiView = `export interface TestInterface<T> { prop: T; }`;
    const currentApiView = `export interface TestInterface<T> { prop: T; }`;

    const astContext = await createTestAstContext(baselineApiView, currentApiView);
    const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
    expect(diffPairs.length).toBe(1);
    expect(diffPairs[0].assignDirection).toBe(AssignDirection.CurrentToBaseline);
    expect(diffPairs[0].location).toBe(DiffLocation.Property);
    expect(diffPairs[0].reasons).toBe(DiffReasons.TypeChanged);
    expect(diffPairs[0].target?.name).toBe('prop');
    expect(diffPairs[0].target?.node.asKind(SyntaxKind.PropertySignature)?.getTypeNode()?.getText()).toBe('T');
    expect(diffPairs[0].source?.node.asKind(SyntaxKind.PropertySignature)?.getTypeNode()?.getText()).toBe('T');
  });

  test('Generic Type 2', async () => {
    const project = new Project({ useInMemoryFileSystem: true });
    const sourceFile = project.createSourceFile(
      'temp.ts',
      `
export interface TestInterface1<T> { prop: T; }
export interface TestInterface2<T extends number> { prop: T; }
export function testFn<T extends boolean>(): T;
export const identity = <T>(value: T): T => value;
`
    );

    function instantiateSourceFile(node: Node): string {
      const generateStatement = (node: Node) => {
        const kind = node.getKind();
        switch (kind) {
          case SyntaxKind.InterfaceDeclaration: {
            const interfaceDeclaration = node.asKindOrThrow(SyntaxKind.InterfaceDeclaration);
            const name = interfaceDeclaration.getName();
            const typeParameters = interfaceDeclaration.getTypeParameters();
            const statement = `type ______TEMP______${name} = ${name}<${typeParameters.map((p) => 'any').join(', ')}>;`;
            return statement;
          }
          case SyntaxKind.TypeAliasDeclaration: {
            const typeAlias = node.asKindOrThrow(SyntaxKind.TypeAliasDeclaration);
            const name = typeAlias.getName();
            const typeParameters = typeAlias.getTypeParameters();
            const statement = `type ______TEMP______${name} = ${name}<${typeParameters.map((p) => 'any').join(', ')}>;\n`;
            return statement;
          }
          case SyntaxKind.VariableDeclaration: {
            const variableDeclaration = node.asKind(SyntaxKind.VariableDeclaration);
            if (!variableDeclaration) return undefined;
            const name = variableDeclaration.getName();
            const typeParameters = variableDeclaration
              .getInitializer()
              ?.asKind(SyntaxKind.ArrowFunction)
              ?.getTypeParameters();
            if (!typeParameters) return undefined;
            const statement = `const ______TEMP______${name} = ${name}<${typeParameters.map((p) => 'any').join(', ')}>;\n`;
            return statement;
          }
          case SyntaxKind.FunctionDeclaration: {
            const functionDeclaration = node.asKindOrThrow(SyntaxKind.FunctionDeclaration);
            const name = functionDeclaration.getName();
            const typeParameters = functionDeclaration.getTypeParameters();
            const statement = `const ______TEMP______${name} = ${name}<${typeParameters.map((p) => 'any').join(', ')}>;\n`;
            return statement;
          }
          case SyntaxKind.ClassDeclaration: {
            const classDeclaration = node.asKindOrThrow(SyntaxKind.ClassDeclaration);
            const name = classDeclaration.getName();
            const typeParameters = classDeclaration.getTypeParameters();
            const statement = `type ______TEMP______${name} = ${name}<${typeParameters.map((p) => 'any').join(', ')}>;\n`;
            return statement;
          }
          default:
            return undefined;
        }
      };

      const sourceFile = node.getSourceFile();
      const statement = generateStatement(node);
      if (statement) sourceFile.addStatements([statement]);
      return sourceFile.getFullText();
    }

    const getFunction = (name: string, sourceFile: SourceFile) => {
      return sourceFile
        .getStatements()
        .filter(
          (d) =>
            d.getKind() === SyntaxKind.FunctionDeclaration &&
            name === d.asKindOrThrow(SyntaxKind.FunctionDeclaration).getName()
        )
        .map((d) => d.asKindOrThrow(SyntaxKind.FunctionDeclaration))[0];
    };

    const iface1 = sourceFile.getInterfaceOrThrow('TestInterface1');
    const iface2 = sourceFile.getInterfaceOrThrow('TestInterface2');
    const identity = sourceFile.getVariableDeclarationOrThrow('identity');
    const testFn = getFunction('testFn', sourceFile);

    const inst1 = instantiateSourceFile(iface1);
    const inst2 = instantiateSourceFile(iface2);
    const inst3 = instantiateSourceFile(identity);
    const inst4 = instantiateSourceFile(testFn);

    // console.log('iface1<string> assignable to iface2<number>?', inst1!.isAssignableTo(inst2!)); // false
    // console.log('identity<string> return type assignable to string?', inst3!.isAssignableTo(typeChecker.string)); // true
    // console.log('testFn<boolean>() returns boolean?', inst4!.isAssignableTo(typeChecker.getBooleanType())); // true
  });

  test('generic type', async () => {
    const project = new Project({ useInMemoryFileSystem: true });
    const sourceFile = project.createSourceFile(
      'temp.ts',
      `
export interface TestInterface1<T> { prop: T; }
export interface TestInterface2<T> { prop: T; }
export function testFn<T extends boolean>(): T;
export const identity = <T>(value: T): T => value;
export type TestTypeAlias<T> = { prop: T };
export class TestClass<T> {
  prop: T;
  constructor(prop: T) {
    this.prop = prop;
  }
}
`
    );

    // 处理每个文件
    {
      let modified = false;

      const nodesWithTypeParams = [
        ...sourceFile.getInterfaces(),
        ...sourceFile.getClasses(),
        ...sourceFile.getTypeAliases(),
        ...sourceFile.getFunctions(),
        ...sourceFile.getVariableStatements().flatMap((v) =>
          v.getDeclarations().flatMap((decl) => {
            const initializer = decl.getInitializer();
            if (initializer?.asKind?.(ts.SyntaxKind.ArrowFunction)) {
              return [initializer];
            }
            return [];
          })
        ),
      ];

      for (const node of nodesWithTypeParams) {
        // TODO: handle arrow function
        const typeParams = (
          node as InterfaceDeclaration | ClassDeclaration | TypeAliasDeclaration | FunctionDeclaration
        ).getTypeParameters();
        for (const typeParam of typeParams) {
          if (!typeParam.getDefault()) {
            typeParam.setDefault('any');
            modified = true;
          }
        }
      }

      //   if (modified) {
      // console.log("Modified:", sourceFile.getFilePath());
      // sourceFile.saveSync();
      console.log('Modified content:', sourceFile.getFullText());
      //   }

      const ass = sourceFile
        .getInterfaceOrThrow('TestInterface1')
        .getType()
        .isAssignableTo(sourceFile.getInterfaceOrThrow('TestInterface2').getType()); // false
      console.log('TestInterface1 is assignable to TestInterface2:', ass);
    }
  });

  test('e2e', async () => {
    /// RLC
    // const baselinePath =
    //   'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/iot-device-update-rest/review/iot-device-update.api.md';
    // const currentPath =
    //   'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/iot-device-update-rest/review/iot-device-update.api copy.md';

    /// HLC
    //  const baselinePath =
    //       'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/arm-deviceupdate/review/arm-deviceupdate.api.md';
    //     const currentPath =
    //       'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/deviceupdate/arm-deviceupdate/review/arm-deviceupdate.api copy.md';

    /// MC
    //  const baselinePath =
    //       'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/mongocluster/arm-mongocluster/review/arm-mongocluster.api.md';
    //     const currentPath =
    //       'C:/Users/wanl/workspace/azure-sdk-for-js/sdk/mongocluster/arm-mongocluster/review/arm-mongocluster.api copy.md';

    /// HLC -> MC
    const baselinePath = 'C:/Users/wanl/Downloads/hlc-mc/arm-chaos-hlc.api.md';
    const currentPath = 'C:/Users/wanl/Downloads/hlc-mc/arm-chaos-mc.api.md';

    const astContext = await createAstContext(
      { path: baselinePath },
      { path: currentPath },
      '.tmp-breaking-change-detect',
      true
    );
    const resInterfaces = astContext.baseline.getInterfaces().map((i) => {
      const diffPairs = patchInterface(i.getName(), astContext, AssignDirection.CurrentToBaseline);
      return { name: i.getName(), pairs: diffPairs };
    });
    const resFunctions = astContext.baseline.getFunctions().map((f) => {
      const diffPairs = patchFunction(f.getName()!, astContext);
      return { name: f.getName(), pairs: diffPairs };
    });
    const resTypeAliases = astContext.baseline.getTypeAliases().map((t) => {
      const diffPairs = patchTypeAlias(t.getName()!, astContext, AssignDirection.CurrentToBaseline);
      return { name: t.getName(), pairs: diffPairs };
    });
    const resEnums = astContext.baseline.getEnums().map((e) => {
      const diffPairs = patchEnum(e.getName(), astContext, AssignDirection.CurrentToBaseline);
      return { name: e.getName(), pairs: diffPairs };
    });
    const resClasses = astContext.baseline.getClasses().map((c) => {
      const diffPairs = patchClass(c.getName()!, astContext, AssignDirection.CurrentToBaseline);
      return { name: c.getName(), pairs: diffPairs };
    });

    let count = 0;
    let names: string[] = [];
    [...resInterfaces, ...resFunctions, ...resTypeAliases, ...resEnums, ...resClasses].forEach((r) => {
      if (r.pairs.length === 0) return;
      count++;
      names.push(r.name!);
      console.log('🚀 ~ e2e ~ res:', r.name);
      r.pairs.forEach((pair) => {
        console.log('🚀 ~ e2e ~ pair:', pair);
      });
    });
    console.log('🚀 ~ e2e ~ count:', count, names);
  });
});
