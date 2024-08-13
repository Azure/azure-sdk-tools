#!/usr/bin/env node

import * as path from 'path';
import { generateMgmt } from './hlc/generateMgmt';
import { backupNodeModules, restoreNodeModules } from './utils/backupNodeModules';
import { logger } from './utils/logger';
import { generateRLCInPipeline } from './llc/generateRLCInPipeline/generateRLCInPipeline';
import { RunningEnvironment } from './utils/runningEnvironment';
import { ModularClientPackageOptions, SDKType } from './common/types';
import { generateAzureSDKPackage } from './mlc/clientGenerator/modularClientPackageGenerator';
import { existsAsync } from './common/utils';

const shell = require('shelljs');
const fs = require('fs');

async function isManagementPlaneModularClient(specFolder: string, typespecProjectFolder: string[] | string | undefined) {
    if (Array.isArray(typespecProjectFolder) && (typespecProjectFolder as string[]).length !== 1) {
        throw new Error(`Unexpected typespecProjectFolder length: ${(typespecProjectFolder as string[]).length} (expect 1)`);
    }

    if (!typespecProjectFolder) {
        return false;
    }

    const resolvedRelativeTspFolder = Array.isArray(typespecProjectFolder) ? typespecProjectFolder[0] : typespecProjectFolder as string;
    const tspFolderFromSpecRoot = path.join(specFolder, resolvedRelativeTspFolder);
    const tspConfigPath = path.join(tspFolderFromSpecRoot, 'tspconfig.yaml');
    if (!(await existsAsync(tspConfigPath))) {
        return false;
    }

    const tspConfig = await loadTspConfig(tspFolderFromSpecRoot);
    if (!tspConfig?.options?.['@azure-tools/typespec-ts']?.['package-name']?.startsWith('@azure/')) {
        return false;
    }
    return true;
}


// TODO: consider add stricter rules for RLC in when update SDK automation for RLC
function getSDKType(isMgmtWithHLC: boolean, isMgmtWithModular: boolean) {
    if (isMgmtWithHLC) {
        return SDKType.HighLevelClient;
    }
    if (isMgmtWithModular) {
        return SDKType.ModularClient;
    }
    return SDKType.RestLevelClient;
}

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use: string | undefined, typespecEmitter: string | undefined, sdkGenerationType: string | undefined, skipBackupNodeModules: boolean) {
    // inputJson schema: https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/sdkautomation/GenerateInputSchema.json
    // todo: add interface for the schema
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, { encoding: 'utf-8' }));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string | undefined = inputJson['relatedReadmeMdFiles'] ? inputJson['relatedReadmeMdFiles'] : inputJson['relatedReadmeMdFile'];
    const typespecProjectFolder: string[] | string | undefined = inputJson['relatedTypeSpecProjectFolder'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const autorestConfig: string | undefined = inputJson['autorestConfig'];
    const downloadUrlPrefix: string | undefined = inputJson.installInstructionInput?.downloadUrlPrefix;
    // TODO: consider remove it, since it's not defined in inputJson schema
    const skipGeneration: boolean | undefined = inputJson['skipGeneration'];

    if (!readmeFiles && !typespecProjectFolder) {
        throw new Error(`readme files and typespec project info are both undefined`);
    }

    if (typespecProjectFolder && typeof typespecProjectFolder !== 'string' && typespecProjectFolder.length !== 1) {
        throw new Error(`get ${typespecProjectFolder.length} typespec project`);
    }

    const isTypeSpecProject = !!typespecProjectFolder;

    const packages: any[] = [];
    const outputJson = {
        packages: packages,
        language: 'JavaScript',
    };
    const readmeMd = isTypeSpecProject ? undefined : typeof readmeFiles === 'string' ? readmeFiles : readmeFiles![0];
    const typespecProject = isTypeSpecProject ? typeof typespecProjectFolder === 'string' ? typespecProjectFolder : typespecProjectFolder![0] : undefined;
    const isMgmtWithHLC = isTypeSpecProject ? false : readmeMd!.includes('resource-manager');
    const isMgmtWithModular = await isManagementPlaneModularClient(specFolder, typespecProjectFolder);
    const runningEnvironment = typeof readmeFiles === 'string' || typeof typespecProjectFolder === 'string' ? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
    const sdkType = getSDKType(isMgmtWithHLC, isMgmtWithModular);

    try {
        if (!skipBackupNodeModules) {
            await backupNodeModules(String(shell.pwd()));
        }
        switch (sdkType) {
            case SDKType.HighLevelClient:
                await generateMgmt({
                    sdkRepo: String(shell.pwd()),
                    swaggerRepo: specFolder,
                    readmeMd: readmeMd!,
                    gitCommitId: gitCommitId,
                    use: use,
                    outputJson: outputJson,
                    swaggerRepoUrl: repoHttpsUrl,
                    downloadUrlPrefix: downloadUrlPrefix,
                    skipGeneration: skipGeneration,
                    runningEnvironment: runningEnvironment
                });
                break;
            case SDKType.RestLevelClient:
                await generateRLCInPipeline({
                    sdkRepo: String(shell.pwd()),
                    swaggerRepo: path.isAbsolute(specFolder) ? specFolder : path.join(String(shell.pwd()), specFolder),
                    readmeMd: readmeMd,
                    typespecProject: typespecProject,
                    autorestConfig,
                    use: use,
                    typespecEmitter: !!typespecEmitter ? typespecEmitter : `@azure-tools/typespec-ts`,
                    outputJson: outputJson,
                    skipGeneration: skipGeneration,
                    sdkGenerationType: sdkGenerationType === 'command' ? 'command' : 'script',
                    runningEnvironment: runningEnvironment,
                    swaggerRepoUrl: repoHttpsUrl,
                    gitCommitId: gitCommitId
                });
                break;

            case SDKType.ModularClient: {
                const typeSpecDirectory = path.posix.join(specFolder, typespecProject!);
                const skip = skipGeneration ?? false;
                const repoUrl = repoHttpsUrl;
                const options: ModularClientPackageOptions = {
                    typeSpecDirectory,
                    gitCommitId,
                    skip,
                    repoUrl,
                    // support MPG for now
                    versionPolicyName: 'management'
                };
                const packageResult = await generateAzureSDKPackage(options);
                outputJson.packages = [packageResult];
                break;
            }
            default:
                break;
        }
    } catch (e) {
        const packageName = outputJson.packages?.[0].packageName;
        logger.error(`Failed to generate SDK for package ${"'" + packageName + "'" ?? ''} due to ${(e as Error)?.stack ?? e}.`);
        logger.error(`Please review the detail errors for potential fixes.`);
        logger.error(`If the issue persists, contact the support channel at https://aka.ms/azsdk/js-teams-channel and include this spec pull request.`)
        throw e;
    } finally {
        if (!skipBackupNodeModules) {
            await restoreNodeModules(String(shell.pwd()));
        }
        fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, null, '  '), { encoding: 'utf-8' });
    }
}

const optionDefinitions = [
    { name: 'use', type: String },
    { name: 'typespecEmitter', type: String },
    { name: 'sdkGenerationType', type: String },
    { name: 'inputJsonPath', type: String },
    { name: 'outputJsonPath', type: String },
    // this option is used to skip backup node modules in local, do NOT set to true in sdk automation pipeline 
    { name: 'skipBackupNodeModules', type: Boolean, defaultValue: false }
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.typespecEmitter, options.sdkGenerationType, options.skipBackupNodeModules ?? false).catch(e => {
    logger.error(e.message);
    process.exit(1);
});
