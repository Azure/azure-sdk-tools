import {
  AssignDirection,
  AstContext,
  createAstContext,
  DiffPair,
  patchClass,
  patchFunction,
  patchInterface,
  patchTypeAlias,
  patchEnum,
  isPropertyMethod,
  extractCodeFromApiView,
} from 'typescript-codegen-breaking-change-detector';
import { SDKType } from '../../common/types.js';
import { join } from 'path';
import { ModuleKind, Project, ScriptTarget, SourceFile, SyntaxKind } from 'ts-morph';
import { logger } from '../../utils/logger.js';
import { JsxEmit } from 'typescript';

export interface ApiViewOptions {
  path?: string;
  apiView?: string;
  sdkType: SDKType;
}

export interface DetectResult {
  interfaces: Map<string, DiffPair[]>;
  classes: Map<string, DiffPair[]>;
  typeAliases: Map<string, DiffPair[]>;
  functions: Map<string, DiffPair[]>;
  enums: Map<string, DiffPair[]>;
}

export interface DetectContext {
  sdkTypes: {
    target: SDKType;
    source: SDKType;
  };
  context: AstContext;
}

export class DifferenceDetector {
  private tempFolder: string;
  private context: AstContext | undefined;

  constructor(
    private baselineApiViewOptions: ApiViewOptions,
    private currentApiViewOptions: ApiViewOptions
  ) {
    console.log('🚀 ~ DifferenceDetector ~ currentApiViewOptions:', currentApiViewOptions);
    console.log('🚀 ~ DifferenceDetector ~ baselineApiViewOptions:', baselineApiViewOptions);
    this.tempFolder = join('~/.tmp-breaking-change-detect-' + Math.random().toString(36).substring(7));
  }

  public getDetectContext(): DetectContext {
    return {
      sdkTypes: {
        target: this.baselineApiViewOptions.sdkType,
        source: this.currentApiViewOptions.sdkType,
      },
      context: this.context!,
    };
  }

  public async detect() {
    await this.load();
    await this.preprocess();

    switch (this.currentApiViewOptions.sdkType) {
      case SDKType.HighLevelClient:
      case SDKType.ModularClient:
      // TODO: RLC has it special logic to handle Routes
      case SDKType.RestLevelClient:
        return await this.detectCore();
      default:
        throw new Error(`Unsupported SDK type: ${this.currentApiViewOptions.sdkType} to detect differences.`);
    }
  }

  convertHighLevelClientToModularClientCode(code: string): string {
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

    console.log("🚀 ~ DifferenceDetector ~ convertHighLevelClientToModularClientCode ~ code:", code)
    console.log("🚀 ~ DifferenceDetector ~ convertHighLevelClientToModularClientCode ~ sourceFile -- before:", sourceFile.getFullText())
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
    console.log("🚀 ~ DifferenceDetector ~ convertHighLevelClientToModularClientCode ~ sourceFile -- after:", sourceFile.getFullText())
    return sourceFile.getFullText();
  }

  private async preprocess() {
    const baselineSdkType = this.baselineApiViewOptions.sdkType;
    const currentSdkType = this.currentApiViewOptions.sdkType;
    if (baselineSdkType === currentSdkType) return;
    if (baselineSdkType !== SDKType.HighLevelClient || currentSdkType !== SDKType.ModularClient) {
      logger.warn(
        `Failed to preprocess baseline SDK type '${baselineSdkType}' and current SDK type '${currentSdkType}' for difference detection. Only ${SDKType.HighLevelClient} to ${SDKType.ModularClient} is supported.`
      );
      return;
    }

    const highLevelCodeInModularWay = this.convertHighLevelClientToModularClientCode(this.context?.baseline.getFullText()!);
    const generateApiView = (code: string) => {
      return `
\`\`\` ts
    ${code}
\`\`\`
    `;
    };
    const baselineApiView = generateApiView(highLevelCodeInModularWay);
    console.log('🚀 ~ DifferenceDetector ~ preprocess ~ baselineApiView:', baselineApiView);
    const currentApiView = generateApiView(this.context!.current.getFullText()!);
    console.log('🚀 ~ DifferenceDetector ~ preprocess ~ currentApiView:', currentApiView);
    this.context = await createAstContext({ apiView: baselineApiView }, { apiView: currentApiView }, this.tempFolder);
  }

