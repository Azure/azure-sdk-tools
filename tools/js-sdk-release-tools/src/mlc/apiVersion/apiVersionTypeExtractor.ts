import { ApiVersionType } from "../../common/types.js";
import { IApiVersionTypeExtractor } from "../../common/interfaces.js";
import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils.js";
import { join } from "path";
import { exists } from "fs-extra";
import { logger } from "../../utils/logger.js";
import { getNpmPackageName } from "../../common/utils.js";
import { tryGetNpmView } from "../../common/npmUtils.js";
import { getVersion, isBetaVersion } from "../../utils/version.js";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["src/api/*Context.ts", "src/rest/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }

    logger.info('Failed to find api version in client, fallback to get api version type in operation\'s parameter');
    const parametersPath = join(packageRoot, "src/rest/parameters.ts");
    if (await exists(parametersPath)) {
        const typeFromOperations = getApiVersionTypeFromOperations(parametersPath);
        if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
        return ApiVersionType.Stable;
    } 

    logger.info('No operation found, fallback to get api version type from latest version in NPM');
    const packageName = getNpmPackageName(packageRoot);
    const npmViewResult = await tryGetNpmView(packageName);
    const latestVersion = getVersion(npmViewResult, "latest");
    const isBeta = isBetaVersion(latestVersion);
    return isBeta ? ApiVersionType.Preview : ApiVersionType.Stable;
};
