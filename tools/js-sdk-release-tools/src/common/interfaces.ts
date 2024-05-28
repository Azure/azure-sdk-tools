import { ApiVersionType } from "./types";

export interface IApiVersionTypeExtractor {
    (packageRoot: string): ApiVersionType;
}