  private async load() {
    this.context = await createAstContext(
      { path: this.baselineApiViewOptions.path, apiView: this.baselineApiViewOptions.apiView },
      { path: this.currentApiViewOptions.path, apiView: this.currentApiViewOptions.apiView },
      this.tempFolder
    );
  }

  private getUniqueDeclarationNames(getDeclarationFn: (sourceFile: SourceFile) => string[]) {
    const uniquefy = (arrays: (string | undefined)[]) => {
      const arr = arrays.filter((a) => a !== undefined).map((a) => a!);
      return [...new Set(arr)];
    };
    const namesFromBoth = [...getDeclarationFn(this.context!.baseline), ...getDeclarationFn(this.context!.current)];
    return uniquefy(namesFromBoth);
  }

  private async detectCore(): Promise<DetectResult> {
    const getFunctionNames = (sourceFile: SourceFile) => {
      return sourceFile
        .getStatements()
        .filter((d) => d.getKind() === SyntaxKind.FunctionDeclaration)
        .map((d) => d.asKindOrThrow(SyntaxKind.FunctionDeclaration).getName()!);
    };

    const interfaceNames = this.getUniqueDeclarationNames((s) => s.getInterfaces().map((i) => i.getName()));
    const classNames = this.getUniqueDeclarationNames((s) => s.getClasses().map((i) => i.getName()!));
    const typeAliasNames = this.getUniqueDeclarationNames((s) => s.getTypeAliases().map((i) => i.getName()));
    const functionNames = this.getUniqueDeclarationNames((s) => getFunctionNames(s));
    const enumNames = this.getUniqueDeclarationNames((s) => s.getEnums().map((i) => i.getName()));

    console.log('🚀 ~ DifferenceDetector ~ detectCore ~ interfaceNames:', interfaceNames);
    console.log('🚀 ~ DifferenceDetector ~ detectCore ~ classNames:', classNames);
    console.log('🚀 ~ DifferenceDetector ~ detectCore ~ typeAliasNames:', typeAliasNames);
    console.log('🚀 ~ DifferenceDetector ~ detectCore ~ functionNames:', functionNames);

    // TODO: be careful about input models and output models
    const interfaceDiffPairs = interfaceNames.reduce((map, n) => {
      const diffPairs = patchInterface(n, this.context!, AssignDirection.CurrentToBaseline);
      console.log(
        '🚀 ~ DifferenceDetector ~ interfaceDiffPairs ~ this.context.baseline:',
        this.context?.baseline.getText()
      );
      console.log(
        '🚀 ~ DifferenceDetector ~ interfaceDiffPairs ~ this.context.current:',
        this.context?.current.getText()
      );
      console.log('🚀 ~ DifferenceDetector ~ interfaceDiffPairs ~ diffPairs:', diffPairs);
      console.log(
        '🚀 ~ DifferenceDetector ~ interfaceDiffPairs ~ diffPairs baseline:',
        diffPairs[0].target?.node.getText()
      );
      console.log(
        '🚀 ~ DifferenceDetector ~ interfaceDiffPairs ~ diffPairs current:',
        diffPairs[0].source?.node.getText()
      );

      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const classDiffPairs = classNames.reduce((map, n) => {
      const diffPairs = patchClass(n, this.context!, AssignDirection.CurrentToBaseline);
      console.log('🚀 ~ DifferenceDetector ~ classDiffPairs ~ diffPairs for class:', n, diffPairs);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const typeAliasDiffPairs = typeAliasNames.reduce((map, n) => {
      const diffPairs = patchTypeAlias(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const functionDiffPairs = functionNames.reduce((map, n) => {
      // TODO: add assign direction
      const diffPairs = patchFunction(n, this.context!);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const enumDiffPairs = enumNames.reduce((map, n) => {
      const diffPairs = patchEnum(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());

    return {
      interfaces: interfaceDiffPairs,
      classes: classDiffPairs,
      typeAliases: typeAliasDiffPairs,
      functions: functionDiffPairs,
      enums: enumDiffPairs,
    };
  }
}
