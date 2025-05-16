#!/usr/bin/env node

import * as path from 'path';
import { generateMgmt } from './hlc/generateMgmt.js';
import { backupNodeModules, restoreNodeModules } from './utils/backupNodeModules.js';
import { logger } from './utils/logger.js';
import { generateRLCInPipeline } from './llc/generateRLCInPipeline/generateRLCInPipeline.js';
import { ModularClientPackageOptions, SDKType } from './common/types.js';
import { generateAzureSDKPackage } from './mlc/clientGenerator/modularClientPackageGenerator.js';
import { parseInputJson } from './utils/generateInputUtils.js';
import { generateCodeOwnersAndIgnoreLink } from './common/codeownersAndignorelink/generateCodeOwnersAndIgnoreLink.js';

import shell from 'shelljs';
import fs from 'fs';

async function automationGenerateInPipeline(
    inputJsonPath: string,
    outputJsonPath: string,
    use: string | undefined,
    typespecEmitter: string | undefined,
    sdkGenerationType: string | undefined,
    local: boolean
) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, { encoding: 'utf-8' }));
    const {
        sdkType,
        specFolder,
        readmeMd,
        gitCommitId,
        outputJson,
        repoHttpsUrl,
        downloadUrlPrefix,
        skipGeneration,
        runningEnvironment,
        typespecProject,
        autorestConfig
    } = await parseInputJson(inputJson);

    try {
        if (!local) {
            await backupNodeModules(String(shell.pwd()));
        }
        let changedPackagePaths: string[] = [];
        switch (sdkType) {
            case SDKType.HighLevelClient:
                changedPackagePaths = await generateMgmt({
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
                const sdkRepoRoot = String(shell.pwd()).replaceAll('\\', '/');
                const skip = skipGeneration ?? false;
                const repoUrl = repoHttpsUrl;
                const options: ModularClientPackageOptions = {
                    sdkRepoRoot,
                    specRepoRoot: specFolder,
                    typeSpecDirectory,
                    gitCommitId,
                    skip,
                    repoUrl,
                    local,
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

        await generateCodeOwnersAndIgnoreLink(sdkType, {
            changedPackagePaths: changedPackagePaths!,
            typespecProject: typespecProject,
            typeSpecDirectory: specFolder,
            sdkRepo: String(shell.pwd()),
           });
    } catch (e) {
        const packageNameStr = `'${outputJson.packages?.[0]?.packageName}' `;
        logger.error(`Failed to generate SDK for package ${packageNameStr ?? ''}due to ${(e as Error)?.stack ?? e}.`);
        logger.error(`Please review the detail errors for potential fixes.`);
        logger.error(
            `If the issue persists, contact the support channel at https://aka.ms/azsdk/js-teams-channel and include this spec pull request.`
        );
        throw e;
    } finally {
        if (!local) {
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
    // this option should be only used in local run, it will skip backup node modules, etc.
    // do NOT set to true in sdk automation pipeline 
    { name: 'local', type: Boolean, defaultValue: false }
];
import commandLineArgs from 'command-line-args';
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.typespecEmitter, options.sdkGenerationType, options.local ?? false).catch(e => {
    logger.error(e.message);
    process.exit(1);
});
