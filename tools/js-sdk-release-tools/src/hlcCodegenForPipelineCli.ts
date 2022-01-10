#!/usr/bin/env node

import {generateSdkAutomatically, OutputPackageInfo} from "./hlc/hlcCore";

const shell = require('shelljs');
const fs = require('fs');
const path = require('path');

async function automationGenerateInPipeline(inputJsonPath: string, outputJsonPath: string, use?: string, useDebugger?: boolean) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFiles: string[] = inputJson['relatedReadmeMdFiles'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const packages: OutputPackageInfo[] = [];
    const outputJson = {
        packages: packages
    };
    for (const readmeMd of readmeFiles) {
        await generateSdkAutomatically(String(shell.pwd()), path.join(specFolder, readmeMd), readmeMd, gitCommitId, undefined, use, useDebugger, undefined, outputJson, repoHttpsUrl);
    }

    fs.writeFileSync(outputJsonPath, JSON.stringify(outputJson, undefined, '  '), {encoding: 'utf-8'})
}

const optionDefinitions = [
    { name: 'use',  type: String },
    { name: 'inputJsonPath', type: String },
    { name: 'outputJsonPath', type: String },
    { name: 'debugger', type: String}
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInPipeline(options.inputJsonPath, options.outputJsonPath, options.use, options.useDebugger? true : false);
