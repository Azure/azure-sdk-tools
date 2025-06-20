import { ModuleKind, Project, ScriptTarget, SyntaxKind } from 'ts-morph';
import { JsxEmit } from 'typescript';
import { describe, expect, test } from 'vitest';
import { createTestAstContext } from './utils';
import { patchInterface } from '../azure/patch/patch-detection';
import { AssignDirection, DiffLocation, DiffReasons } from '../azure/common/types';
import { tsconfig } from '../azure/detect-breaking-changes';
import { isPropertyMethod } from '../utils/ast-utils';

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
});
