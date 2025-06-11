import { ApiVersionType, SDKType } from "./types.js";

export interface IApiVersionTypeExtractor {
    (packageRoot: string): Promise<ApiVersionType>;
}

export interface IModelOnlyChecker {
    (packageRoot: string): Promise<boolean>;
}

export interface ICodeOwnersAndIgnoreLinkGenerator {
    (options: {
        sdkType: SDKType;
        typeSpecDirectory: string;
        skipGeneration: boolean;
    }): Promise<void>;
}
