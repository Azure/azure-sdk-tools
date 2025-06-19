import { Project } from 'ts-morph';
import { SyntaxKind } from 'typescript';
import { describe, test } from 'vitest';

// TODO: remove
describe('debug', () => {
  test('debug', async () => {
    const project = new Project();
    const sourceFile = project.createSourceFile(
      'example.ts',
      `
      import 
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
});
