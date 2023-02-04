#!/usr/bin/env node

import * as path from 'path';
import {generateMgmt} from "./hlc/generateMgmt";
import { backupNodeModules, restoreNodeModules } from './utils/backupNodeModules';
import {logger} from "./utils/logger";
import {generateRLCInPipeline} from "./llc/generateRLCInPipeline/generateRLCInPipeline";
import {RunningEnvironment} from "./utils/runningEnvironment";

const shell = require('shelljs');
const fs = require('fs');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use: string | undefined, cadlEmitter: string | undefined) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string | undefined = inputJson['relatedReadmeMdFiles']? inputJson['relatedReadmeMdFiles']: inputJson['relatedReadmeMdFile'];
    const cadlProjectFolder: string[] | string | undefined = inputJson['relatedCadlProjectFolder'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const autorestConfig: string | undefined = inputJson['autorestConfig'];
    const downloadUrlPrefix: string | undefined = inputJson.installInstructionInput?.downloadUrlPrefix;
    const skipGeneration: boolean | undefined = inputJson['skipGeneration'];

    if (!readmeFiles && !cadlProjectFolder) {
        throw new Error(`readme files and cadl project info are both undefined`);
    }

    if (readmeFiles && (typeof readmeFiles !== 'string') && readmeFiles.length !== 1) {
        throw new Error(`get ${readmeFiles.length} readme files`);
    }

    if (cadlProjectFolder && (typeof cadlProjectFolder !== 'string') && cadlProjectFolder.length !== 1) {
        throw new Error(`get ${cadlProjectFolder.length} cadl project`);
    }

    const isCadlProject = !!cadlProjectFolder;

    const packages: any[] = [];
    const outputJson = {
        packages: packages
    };
    const readmeMd = isCadlProject? undefined : typeof readmeFiles === 'string'? readmeFiles : readmeFiles![0];
    const cadlProject = isCadlProject? typeof cadlProjectFolder === 'string'? cadlProjectFolder : cadlProjectFolder![0] : undefined;
    const isMgmt = isCadlProject? false : readmeMd!.includes('resource-manager');
    const runningEnvironment = typeof readmeFiles === 'string' || typeof cadlProjectFolder === 'string'? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
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
            cadlProject: cadlProject,
            autorestConfig,
            use: use,
            cadlEmitter: !!cadlEmitter? cadlEmitter : `@azure-tools/cadl-typescript`,
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
    {name: 'cadlEmitter', type: String},
    {name: 'inputJsonPath', type: String},
    {name: 'outputJsonPath', type: String},
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.cadlEmitter).catch(e => {
    logger.logError(e.message);
    process.exit(1);
});
