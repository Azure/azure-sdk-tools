import {
  AssignDirection,
  AstContext,
  createAstContext,
  DiffPair,
  patchClass,
  patchFunction,
  patchInterface,
  patchTypeAlias,
} from 'typescript-codegen-breaking-change-detector';
import { SDKType } from '../../common/types.js';
import { join } from 'path';

export interface ApiViewOptions {
  path: string;
  sdkType: SDKType;
}

export interface DetectResult {
  interfaces: Map<string, DiffPair[]>;
  classes: Map<string, DiffPair[]>;
  typeAliases: Map<string, DiffPair[]>;
  functions: Map<string, DiffPair[]>;
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
  private preprocessFn: ((astContext: AstContext) => Promise<void>) | undefined = undefined;

  constructor(
    private baselineApiViewOptions: ApiViewOptions,
    private sourceApiViewOptions: ApiViewOptions
  ) {
    this.tempFolder = join('~/.tmp-breaking-change-detect-' + Math.random().toString(36).substring(7));
  }

  public getDetectContext(): DetectContext {
    return {
      sdkTypes: {
        target: this.baselineApiViewOptions.sdkType,
        source: this.sourceApiViewOptions.sdkType,
      },
      context: this.context!,
    };
  }

  public async detect() {
    await this.load();
    await this.preprocessFn?.(this.context!);

    if (this.baselineApiViewOptions.sdkType !== this.sourceApiViewOptions.sdkType) return this.detectAcrossSdkTypes();

    switch (this.sourceApiViewOptions.sdkType) {
      case SDKType.HighLevelClient:
      case SDKType.ModularClient:
        return await this.detectCore();
      case SDKType.RestLevelClient:
        break;
      default:
        throw new Error(`Unsupported SDK type: ${this.sourceApiViewOptions.sdkType} to detect differences.`);
    }
  }

  private async load() {
    this.context = await createAstContext(this.baselineApiViewOptions.path, this.sourceApiViewOptions.path, this.tempFolder);
  }

  public async setPreprocessFn(fn: typeof this.preprocessFn) {
    this.preprocessFn = fn;
  }

  private async detectCore(): Promise<DetectResult> {
    const interfaceNamesHasDuplicate = [
      ...this.context!.baseline.getInterfaces().map((i) => i.getName()),
      ...this.context!.current.getInterfaces().map((i) => i.getName()),
    ];
    const typeAliasNamesHasDuplicate = [
      ...this.context!.baseline.getTypeAliases().map((i) => i.getName()),
      ...this.context!.current.getTypeAliases().map((i) => i.getName()),
    ];
    const classNamesHasDuplicate = [
      ...this.context!.baseline.getClasses().map((i) => i.getName()),
      ...this.context!.current.getClasses().map((i) => i.getName()),
    ];
    const functionsHasDuplicate = [
      ...this.context!.baseline.getFunctions().map((i) => i.getName()),
      ...this.context!.current.getFunctions().map((i) => i.getName()),
    ];
    const uniquefy = (arrays: (string | undefined)[]) => {
      const arr = arrays.filter((a) => a !== undefined).map((a) => a!);
      return [...new Set(arr)];
    };
    const interfaceNames = uniquefy(interfaceNamesHasDuplicate);
    const classNames = uniquefy(classNamesHasDuplicate);
    const typeAliasNames = uniquefy(typeAliasNamesHasDuplicate);
    const functionNames = uniquefy(functionsHasDuplicate);

    // TODO: be careful about input models and output models
    const interfaceDiffPairs = interfaceNames.reduce((map, n) => {
      const diffPairs = patchInterface(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const classDiffPairs = classNames.reduce((map, n) => {
      const diffPairs = patchClass(n, this.context!, AssignDirection.CurrentToBaseline);
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

    return {
      interfaces: interfaceDiffPairs,
      classes: classDiffPairs,
      typeAliases: typeAliasDiffPairs,
      functions: functionDiffPairs,
    };
  }

  private async detectAcrossSdkTypes() {
    if (this.baselineApiViewOptions.sdkType === SDKType.HighLevelClient && this.sourceApiViewOptions.sdkType === SDKType.ModularClient) {
      // TODO
    } else {
      const message = `Not supported SDK type: ${this.baselineApiViewOptions.sdkType} -> ${this.sourceApiViewOptions.sdkType} to detect differences.`;
      throw new Error(message);
    }
    return;
  }
}
