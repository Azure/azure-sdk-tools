import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { join } from "path";
import { exists } from "fs-extra";
import { tryGetNpmView } from "../../common/npmUtils";
import { getNpmPackageName } from "../../common/utils";
import { getVersion, isBetaVersion } from "../../utils/version";
import { logger } from "../../utils/logger";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["generated/*Context.ts", "generated/*Client.ts", "src/*Context.ts", "src/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }
    
    logger.info('No client found, fallback to get api version type in operation\'s parameter');
    const parametersFolder = ["src/", "generated/", "src/generated"];
    for (const folder of parametersFolder) {
        const typeFromOperations = getApiVersionTypeFromOperations(packageRoot, folder, findParametersPath);
        if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    }

    logger.info('No operation found, fallback to get api version type from latest version in NPM');
    const packageName = getNpmPackageName(packageRoot);
    const npmViewResult = await tryGetNpmView(packageName);
    const latestVersion = getVersion(npmViewResult, "latest");
    const isBeta = isBetaVersion(latestVersion);
    return isBeta ? ApiVersionType.Preview : ApiVersionType.Stable;
};
