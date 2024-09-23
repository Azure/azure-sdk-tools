import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { findParametersPath, getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    let clientPattern = "src/api/*Context.ts";
    let typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;

    clientPattern = "src/rest/*Client.ts";
    typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;

    const parametersFolder = "src/rest";
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, parametersFolder, findParametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
