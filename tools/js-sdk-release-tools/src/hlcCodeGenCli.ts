#!/usr/bin/env node

import {logger} from "./utils/logger";
import {getLastCommitId} from "./utils/git";
import {generateMgmt} from "./hlc/generateMgmt";

const shell = require('shelljs');

async function automationGenerateInTerminal(absoluteReadmeMd: string, tag?: string, use?: string, additionalArgs?: string) {
    const regexResult = /^(.*[\/\\]azure-rest-api-specs[-pr]*)[\/\\](specification.*)/.exec(absoluteReadmeMd);
    if (!regexResult || regexResult.length !== 3) {
        logger.logError(`Cannot Parse readme file path: ${absoluteReadmeMd}`);
    } else {
        const gitCommitId = await getLastCommitId(regexResult[1]);
        await generateMgmt({
            sdkRepo: String(shell.pwd()),
            swaggerRepo: regexResult[1],
            readmeMd: regexResult[2],
            gitCommitId: gitCommitId,
            tag: tag,
            use: use,
            additionalArgs: additionalArgs,
        });
    }

}

const optionDefinitions = [
    { name: 'use',  type: String },
    { name: 'tag', type: String },
    { name: 'readme', type: String },
    { name: 'additional-args', type: String },
];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
automationGenerateInTerminal(options.readme, options.tag, options.use, options['additional-args']);
