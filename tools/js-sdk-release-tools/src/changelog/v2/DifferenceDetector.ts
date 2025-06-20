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
} from 'typescript-codegen-breaking-change-detector';
import { SDKType } from '../../common/types.js';
import { join } from 'path';
import { SourceFile, SyntaxKind } from 'ts-morph';
import { logger } from '../../utils/logger.js';

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
    this.preprocess();

    if (this.baselineApiViewOptions.sdkType !== this.currentApiViewOptions.sdkType) return this.detectAcrossSdkTypes();

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

  private preprocess() {
    const baselineSdkType = this.baselineApiViewOptions.sdkType;
    const currentSdkType = this.currentApiViewOptions.sdkType;
    if (baselineSdkType === currentSdkType) return;
    if (baselineSdkType !== SDKType.HighLevelClient || currentSdkType !== SDKType.ModularClient) {
      logger.warn(
        `Failed to preprocess baseline SDK type '${baselineSdkType}' and current SDK type '${currentSdkType}' for difference detection. Only ${SDKType.HighLevelClient} to ${SDKType.ModularClient} is supported.`
      );
      return;
    }
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

  private async detectAcrossSdkTypes(): Promise<DetectResult> {
    if (
      this.baselineApiViewOptions.sdkType === SDKType.HighLevelClient &&
      this.currentApiViewOptions.sdkType === SDKType.ModularClient
    ) {
      // TODO
    } else {
      const message = `Not supported SDK type: ${this.baselineApiViewOptions.sdkType} -> ${this.currentApiViewOptions.sdkType} to detect differences.`;
      throw new Error(message);
    }
    // TODO
    return {
      interfaces: new Map<string, DiffPair[]>(),
      classes: new Map<string, DiffPair[]>(),
      typeAliases: new Map<string, DiffPair[]>(),
      functions: new Map<string, DiffPair[]>(),
      enums: new Map<string, DiffPair[]>(),
    };
  }
}
