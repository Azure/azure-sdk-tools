#!/usr/bin/env node

import * as path from 'path';
import {generateMgmt} from "./hlc/generateMgmt";
import { backupNodeModules, restoreNodeModules } from './utils/backupNodeModules';
import {logger} from "./utils/logger";
import {generateRLCInPipeline} from "./llc/generateRLCInPipeline/generateRLCInPipeline";
import {RunningEnvironment} from "./utils/runningEnvironment";

const shell = require('shelljs');
const fs = require('fs');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use: string | undefined, typespecEmitter: string | undefined, sdkGenerationType: string | undefined) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string | undefined = inputJson['relatedReadmeMdFiles']? inputJson['relatedReadmeMdFiles']: inputJson['relatedReadmeMdFile'];
    const typespecProjectFolder: string[] | string | undefined = inputJson['relatedTypeSpecProjectFolder'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const autorestConfig: string | undefined = inputJson['autorestConfig'];
    const downloadUrlPrefix: string | undefined = inputJson.installInstructionInput?.downloadUrlPrefix;
    const skipGeneration: boolean | undefined = inputJson['skipGeneration'];

    if (!readmeFiles && !typespecProjectFolder) {
        throw new Error(`readme files and typespec project info are both undefined`);
    }

    if (readmeFiles && (typeof readmeFiles !== 'string') && readmeFiles.length !== 1) {
        throw new Error(`get ${readmeFiles.length} readme files`);
    }

    if (typespecProjectFolder && (typeof typespecProjectFolder !== 'string') && typespecProjectFolder.length !== 1) {
        throw new Error(`get ${typespecProjectFolder.length} typespec project`);
    }

    const isTypeSpecProject = !!typespecProjectFolder;

    const packages: any[] = [];
    const outputJson = {
        packages: packages
    };
    const readmeMd = isTypeSpecProject? undefined : typeof readmeFiles === 'string'? readmeFiles : readmeFiles![0];
    const typespecProject = isTypeSpecProject? typeof typespecProjectFolder === 'string'? typespecProjectFolder : typespecProjectFolder![0] : undefined;
    const isMgmt = isTypeSpecProject? false : readmeMd!.includes('resource-manager');
    const runningEnvironment = typeof readmeFiles === 'string' || typeof typespecProjectFolder === 'string'? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
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
            typespecProject: typespecProject,
            autorestConfig,
            use: use,
            typespecEmitter: !!typespecEmitter? typespecEmitter : `@azure-tools/typespec-ts`,
            outputJson: outputJson,
            skipGeneration: skipGeneration,
            sdkGenerationType: (sdkGenerationType === "command") ? "command" : "script",
            runningEnvironment: runningEnvironment,
            swaggerRepoUrl: repoHttpsUrl,
            gitCommitId: gitCommitId,
        })
    }
    await restoreNodeModules(String(shell.pwd()));
    fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, null, '  '), {encoding: 'utf-8'})
}

const optionDefinitions = [
    {name: 'use', type: String},
    {name: 'typespecEmitter', type: String},
    {name: 'sdkGenerationType', type: String},
    {name: 'inputJsonPath', type: String},
    {name: 'outputJsonPath', type: String},
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.typespecEmitter, options.sdkGenerationType).catch(e => {
    logger.logError(e.message);
    process.exit(1);
});
