#!/usr/bin/env node

import {generateSdkAutomatically, OutputPackageInfoForSdkGeneration} from "./hlc/hlcCore";

const shell = require('shelljs');
const fs = require('fs');
const path = require('path');

async function automationGenerateForSdkGeneration(inputJsonPath: string, outputJsonPath: string, use?: string, useDebugger?: boolean) {
    const inputJson = JSON.parse(fs.readFileSync(inputJsonPath, {encoding: 'utf-8'}));
    const specFolder: string = inputJson['specFolder'];
    const readmeFile: string = inputJson['relatedReadmeMdFile'];
    const gitCommitId: string = inputJson['headSha'];
    const repoHttpsUrl: string = inputJson['repoHttpsUrl'];
    const packages: OutputPackageInfoForSdkGeneration[] = [];
    const outputJson = {
        packages: packages
    };
    await generateSdkAutomatically(String(shell.pwd()), readmeFile, path.relative(specFolder, readmeFile), gitCommitId, true, undefined, use, useDebugger, undefined, outputJson, repoHttpsUrl);

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
automationGenerateForSdkGeneration(options.inputJsonPath, options.outputJsonPath, options.use, options.useDebugger? true : false);
