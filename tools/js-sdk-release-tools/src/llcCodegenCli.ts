#!/usr/bin/env node

import {logger} from "./utils/logger";
import {
    createFolderIfNotExist,
    findPackageInRepo, getInputFromCommand,
    getPackageFolderName,
    getPackageNameFromCommand,
    getPackageNameFromReadmeMd,
    getPackagePathFromReadmePath,
    validPackageName
} from "./llc/utils";
import {generateSampleReadmeMd} from "./llc/generateSampleReadmeMd";
import * as fs from "fs";
import * as path from "path";
import {buildGeneratedCodes, generateCodes} from "./llc/llcCore";

const shell = require('shelljs');

async function autoGenerate(options: any) {
    const sdkRepo = String(shell.pwd());
    let packagePath: string | undefined;
    let packageName: string | undefined;
    let readme = options.readme;
    if (!!readme) {
        packagePath = getPackagePathFromReadmePath(readme);
        packageName = getPackageNameFromReadmeMd(readme);
    } else {
        packageName = options['package-name'];
        if (!packageName || !validPackageName(packageName)) {
            if (!!packageName && !validPackageName(packageName)) {
                logger.logWarn(`Your package-name ${packageName} is invalid, it should be in format @azure-rest/xxxxx.`)
            }
            packageName = await getPackageNameFromCommand();
        }
        packagePath = findPackageInRepo(packageName, sdkRepo);
        if (!packagePath) {
            logger.logGreen(`${packageName} is first generated, please help input necessary information to create swagger/README.md`);
            const rp = options['service-name']? options['service-name'] : await getInputFromCommand('service-name');
            createFolderIfNotExist(path.join(sdkRepo, 'sdk', rp));
            createFolderIfNotExist(path.join(sdkRepo, 'sdk', rp, getPackageFolderName(packageName)));
            packagePath = path.join(sdkRepo, 'sdk', rp, getPackageFolderName(packageName));
            await generateSampleReadmeMd(packageName, packagePath, options);
        } else {
            if (!fs.existsSync(path.join(packagePath, 'swagger', 'README.md'))) {
                logger.logGreen(`${packageName} is found in ${packagePath}, but not contains swagger/README.md. Creating a sample one for quickstart`);
                await generateSampleReadmeMd(packageName, packagePath, options);
            }
        }
    }
    if (!packageName) {
        logger.logError(`Cannot get valid package-name.`);
        process.exit(1);
    }

    await generateCodes(packagePath, packageName, sdkRepo);
    await buildGeneratedCodes(sdkRepo, packagePath, packageName);
    logger.logGreen(``);
    logger.logGreen(`----------------------------------------------------------------`);
    logger.logGreen(``);
    logger.logGreen(`RLC code is generated and build successfully, please find it in ${packagePath}`);
}

const optionDefinitions = [
    {name: 'package-name', type: String},
    {name: 'title', type: String},
    {name: 'description', type: String},
    {name: 'input-file', type: String},
    {name: 'package-version', type: String},
    {name: 'credential-scopes', type: String},
    {name: 'readme', type: String},
    {name: 'service-name', type: String},

];
const commandLineArgs = require('command-line-args');
const options = commandLineArgs(optionDefinitions);
autoGenerate(options);
