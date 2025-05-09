import pkg from '@npmcli/package-json';
const { load } = pkg;
import { NpmPackageInfo } from './types.js';
import * as fetch from 'npm-registry-fetch';
import { getApiReviewPath } from './utils.js';
import shell from 'shelljs';
import { writeFile } from 'fs';
import path from 'path';
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

export async function tryGetNpmView(packageName: string): Promise<{ [id: string]: any } | undefined> {
    try {
        return await fetch.json(`/${packageName}`);
    } catch (err) {
        return undefined;
    }
}

export async function tryCreateLastStableNpmView(lastStableVersion: string, packageName: string, packageFolderPath: string) {
    logger.info(`Start to get and clone Api View file from last ${packageName} stable release tag.`);
    const targetApiViewPath = getApiReviewPath(packageFolderPath).split("sdk");
    const apiViewPath = path.join("sdk", targetApiViewPath[targetApiViewPath.length - 1]).replace(/\\/g, "/");

    const gitCommand = `git --no-pager show ${packageName}_${lastStableVersion}:${apiViewPath}`;

    try {
        const lastStableApiViewContext = shell.exec(gitCommand, { silent: true }).stdout;

        const lastStableApiViewPath = getApiReviewPath(path.join(packageFolderPath, 'changelog-temp', 'package'));
        fs.writeFileSync(lastStableApiViewPath, lastStableApiViewContext, { encoding: 'utf-8' });
        logger.info(`Create Api View file from the tag ${packageName}_${lastStableVersion} package successfully`);
    } catch (error) {
        logger.error(`Failed to read Api View file in ${apiViewPath} from the tag ${packageName}_${lastStableVersion}.\n Error details: ${error}`);
    }
}

export function tryCreateLastestChangeLog(packageFolderPath: string, packageName: string, version: string, targetChangelogPath: string) {
    logger.info(`Start to get and clone CHANGELOG.md from latest ${packageName} release tag.`);
    const targentchangelogPath = packageFolderPath.split("sdk");
    const changelogPathInRepo = path.join("sdk", targentchangelogPath[targentchangelogPath.length - 1], "CHANGELOG.md").replace(/\\/g, "/");
    const gitCommand = `git --no-pager show ${packageName}_${version}:${changelogPathInRepo}`;

    try {
        const latestChangeLog = shell.exec(gitCommand, { silent: true });
        const latestChangeLogContext = latestChangeLog.stdout;

        fs.writeFileSync(targetChangelogPath, latestChangeLogContext, { encoding: 'utf-8' });
        logger.info(`Create CHANGELOG.md from the tag ${packageName}_${version} successfully`);
    } catch (error) {
        logger.error(`Failed to read CHANGELOG.md in ${changelogPathInRepo} from the tag ${packageName}_${version}.\n Error details: ${error}`)
    }
}