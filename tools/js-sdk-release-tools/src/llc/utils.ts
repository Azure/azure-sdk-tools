import * as fs from "fs";
import * as path from "path";
import {logger} from "../utils/logger";
import {NPMScope} from "@ts-common/azure-js-dev-tools";
import {getLatestStableVersion} from "../utils/version";
const readline = require('readline');

export function validPackageName(packageName) {
    const match = /@azure-rest\/[a-zA-Z-]+/.exec(packageName);
    if (!match)
        return false;
    else
        return true;
}

export function findPackageInRepo(packageName, sdkRepo) {
    const rps = fs.readdirSync(path.join(sdkRepo, 'sdk'));
    for (const rp of rps) {
        if (!fs.lstatSync(path.join(sdkRepo, 'sdk', rp)).isDirectory()) {
            continue;
        }
        const packages = fs.readdirSync(path.join(sdkRepo, 'sdk', rp));
        for (const p of packages) {
            if (!fs.lstatSync(path.join(sdkRepo, 'sdk', rp, p)).isDirectory()) {
                continue;
            }
            if (fs.existsSync(path.join(sdkRepo, 'sdk', rp, p, 'package.json'))) {
                const packageJson = path.join(sdkRepo, 'sdk', rp, p, 'package.json');
                const packageJsonContent = JSON.parse(fs.readFileSync(packageJson, {encoding: 'utf-8'}));
                if (packageName === packageJsonContent['name']) {
                    return path.join(sdkRepo, 'sdk', rp, p);
                }
            }
            if (fs.existsSync(path.join(sdkRepo, 'sdk', rp, p, 'swagger', 'README.md'))) {
                const readme = fs.readFileSync(path.join(sdkRepo, 'sdk', rp, p, 'swagger', 'README.md'), {encoding: 'utf-8'});
                const match = /package-name: "*(@azure-rest\/[a-zA-Z-]+)/.exec(readme);
                if (!!match && match.length === 2 && packageName === match[1]) {
                    return path.join(sdkRepo, 'sdk', rp, p);
                }
            }
        }
    }
    return undefined;
}

export function getPackageFolderName(packageName) {
    const match = /@azure-rest\/([a-z-]+)/.exec(packageName);
    if (!match || match.length !== 2) {
        logger.logError(`packageName ${packageName} is invalid, please input a new packageName in format "@azure-rest/*****"`);
        process.exit(1);
    } else {
        const subName = match[1];
        return `${subName}-rest`;
    }
}

export async function getLatestCodegen(packagePath) {
    const npm = new NPMScope({executionFolderPath: packagePath});
    const npmViewResult = await npm.view({packageName: '@autorest/typescript'});
    const stableVersion = getLatestStableVersion(npmViewResult);
    if (!stableVersion)
        return '6.0.0-beta.14';
    return stableVersion;
}

export function getRelativePackagePath(packagePath) {
    const match = /.*[\/\\](sdk[\/\\][a-zA-Z0-9-]+[\/\\][a-zA-Z0-9-]+)/.exec(packagePath);
    if (!!match && match.length == 2) {
        return match[1].replace(/\\/g, '/');
    } else {
        throw `Wrong package path ${packagePath};`;
    }
}

export function getPackagePathFromReadmePath(readmePath) {
    if (!fs.existsSync(readmePath)) {
        logger.logError(`Invalid README.md file path: ${readmePath}`);
        process.exit(1);
    } else {
        const absolutePath = path.resolve(readmePath);
        const match = /.*sdk[\/\\]+[a-zA-Z0-9-]+[\/\\]+[a-zA-Z0-9-]+/.exec(absolutePath);
        if (!match || match.length !== 1) {
            logger.logError(`Invalid README.md file path: ${readmePath}`);
            process.exit(1);
        }
        return match[0];
    }
}

export function createFolderIfNotExist(path: string) {
    if (!fs.existsSync(path)) {
        fs.mkdirSync(path);
    }
}

export function getPackageNameFromReadmeMd(readmePath) {
    const readme = fs.readFileSync(readmePath, {encoding: 'utf-8'});
    const match = /package-name: "*(@azure-rest\/[a-zA-Z-]+)/.exec(readme);
    if (!match || match.length !== 2) {
        logger.logError(`Cannot find invalid package name from ${readmePath}`);
        process.exit(1);
    }
    return match[1];
}


export async function getPackageNameFromCommand(): Promise<string> {
    while (true) {
        const packageName = await getInputFromCommand('package-name');
        if (validPackageName(packageName)) {
            return packageName;
        } else {
            logger.logWarn('Invalid package name. It should be in format @azure-rest/xxxxx, please input a new one: ')
        }
    }
}

function ask(query: string) {
    const rl = readline.createInterface({
        input: process.stdin,
        output: process.stdout,
    });

    return new Promise(resolve => rl.question(query, ans => {
        rl.close();
        resolve(ans);
    }))
}

export async function getInputFromCommand(parameter: string): Promise<string> {
    const messages = {
        'package-name': 'Please input packageName which should be in format @azure-rest/xxxxx: ',
        title: 'Please input the title of sdk: ',
        description: `Please input the description of sdk: `,
        'input-file': `Please input the swagger files. If you have multi input files, please use semicolons to separate: `,
        'package-version': `Please input the package version you want to generate: `,
        'credential-scopes': `Please input credential-scopes of your service: `,
        'resource-provider': `Which resource provider do you want to store your package in sdk folder? Please input it: `
    }
    const input = await ask(messages[parameter].yellow);
    return input as string;
}
