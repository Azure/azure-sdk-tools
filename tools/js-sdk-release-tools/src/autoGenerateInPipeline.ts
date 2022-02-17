#!/usr/bin/env node

import {generateMgmt} from "./hlc/generateMgmt";
import {logger} from "./utils/logger";
import {SwaggerSdkAutomationOutputPackageInfo} from "./common-types/swaggerSdkAutomation";
import {generateRLCInPipeline} from "./llc/generateRLCInPipeline/generateRLCInPipeline";

const shell = require('shelljs');
const fs = require('fs');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use?: string) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] = inputJson['relatedReadmeMdFiles'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];

    if (readmeFiles.length !== 1) {
        throw new Error(`get ${readmeFiles.length} readme files`);
    }

    const packages: SwaggerSdkAutomationOutputPackageInfo[] = [];
    const outputJson = {
        packages: packages
    };
    for (const readmeMd of readmeFiles) {
        const isMgmt = readmeFiles[0].includes('resource-manager');
        if (isMgmt) {
            await generateMgmt({
                sdkRepo: String(shell.pwd()),
                swaggerRepo: specFolder,
                readmeMd: readmeMd,
                gitCommitId: gitCommitId,
                use: use,
                outputJson: outputJson,
                swaggerRepoUrl: repoHttpsUrl
            });
        } else {
            await generateRLCInPipeline({
                sdkRepo: String(shell.pwd()),
                swaggerRepo: specFolder,
                readmeMd: readmeMd,
                use: use,
                outputJson: outputJson
            })
        }
    }

    fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, undefined, '  '), {encoding: 'utf-8'})
}

const optionDefinitions = [
    { name: 'use',  type: String },
    { name: 'inputJsonPath', type: String },
    { name: 'outputJsonPath', type: String },
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use).catch(e => {
    logger.logError(e.message);
    process.exit(1);
});
