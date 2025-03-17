import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { join } from "path";
import { exists } from "fs-extra";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["generated/*Context.ts", "generated/*Client.ts", "src/*Context.ts", "src/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }
    
    const parametersPath = join(packageRoot, "src/parameters.ts");
    if (!(await exists(parametersPath))) {
        throw new Error(`Failed to find parameters file '${parametersPath}'.`);
    }
    const typeFromOperations = getApiVersionTypeFromOperations(parametersPath);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
