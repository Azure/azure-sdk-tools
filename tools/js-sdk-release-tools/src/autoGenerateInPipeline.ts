#!/usr/bin/env node

import * as path from 'path';
import {generateMgmt} from "./hlc/generateMgmt";
import { backupNodeModules, restoreNodeModules } from './utils/backupNodeModules';
import {logger} from "./utils/logger";
import {generateRLCInPipeline} from "./llc/generateRLCInPipeline/generateRLCInPipeline";
import {RunningEnvironment} from "./utils/runningEnvironment";

const shell = require('shelljs');
const fs = require('fs');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use: string | undefined, typeSpecEmitter: string | undefined) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string | undefined = inputJson['relatedReadmeMdFiles']? inputJson['relatedReadmeMdFiles']: inputJson['relatedReadmeMdFile'];
    const typeSpecProjectFolder: string[] | string | undefined = inputJson['relatedTypeSpecProjectFolder'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const autorestConfig: string | undefined = inputJson['autorestConfig'];
    const downloadUrlPrefix: string | undefined = inputJson.installInstructionInput?.downloadUrlPrefix;
    const skipGeneration: boolean | undefined = inputJson['skipGeneration'];

    if (!readmeFiles && !typeSpecProjectFolder) {
        throw new Error(`readme files and typespec project info are both undefined`);
    }

    if (readmeFiles && (typeof readmeFiles !== 'string') && readmeFiles.length !== 1) {
        throw new Error(`get ${readmeFiles.length} readme files`);
    }

    if (typeSpecProjectFolder && (typeof typeSpecProjectFolder !== 'string') && typeSpecProjectFolder.length !== 1) {
        throw new Error(`get ${typeSpecProjectFolder.length} typespec project`);
    }

    const isTypeSpecProject = !!typeSpecProjectFolder;

    const packages: any[] = [];
    const outputJson = {
        packages: packages
    };
    const readmeMd = isTypeSpecProject? undefined : typeof readmeFiles === 'string'? readmeFiles : readmeFiles![0];
    const typeSpecProject = isTypeSpecProject? typeof typeSpecProjectFolder === 'string'? typeSpecProjectFolder : typeSpecProjectFolder![0] : undefined;
    const isMgmt = isTypeSpecProject? false : readmeMd!.includes('resource-manager');
    const runningEnvironment = typeof readmeFiles === 'string' || typeof typeSpecProjectFolder === 'string'? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
    await backupNodeModules(String(shell.pwd()));
    if (isMgmt) {
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
    } else {
        await generateRLCInPipeline({
            sdkRepo: String(shell.pwd()),
            swaggerRepo: path.isAbsolute(specFolder)? specFolder : path.join(String(shell.pwd()), specFolder),
            readmeMd: readmeMd,
            typeSpecProject: typeSpecProject,
            autorestConfig,
            use: use,
            typeSpecEmitter: !!typeSpecEmitter? typeSpecEmitter : `@azure-tools/typespec-ts`,
            outputJson: outputJson,
            skipGeneration: skipGeneration,
            runningEnvironment: runningEnvironment
        })
    }
    await restoreNodeModules(String(shell.pwd()));
    fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, null, '  '), {encoding: 'utf-8'})
}

const optionDefinitions = [
    {name: 'use', type: String},
    {name: 'typeSpecEmitter', type: String},
    {name: 'inputJsonPath', type: String},
    {name: 'outputJsonPath', type: String},
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.typeSpecEmitter).catch(e => {
    logger.logError(e.message);
    process.exit(1);
});
