import { findParametersPath, getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    let clientPattern = "src/*Context.ts";
    let typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    
    clientPattern = "src/*Client.ts";
    typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    
    clientPattern = "src/parameters.ts";
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, clientPattern, findParametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
