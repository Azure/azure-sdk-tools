import { getApiVersionTypeFromOperations, getApiVersionTypeFromRestClient, tryFindRestClientPath } from "../../xlc/apiVersion/utils.js";

import { ApiVersionType } from "../../common/types.js";
import { IApiVersionTypeExtractor } from "../../common/interfaces.js";
import { join } from "path";
import pkg from 'fs-extra';
const { exists, readFile } = pkg;
import { tryGetNpmView } from "../../common/npmUtils.js";
import { getNpmPackageName } from "../../common/utils.js";
import { getVersion, isBetaVersion } from "../../utils/version.js";
import { logger } from "../../utils/logger.js";
import { parse } from "yaml"
import { iterate, MarkDownEx, parseMarkdown } from "@azure-tools/openapi-tools-common";

function extractAutorestConfig(readme: MarkDownEx) {
    let isInConfigurationSection = false;
    for (const node of iterate(readme.markDown)) {
        if (node.type === 'heading' && node.level === 2 && node.firstChild?.literal?.trim() === 'Configuration') {
            isInConfigurationSection = true;
            continue;
        }

        if (node.type === 'heading' && node.level >= 2 && node.firstChild?.literal?.trim() !== 'Configuration') {
            isInConfigurationSection = false;
            continue;
        }

        // find yaml code block
        if (isInConfigurationSection && node.type === 'code_block' &&
            node.info === 'yaml' && node.literal !== null) {
            return parse(node.literal);
        }
    }
}

async function resolveParameterPath(packageRoot: string) {
    let parametersPath = join(packageRoot, "src/parameters.ts");
    const swaggerReadmePath = join(packageRoot, "swagger/README.md");
    const hasSwaggerReadme = await exists(swaggerReadmePath);
    if (hasSwaggerReadme) {
        const autoRestContent = await readFile(swaggerReadmePath, { encoding: 'utf-8' });
        const readme = parseMarkdown(autoRestContent);
        const config = extractAutorestConfig(readme);
        const sourceFolderPath = config["source-code-folder-path"];
        if (sourceFolderPath) parametersPath = join(packageRoot, sourceFolderPath, "parameters.ts");
    }
    return parametersPath;
}

export const getApiVersionType: IApiVersionTypeExtractor = async (
    packageRoot: string
): Promise<ApiVersionType> => {
    // NOTE: when there's customized code, emitter must put generated code in root/generated folder
    const clientPatterns = ["generated/*Context.ts", "generated/*Client.ts", "src/*Context.ts", "src/*Client.ts"];
    for (const pattern of clientPatterns) {
        const typeFromClient = await getApiVersionTypeFromRestClient(packageRoot, pattern, tryFindRestClientPath);
        if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    }

    logger.info('Failed to find api version in client, fallback to get api version type in operation\'s parameter');
    const parametersPath = await resolveParameterPath(packageRoot);
    if (await exists(parametersPath)) {
        const typeFromOperations = getApiVersionTypeFromOperations(parametersPath);
        if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    }

    logger.info('No operation found, fallback to get api version type from latest version in NPM');
    const packageName = getNpmPackageName(packageRoot);
    const npmViewResult = await tryGetNpmView(packageName);
    const latestVersion = getVersion(npmViewResult, "latest");
    const isBeta = isBetaVersion(latestVersion);
    return isBeta ? ApiVersionType.Preview : ApiVersionType.Stable;
};
