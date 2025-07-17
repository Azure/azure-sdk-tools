import pkg from '@npmcli/package-json';
const { load } = pkg;
import { NpmPackageInfo } from './types.js';
import * as fetch from 'npm-registry-fetch';
import { getApiReviewPath } from './utils.js';
import shell from 'shelljs';
import { writeFile } from 'fs';
import path, { relative } from 'path';
import { logger } from '../utils/logger.js';
import { error } from 'console';
import fs from 'fs';

export async function getNpmPackageInfo(packageDirectory): Promise<NpmPackageInfo> {
    const packageJson = await load(packageDirectory);
    if (!packageJson.content.name) {
        throw new Error(`package.json doesn't contains name property`);
    }
    if (!packageJson.content.version) {
        throw new Error(`package.json doesn't contains version property`);
    }
    const name = packageJson.content.name;
    const version = packageJson.content.version;
    return { name, version };
}

export function getNpmPackageName(info: NpmPackageInfo) {
    return info.name.replace('@azure/', 'azure-');
}

export function getNpmPackageSafeName(info: NpmPackageInfo) {
    const name = getNpmPackageName(info);
    const safeName = name.replace(/-/g, '');
    return safeName;
}

export function getArtifactName(info: NpmPackageInfo) {
    const name = getNpmPackageName(info);
    const version = info.version;
    return `${name}-${version}.tgz`;
}

export async function tryGetNpmView(packageName: string): Promise<{ [id: string]: unknown } | undefined> {
    try {
        return await fetch.json(`/${packageName}`);
    } catch (err) {
        return undefined;
    }
}

export interface NpmViewParameters {
    file: "ApiView" | "CHANGELOG.md";
    version: string;
    packageFolderPath: string;
    packageName: string;
    sdkRootPath: string;
    npmPackagePath: string;
}

// TODO: refactor this function to support praparing files from github in general way
export function tryCreateLastestStableNpmViewFromGithub(NpmViewParameters: NpmViewParameters) {
    const {
        file,
        version,
        packageFolderPath,
        packageName,
        sdkRootPath,
        npmPackagePath
    } = NpmViewParameters;
    let sdkFilePath = "";
    let targetFilePath = "";
    logger.info(`Start to get and clone ${npmPackagePath} from latest ${packageName} release tag.`);
    if (file === "CHANGELOG.md") {
        sdkFilePath = relative(sdkRootPath, path.join(packageFolderPath, file)).replace(/\\/g, "/");
        targetFilePath = path.join(npmPackagePath, file);
    }
    else {
        sdkFilePath = relative(sdkRootPath, getApiReviewPath(packageFolderPath)).replace(/\\/g, "/");
        targetFilePath = getApiReviewPath(npmPackagePath)
    }

    const gitCommand = `git --no-pager show ${packageName}_${version}:${sdkFilePath}`;

    try {
        const lastStableNpmViewContext = shell.exec(gitCommand, { silent: true }).stdout;

        fs.writeFileSync(targetFilePath, lastStableNpmViewContext, { encoding: 'utf-8' });
        logger.info(`Create ${packageFolderPath} from the tag ${packageName}_${version} successfully`);
    } catch (error) {
        logger.error(`Failed to read ${packageFolderPath} in ${sdkFilePath} from the tag ${packageName}_${version}.\n Error details: ${error}`)
    }
}
