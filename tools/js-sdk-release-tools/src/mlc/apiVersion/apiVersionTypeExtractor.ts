import { ApiVersionType } from "../../common/types.js";
import { IApiVersionTypeExtractor, IModelOnlyChecker } from "../../common/interfaces.js";
import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath, getApiVersionTypeFromNpm } from "../../xlc/apiVersion/utils.js";
import { join } from "path";
import { exists } from "fs-extra";
import { logger } from "../../utils/logger.js";
import { getNpmPackageName } from "../../common/utils.js";
import { isBetaVersion } from "../../utils/version.js";
import { checkDirectoryExistsInGithub } from "../../common/npmUtils.js";
import { tryGetNpmView } from "../../common/npmUtils.js";
import { getLatestStableVersion } from "../../utils/version.js";

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
    // Check locally for parameters.ts
    const parametersPath = join(packageRoot, "src/rest/parameters.ts");
    const isParametersExists = await exists(parametersPath);

    if (!isParametersExists) {
        // Get npm view to find the latest stable version
        const packageName = getNpmPackageName(packageRoot);
        const npmViewResult = await tryGetNpmView(packageName);
        if (!npmViewResult) {
            logger.warn(`No npm package found for ${packageName}, cannot check GitHub directory`);
            return true;
        }

        const stableVersion = getLatestStableVersion(npmViewResult);
        if (!stableVersion) {
            logger.warn(`No stable version found for ${packageName}, cannot check GitHub directory`);
            return true;
        }

        // Check if src/api exists in the GitHub repository
        const hasOperationsInGithub = await checkDirectoryExistsInGithub(
            packageRoot,
            "src/api",
            packageName,
            stableVersion
        );

        if (!hasOperationsInGithub) {
            logger.warn(`No parameters.ts found in ${packageRoot} and no src/api directory found in GitHub for ${packageName}, this is a model-only service`);
            return true;
        }
    }

    logger.info(`Found parameters.ts locally or src/api in GitHub for ${packageRoot}, this is not a model-only service`);
    return false;
};
