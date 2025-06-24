import { ModuleKind, Project, ScriptTarget, SyntaxKind, Type, Node, TypeParameter, SourceFile } from 'ts-morph';
import { JsxEmit } from 'typescript';
import { describe, expect, test } from 'vitest';
import { createTestAstContext } from './utils';
import { patchInterface } from '../azure/patch/patch-detection';
import { AssignDirection, DiffLocation, DiffReasons } from '../azure/common/types';
import { isPropertyMethod } from '../utils/ast-utils';
import { Variable } from '@typescript-eslint/scope-manager';

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

    const processed = preprocessHighLevelClientCode(
      `export interface TestInterface {get(): void; set(s: string): XXX}`
    );
    console.log('🚀 ~ processed:', processed);
  });

  test('Generic Type', async () => {
    const baselineApiView = `export interface TestInterface<T> { prop: T; }`;
    const currentApiView = `export interface TestInterface<T> { prop: T; }`;

    const astContext = await createTestAstContext(baselineApiView, currentApiView);
    const diffPairs = patchInterface('TestInterface', astContext, AssignDirection.CurrentToBaseline);
    console.log('🚀 ~ test ~ diffPairs:', diffPairs);
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
            typeParameters[0].
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
      console.log('🚀 ~ instantiateSourceFile ~ sourceFile.getFullText():', sourceFile.getFullText());
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
});
