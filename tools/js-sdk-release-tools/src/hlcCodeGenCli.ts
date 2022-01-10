#!/usr/bin/env node

import {logger} from "./utils/logger";
import {getLastCommitId} from "./utils/git";
import {generateSdkAutomatically} from "./hlc/hlcCore";

const shell = require('shelljs');

async function automationGenerate(absoluteReadmeMd: string, tag?: string, use?: string, useDebugger?: boolean, additionalArgs?: string) {
    const regexResult = /^(.*\/azure-rest-api-specs[-pr]*)\/(specification.*)/.exec(absoluteReadmeMd);
    if (!regexResult || regexResult.length !== 3) {
        logger.logError(`Cannot Parse readme file path: ${absoluteReadmeMd}`);
    } else {
        const gitCommitId = await getLastCommitId(regexResult[1]);
        const relativeReadmeMd = regexResult[2];
        await generateSdkAutomatically(String(shell.pwd()), absoluteReadmeMd, relativeReadmeMd, gitCommitId, tag, use, useDebugger, additionalArgs);
    }

}

const optionDefinitions = [
    { name: 'use',  type: String },
    { name: 'tag', type: String },
    { name: 'readme', type: String },
    { name: 'useDebugger', type: String},
    { name: 'additional-args', type: String },
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerate(options.readme, options.tag, options.use, options.useDebugger? true : false, options['additional-args']);
