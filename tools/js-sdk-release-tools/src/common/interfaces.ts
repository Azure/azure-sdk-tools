import { ApiVersionType } from "./types.js";

export interface IApiVersionTypeExtractor {
    (packageRoot: string): Promise<ApiVersionType>;
}