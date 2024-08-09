import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { findParametersPath, findRestClientPath, getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient } from "../../xlc/apiVersion/utils";

export const getApiVersionType: IApiVersionTypeExtractor = (
    packageRoot: string
): ApiVersionType => {
    const relativeRestSrcFolder = "src/rest/";
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot, relativeRestSrcFolder, findRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, relativeRestSrcFolder, findParametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
