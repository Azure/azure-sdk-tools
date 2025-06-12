import { getTsSourceFile } from '../../common/utils.js';
import { ApiVersionType } from '../../common/types.js';
import path, { basename } from 'node:path';
import shell from 'shelljs';
import { FunctionDeclaration, SourceFile, SyntaxKind } from 'ts-morph';
import { logger } from '../../utils/logger.js';
import { promises as fs } from 'node:fs';
import { exists } from 'fs-extra';
import { getNpmPackageName } from "../../common/utils.js";
import { tryGetNpmView } from "../../common/npmUtils.js";
import { getVersion, isBetaVersion } from "../../utils/version.js";

import unixify from 'unixify';

function tryFindVersionInFunctionBody(func: FunctionDeclaration): string | undefined {
    const apiVersionStatements = func.getStatements().filter((s) => s.getText().includes('options.apiVersion'));
    if (apiVersionStatements.length === 0) {
        return undefined;
    }
    const text = apiVersionStatements[apiVersionStatements.length - 1].getText();
    return extractApiVersionFromText(text);
}

function tryFindFunctionWithApiVersion(clientPath: string, functionName: string): FunctionDeclaration | undefined {
    const sourceFile = getTsSourceFile(clientPath);
    const createClientFunction = sourceFile?.getFunction(functionName);
    return createClientFunction;
}

const extractApiVersionFromText = (text: string): string | undefined => {
    const begin = text.indexOf('"');
    const end = text.lastIndexOf('"');
    return text.substring(begin + 1, end);
};

const tryFindApiVersionInRestClientV1 = (clientPath: string): string | undefined => {
    const createClientFunction = tryFindFunctionWithApiVersion(clientPath, 'createClient');
    if (!createClientFunction) return undefined;
    return tryFindVersionInFunctionBody(createClientFunction);
};

// new way in @autorest/typespec-ts emitter to set up api-version
const tryFindApiVersionInRestClientV2 = (clientPath: string): string | undefined => {
    const createClientFunction = tryFindFunctionWithApiVersion(clientPath, 'createClient');
    if (!createClientFunction) return undefined;
    let apiVersion: string | undefined = undefined;
    const bindingParameters = createClientFunction
        .getParameters()
        .filter((p) => p.getNameNode().getKind() === SyntaxKind.ObjectBindingPattern);
    if (bindingParameters.length !== 1) return undefined;
    const bindingPatterns = bindingParameters[0].getNameNode().asKind(SyntaxKind.ObjectBindingPattern);
    if (!bindingPatterns) return undefined;
    bindingPatterns
        .getElements()
        .filter((e) => e.getName() === 'apiVersion')
        .map((e) => {
            const text = e.getInitializer()?.getText();
            if (!text) return;
            apiVersion = extractApiVersionFromText(text);
        });
    return apiVersion;
};

// another new way in @autorest/typespec-ts emitter to set up api-version
const tryFindApiVersionInRestClientV3 = (clientPath: string): string | undefined => {
    const suffix = basename(clientPath).replace('Context.ts', '');
    const functionName = `create${suffix[0].toUpperCase()}${suffix.slice(1)}`;
    const createClientFunction = tryFindFunctionWithApiVersion(clientPath, functionName);
    if (!createClientFunction) return undefined;
    return tryFindVersionInFunctionBody(createClientFunction);
};

const findApiVersionsInOperations = (sourceFile: SourceFile | undefined): Array<string> | undefined => {
    const interfaces = sourceFile?.getInterfaces();
    const interfacesWithApiVersion = interfaces?.filter((itf) => itf.getProperty('"api-version"'));
    const apiVersions = interfacesWithApiVersion?.map((itf) => {
        const property = itf.getMembers().filter((m) => {
            const defaultValue = m.getChildrenOfKind(SyntaxKind.StringLiteral)[0];
            return defaultValue && defaultValue.getText() === '"api-version"';
        })[0];
        const literals = property.getChildrenOfKind(SyntaxKind.LiteralType);
        const apiVersion = literals.length > 0 ? literals[0].getText() : undefined;
        return apiVersion;
    });
    return apiVersions?.filter((v) => v !== undefined);
};

