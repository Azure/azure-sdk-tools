import { findParametersPath, getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["generated/*Context.ts", "generated/*Client.ts", "src/*Context.ts", "src/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }
    
    const parametersFolder = "src/";
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, parametersFolder, findParametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
