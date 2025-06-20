import { Project } from 'ts-morph';
import { SyntaxKind } from 'typescript';
import { describe, expect, test } from 'vitest';
import { createTestAstContext } from './utils';
import { patchInterface } from '../azure/patch/patch-detection';
import { AssignDirection, DiffLocation, DiffReasons } from '../azure/common/types';

// TODO: remove
describe('debug', () => {
  test('HLC => MC migration', async () => {
    const project = new Project();
    const sourceFile = project.createSourceFile(
      'example.ts',
      `
export interface AAA {
  func(p: number): string;
}

`,
      { overwrite: true }
    );

    const iface = sourceFile.getInterfaceOrThrow('AAA');

    // 找出所有 method signatures
    const methodSigs = iface.getMembers().filter((m) => m.getKind() === SyntaxKind.MethodSignature);

    for (const method of methodSigs) {
      const methodSig = method.asKindOrThrow(SyntaxKind.MethodSignature);

      const name = methodSig.getName();
      const params = methodSig.getParameters().map((p) => ({
        name: p.getName(),
        type: p.getTypeNodeOrThrow().getText(),
      }));
      const returnType = methodSig.getReturnTypeNodeOrThrow().getText();

      // 删除原来的 method signature
      methodSig.remove();

      // 添加箭头函数形式的 property signature
      iface.addProperty({
        name,
        type: `(${params.map((p) => `${p.name}: ${p.type}`).join(', ')}) => ${returnType}`,
      });
    }

    // 打印修改后的代码
    console.log(sourceFile.getFullText());
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