// workaround for createClient function changes it's way to setup api-version
export const tryFindApiVersionInRestClient = (clientPath: string): string | undefined => {
    const version3 = tryFindApiVersionInRestClientV3(clientPath);
    if (version3) return version3;
    const version2 = tryFindApiVersionInRestClientV2(clientPath);
    if (version2) return version2;
    const version1 = tryFindApiVersionInRestClientV1(clientPath);
    return version1;
};

export const tryFindRestClientPath = async (
    packageRoot: string,
    clientPattern: string
): Promise<string | undefined> => {
    const pattern = unixify(path.join(packageRoot, clientPattern));
    const clientFiles = await findFilesByPattern(pattern);
    if (clientFiles.length !== 1) {
        logger.warn(`Failed to find extactly one REST client in pattern '${pattern}', got '${clientFiles}'.`);
        return undefined;
    }
    const filePath = clientFiles[0];
    if (!(await exists(filePath))) {
        logger.warn(`Client file '${filePath}' does not exist.`);
        return undefined;
    }
    return clientFiles[0];
};

export const getApiVersionTypeFromRestClient = async (
    packageRoot: string,
    clientPattern: string,
    findRestClientPath: (packageRoot: string, clientPattern: string) => Promise<string | undefined>
): Promise<ApiVersionType> => {
    const clientPath = await findRestClientPath(packageRoot, clientPattern);
    if (!clientPath) return ApiVersionType.None;
    const apiVersion = tryFindApiVersionInRestClient(clientPath);
    if (apiVersion && apiVersion.indexOf('-preview') >= 0) return ApiVersionType.Preview;
    if (apiVersion && apiVersion.indexOf('-preview') < 0) return ApiVersionType.Stable;
    return ApiVersionType.None;
};

export const getApiVersionTypeFromOperations = (parametersPath: string): ApiVersionType => {
    const sourceFile = getTsSourceFile(parametersPath);
    const apiVersions = findApiVersionsInOperations(sourceFile);
    if (!apiVersions) return ApiVersionType.None;
    const previewVersions = apiVersions.filter((v) => v.indexOf('-preview') >= 0);
    return previewVersions.length > 0 ? ApiVersionType.Preview : ApiVersionType.Stable;
};

export const getApiVersionTypeFromNpm = async (packageRoot: string): Promise<ApiVersionType> => {
    logger.info('Fallback to get api version type from latest version in NPM');
    const packageName = getNpmPackageName(packageRoot);
    const npmViewResult = await tryGetNpmView(packageName);
    const latestVersion = getVersion(npmViewResult, "latest");
    const isBeta = isBetaVersion(latestVersion);
    return isBeta ? ApiVersionType.Preview : ApiVersionType.Stable;
};

/**
 * A Node.js-based implementation of glob functionality
 * @param pattern The glob pattern to match
 * @returns A Promise that resolves to an array of matching file paths
 */
async function findFilesByPattern(pattern: string): Promise<string[]> {
    // Extract the base directory from the pattern
    const segments = pattern.split(/[\/\\]/);
    const wildcardIndex = segments.findIndex(segment => segment.includes('*'));
    const baseDir = wildcardIndex > 0 
        ? segments.slice(0, wildcardIndex).join(path.sep) 
        : '.';

    // Convert glob pattern to regex
    const regexPattern = new RegExp(
        `^${pattern
            .replace(/\//g, '[\\\\/]')
            .replace(/\./g, '\\.')
            .replace(/\*\*/g, '.*')
            .replace(/\*/g, '[^\\\\/]*')}$`
    );

    const results: string[] = [];

    // Recursively search for files
    async function scanDir(dir: string, remainingDepth = 20): Promise<void> {
        if (remainingDepth <= 0) return;

        try {
            const entries = await fs.readdir(dir, { withFileTypes: true });

            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                const unixPath = unixify(fullPath);

                if (regexPattern.test(unixPath)) {
                    results.push(unixPath);
                }

                if (entry.isDirectory()) {
                    await scanDir(fullPath, remainingDepth - 1);
                }
            }
        } catch (error) {
            // Silently ignore if directory doesn't exist or cannot be read
        }
    }

    await scanDir(baseDir);
    return results;
}