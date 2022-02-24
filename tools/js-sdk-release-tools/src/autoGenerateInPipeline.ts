#!/usr/bin/env node

import {generateMgmt} from "./hlc/generateMgmt";
import {logger} from "./utils/logger";
import {generateRLCInPipeline} from "./llc/generateRLCInPipeline/generateRLCInPipeline";
import {RunningEnvironment} from "./utils/runningEnvironment";

const shell = require('shelljs');
const fs = require('fs');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use?: string) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] | string = inputJson['relatedReadmeMdFiles']? inputJson['relatedReadmeMdFiles']: inputJson['relatedReadmeMdFile'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];

    if ((typeof readmeFiles !== 'string') && readmeFiles.length !== 1) {
        throw new Error(`get ${readmeFiles.length} readme files`);
    }

    const packages: any[] = [];
    const outputJson = {
        packages: packages
    };
    const readmeMd = typeof readmeFiles === 'string'? readmeFiles : readmeFiles[0];
    const isMgmt = readmeMd.includes('resource-manager');
    const runningEnvironment = typeof readmeFiles === 'string'? RunningEnvironment.SdkGeneration : RunningEnvironment.SwaggerSdkAutomation;
    if (isMgmt) {
        await generateMgmt({
            sdkRepo: String(shell.pwd()),
            swaggerRepo: specFolder,
            readmeMd: readmeMd,
            gitCommitId: gitCommitId,
            use: use,
            outputJson: outputJson,
            swaggerRepoUrl: repoHttpsUrl,
            runningEnvironment: runningEnvironment
        });
    } else {
        await generateRLCInPipeline({
            sdkRepo: String(shell.pwd()),
            swaggerRepo: specFolder,
            readmeMd: readmeMd,
            use: use,
            outputJson: outputJson,
            runningEnvironment: runningEnvironment
        })
    }

    fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, undefined, '  '), {encoding: 'utf-8'})
}

const optionDefinitions = [
    {name: 'use', type: String},
    {name: 'inputJsonPath', type: String},
    {name: 'outputJsonPath', type: String},
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use).catch(e => {
    logger.logError(e.message);
    process.exit(1);
});
