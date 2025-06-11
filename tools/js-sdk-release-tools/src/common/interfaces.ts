import { ApiVersionType, SDKType } from "./types.js";

export interface IApiVersionTypeExtractor {
    (packageRoot: string): Promise<ApiVersionType>;
}

export interface IModelOnlyChecker {
    (packageRoot: string): Promise<boolean>;
}

export interface ICodeOwnersAndIgnoreLinkGenerator {
    (
        sdkType: SDKType,
        options: {
            typespecProject?: string;
            typeSpecDirectory: string;
            sdkRepo: string;
            skipGeneration: boolean;
        },
    ): Promise<void>;
}
