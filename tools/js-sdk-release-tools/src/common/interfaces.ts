import { ApiVersionType, SDKType } from "./types.js";

export interface IApiVersionTypeExtractor {
    (packageRoot: string): Promise<ApiVersionType>;
}

export interface IModelOnlyChecker {
    (packageRoot: string): Promise<boolean>;
}