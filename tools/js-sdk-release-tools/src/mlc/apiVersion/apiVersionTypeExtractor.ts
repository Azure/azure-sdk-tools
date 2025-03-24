import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";
import { join } from "path";
import { exists } from "fs-extra";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    let clientPattern = "src/api/*Context.ts";
    let typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;

    clientPattern = "src/rest/*Client.ts";
    typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, clientPattern, tryFindRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;

    const parametersPath = join(packageRoot, "src/rest/parameters.ts");
    if (!(await exists(parametersPath))) {
        throw new Error(`Failed to find parameters file '${parametersPath}'.`);
    }
    const typeFromOperations = getApiVersionTypeFromOperations(parametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
