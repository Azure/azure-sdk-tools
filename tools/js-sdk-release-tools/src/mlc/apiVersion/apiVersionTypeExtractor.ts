import { ApiVersionType } from "../../common/types.js";
import { IApiVersionTypeExtractor, IModelOnlyChecker } from "../../common/interfaces.js";
import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath, getApiVersionTypeFromNpm } from "../../xlc/apiVersion/utils.js";
import { join } from "path";
import { exists } from "fs-extra";
import { logger } from "../../utils/logger.js";
import { getNpmPackageName } from "../../common/utils.js";
import { isBetaVersion } from "../../utils/version.js";

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string,
    apiVersion?: string
): Promise<ApiVersionType> => {
    if (apiVersion) {
        return isBetaVersion(apiVersion) ? ApiVersionType.Preview : ApiVersionType.Stable;
    }

    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["src/api/*Context.ts", "src/rest/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }
    const isModelOnlyPackage = await isModelOnly(packageRoot);
    if (isModelOnlyPackage) {
        const packageName = getNpmPackageName(packageRoot);
        return await getApiVersionTypeFromNpm(packageName);
    }
    
    logger.info('Failed to find api version in client, fallback to get api version type in operation\'s parameter');
    const parametersPath = join(packageRoot, "src/rest/parameters.ts");
    const typeFromOperations = getApiVersionTypeFromOperations(parametersPath);
    if (typeFromOperations !== ApiVersionType.None) {
        return typeFromOperations;
    } else {
        return ApiVersionType.Stable; // If no version found in operations, default to stable
    }
};

export const isModelOnly: IModelOnlyChecker = async (packageRoot: string): Promise<boolean> => {
    // For MLC, simply check for parameters.ts - its absence indicates a model-only service
    const parametersPath = join(packageRoot, "src/api/index.ts");
    const isParametersExists = await exists(parametersPath);
    
    if (!isParametersExists) {
        logger.warn(`No index.ts found in ${packageRoot}, this is a model-only service in Modular client`);
        return true;
    }

    logger.info(`Found index.ts in ${packageRoot}, this is not a model-only service`);
    return false;
};
