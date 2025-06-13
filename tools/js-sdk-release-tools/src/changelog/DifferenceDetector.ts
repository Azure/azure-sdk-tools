import {
    AssignDirection,
    AstContext,
    createAstContext,
    DiffPair,
    patchClass,
    patchInterface,
    patchTypeAlias,
} from "typescript-codegen-breaking-change-detector";
import { SDKType } from "../common/types.js";
import { join } from "path";

export interface ApiViewOptions {
    path: string;
    sdkType: SDKType;
}

export class DifferenceDetector {
    private tempFolder: string;
    private context: AstContext | undefined;
    private preprocessFn: ((astContext: AstContext) => {}) | undefined =
        undefined;

    constructor(
        private baseline: ApiViewOptions,
        private current: ApiViewOptions,
    ) {
        this.tempFolder = join(
            "~/.tmp-breaking-change-detect-" +
                Math.random().toString(36).substring(7),
        );
    }

    public async detect() {
        await this.load();
        await this.preprocess();

        if (this.baseline.sdkType !== this.current.sdkType)
            return this.detectAcrossSdkTypes();

        switch (this.current.sdkType) {
            case SDKType.HighLevelClient:
            case SDKType.ModularClient:
                return await this.detectCore();
            case SDKType.RestLevelClient:
                break;
            default:
                throw new Error(
                    `Unsupported SDK type: ${this.current.sdkType} to detect differences.`,
                );
        }
    }

    private async load() {
        this.context = await createAstContext(
            this.baseline.path,
            this.current.path,
            this.tempFolder,
        );
    }

    public async setPreprocessFn(fn: typeof this.preprocessFn) {
        this.preprocessFn = fn;
    }

    private async detectCore(): Promise<DiffPair[]> {
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
        const interfaceDiffPairs = interfaceNames.map((n) =>
            patchInterface(n, this.context!, AssignDirection.CurrentToBaseline),
        );
        const classDiffPairs = classNames.map((n) =>
            patchClass(n, this.context!, AssignDirection.CurrentToBaseline),
        );
        const typeAliasDiffPairs = typeAliasNames.map((n) =>
            patchTypeAlias(n, this.context!, AssignDirection.CurrentToBaseline),
        );
        const functionDiffPairs = functionNames.map((n) =>
            patchInterface(n, this.context!, AssignDirection.CurrentToBaseline),
        );
        return [
            // TODO
        ];
    }

    private async detectAcrossSdkTypes() {
        if (
            this.baseline.sdkType === SDKType.HighLevelClient &&
            this.current.sdkType === SDKType.ModularClient
        ) {
            // TODO
        } else {
            const message = `Not supported SDK type: ${this.baseline.sdkType} -> ${this.current.sdkType} to detect differences.`;
            throw new Error(message);
        }
        return;
    }
}
