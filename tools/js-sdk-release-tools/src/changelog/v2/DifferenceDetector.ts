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
  patchRoutes,
} from 'typescript-codegen-breaking-change-detector';
import { SDKType } from '../../common/types.js';
import { join } from 'path';
import { FunctionDeclaration, ModuleKind, Project, ScriptTarget, SourceFile, SyntaxKind } from 'ts-morph';
import { logger } from '../../utils/logger.js';
import ts from 'typescript';

const { JsxEmit } = ts;

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
  private result: DetectResult | undefined;

  constructor(
    private baselineApiViewOptions: ApiViewOptions,
    private currentApiViewOptions: ApiViewOptions
  ) {
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

  public async detect(): Promise<DetectResult> {
    await this.load();
    await this.preprocess();
    await this.detectCore();
    this.postprocess();
    return this.result!;
  }

  private postprocess() {
    this.result?.interfaces.forEach((v, k) => {
      if (k.endsWith('NextOptionalParams')) this.result?.interfaces.delete(k);
    });
    if (this.currentApiViewOptions.sdkType !== SDKType.RestLevelClient) return;
    // use Routes specific detection
    this.result?.interfaces.delete('Routes');
    const routesDiffPairs = patchRoutes(this.context!);
    this.result?.interfaces.set('Routes', routesDiffPairs);
  }

  private convertHighLevelClientToModularClientCode(code: string): string {
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
        const hasMethod = i.getMembers().filter((m) => isPropertyMethod(m.getSymbolOrThrow())).length > 0;
        if (!hasMethod) return false;
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
  }

  private async preprocess() {
    const baselineSdkType = this.baselineApiViewOptions.sdkType;
    const currentSdkType = this.currentApiViewOptions.sdkType;
    if (baselineSdkType === currentSdkType) return;
    if (baselineSdkType !== SDKType.HighLevelClient || currentSdkType !== SDKType.ModularClient) {
      logger.error(
        `Failed to preprocess baseline SDK type '${baselineSdkType}' and current SDK type '${currentSdkType}' for difference detection. Only ${SDKType.HighLevelClient} to ${SDKType.ModularClient} is supported.`
      );
      return;
    }

    const highLevelCodeInModularWay = this.convertHighLevelClientToModularClientCode(
      this.context?.baseline.getFullText()!
    );
    const generateApiView = (code: string) => {
      return `
\`\`\` ts
    ${code}
\`\`\`
    `;
    };
    const baselineApiView = generateApiView(highLevelCodeInModularWay);
    const currentApiView = generateApiView(this.context!.current.getFullText()!);
    this.context = await createAstContext(
      { apiView: baselineApiView },
      { apiView: currentApiView },
      this.tempFolder,
      true
    );
  }

  private async load() {
    this.context = await createAstContext(
      { path: this.baselineApiViewOptions.path, apiView: this.baselineApiViewOptions.apiView },
      { path: this.currentApiViewOptions.path, apiView: this.currentApiViewOptions.apiView },
      this.tempFolder,
      true
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

  // TODO: support type parameters
  private hasTypeParametersCore(name: string, kind: SyntaxKind, sourceFile: SourceFile): boolean {
    switch (kind) {
      case SyntaxKind.InterfaceDeclaration:
        return (sourceFile.getInterface(name)?.getTypeParameters().length ?? 0) > 0;
      case SyntaxKind.ClassDeclaration:
        return (sourceFile.getClass(name)?.getTypeParameters().length ?? 0) > 0;
      case SyntaxKind.TypeAliasDeclaration:
        return (sourceFile.getTypeAlias(name)?.getTypeParameters().length ?? 0) > 0;
      case SyntaxKind.FunctionDeclaration:
        return (
          (this.getFunctions(sourceFile)
            .find((d) => d.getName() === name)
            ?.getTypeParameters().length ?? 0) > 0
        );
      default:
        return false;
    }
  }

  private hasTypeParameters(name: string, kind: SyntaxKind): boolean {
    return (
      this.hasTypeParametersCore(name, kind, this.context!.baseline) ||
      this.hasTypeParametersCore(name, kind, this.context!.current)
    );
  }

  /**
   * Get the structural content of an interface for comparison, ignoring the interface name
   * This includes type parameters, extends clause, and all members
   */
  private getInterfaceStructure(interfaceName: string, sourceFile: SourceFile): string | null {
    const interfaceDecl = sourceFile.getInterface(interfaceName);
    if (!interfaceDecl) return null;

    const typeParams = interfaceDecl.getTypeParameters().map((tp) => tp.getText()).join(', ');
    const extendsClause = interfaceDecl.getExtends().map((ext) => ext.getText()).join(', ');
    const members = interfaceDecl.getMembers().map((member) => member.getText()).sort().join('\n');

    return `<${typeParams}>${extendsClause ? ` extends ${extendsClause}` : ''} { ${members} }`;
  }

  /**
   * Find generic interfaces that have the same structure but different names
   */
  private findMatchingGenericInterface(
    targetStructure: string,
    sourceFile: SourceFile,
    excludeName?: string
  ): string | null {
    const interfaces = sourceFile
      .getInterfaces()
      .filter((i) => i.getTypeParameters().length > 0 && i.getName() !== excludeName);

    for (const interfaceDecl of interfaces) {
      const structure = this.getInterfaceStructure(interfaceDecl.getName(), sourceFile);
      if (structure === targetStructure) {
        return interfaceDecl.getName();
      }
    }
    return null;
  }

  /**
   * Check if a generic interface was only renamed (structure unchanged)
   */
  private isGenericInterfaceRenamed(interfaceName: string): boolean {
    const baselineInterface = this.context!.baseline.getInterface(interfaceName);
    const currentInterface = this.context!.current.getInterface(interfaceName);

    // If interface exists in both with same name, check if it's generic
    if (baselineInterface && currentInterface) {
      return false; // Not renamed, same name exists in both
    }

    // If interface exists only in baseline, check if there's a matching structure in current
    if (baselineInterface && !currentInterface && baselineInterface.getTypeParameters().length > 0) {
      const baselineStructure = this.getInterfaceStructure(interfaceName, this.context!.baseline);
      if (baselineStructure) {
        const matchingName = this.findMatchingGenericInterface(baselineStructure, this.context!.current, interfaceName);
        return matchingName !== null;
      }
    }

    // If interface exists only in current, check if there's a matching structure in baseline
    if (!baselineInterface && currentInterface && currentInterface.getTypeParameters().length > 0) {
      const currentStructure = this.getInterfaceStructure(interfaceName, this.context!.current);
      if (currentStructure) {
        const matchingName = this.findMatchingGenericInterface(currentStructure, this.context!.baseline, interfaceName);
        return matchingName !== null;
      }
    }

    return false;
  }  

  private getFunctions(sourceFile: SourceFile): FunctionDeclaration[] {
    return sourceFile
      .getStatements()
      .filter((d) => d.getKind() === SyntaxKind.FunctionDeclaration)
      .map((d) => d.asKindOrThrow(SyntaxKind.FunctionDeclaration));
  }

  private async detectCore(): Promise<void> {
    const interfaceNames = this.getUniqueDeclarationNames((s) => s.getInterfaces().map((i) => i.getName()));
    const classNames = this.getUniqueDeclarationNames((s) => s.getClasses().map((i) => i.getName()!));
    const typeAliasNames = this.getUniqueDeclarationNames((s) => s.getTypeAliases().map((i) => i.getName()));
    const functionNames = this.getUniqueDeclarationNames((s) => this.getFunctions(s).map((d) => d.getName()!));
    const enumNames = this.getUniqueDeclarationNames((s) => s.getEnums().map((i) => i.getName()));

    // TODO: be careful about input models and output models
    const interfaceDiffPairs = interfaceNames.reduce((map, n) => {
      if (this.hasTypeParameters(n, SyntaxKind.InterfaceDeclaration) && this.isGenericInterfaceRenamed(n)) {
        // Check if this is just a rename of a generic interface with same structure
        logger.info(`Generic interface '${n}' appears to be renamed with same structure, skipping breaking change detection.`);
        return map;
      }
      const diffPairs = patchInterface(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const classDiffPairs = classNames.reduce((map, n) => {
      if (this.hasTypeParameters(n, SyntaxKind.ClassDeclaration)) {
        logger.warn(`Generic class '${n}' breaking change detection is not supported.`);
        return map;
      }
      const diffPairs = patchClass(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const typeAliasDiffPairs = typeAliasNames.reduce((map, n) => {
      if (this.hasTypeParameters(n, SyntaxKind.TypeAliasDeclaration)) {
        logger.warn(`Generic type alias '${n}' breaking change detection is not supported.`);
        return map;
      }
      const diffPairs = patchTypeAlias(n, this.context!, AssignDirection.CurrentToBaseline);
      map.set(n, diffPairs);
      return map;
    }, new Map<string, DiffPair[]>());
    const functionDiffPairs = functionNames.reduce((map, n) => {
      if (this.hasTypeParameters(n, SyntaxKind.FunctionDeclaration)) {
        logger.warn(`Generic interface '${n}' breaking change detection is not supported.`);
        return map;
      }
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

    this.result = {
      interfaces: interfaceDiffPairs,
      classes: classDiffPairs,
      typeAliases: typeAliasDiffPairs,
      functions: functionDiffPairs,
      enums: enumDiffPairs,
    };
  }
}
