import pkg from '@npmcli/package-json';
const { load } = pkg;
import { NpmPackageInfo } from './types.js';
import * as fetch from 'npm-registry-fetch';
import { getApiReviewPath } from './utils.js';
import shell from 'shelljs';
import { writeFile } from 'fs';
import path from 'path';
import { logger } from '../utils/logger.js';

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

export function tryCreateLastStableNpmView(lastStableVersion: string, packageName: string, packageFolderPath: string) {
    logger.info(`Start to get and clone Api View file from last ${packageName} stable release tag.`);
    const targentApiViewPath = getApiReviewPath(packageFolderPath).split("sdk");
    const apiViewPath = path.join("sdk", targentApiViewPath[targentApiViewPath.length - 1])

    const gitCommand = `git --no-pager show ${packageName}_${lastStableVersion}:${apiViewPath}`;

    const lastStableApiView = shell.exec(gitCommand, { silent: true });

    if (lastStableApiView.code !== 0) {
        logger.error(`Failed to read Api View file in ${apiViewPath} from the tag ${packageName}_${lastStableVersion}.`)
    }
    const lastStableApiViewContext = lastStableApiView.stdout;

    const lastStableApiViewPath = getApiReviewPath(path.join(packageFolderPath, 'changelog-temp', 'package'));
    writeFile(lastStableApiViewPath, lastStableApiViewContext, (err) => {
        if (err) {
            logger.error(`Failed to write Api View file in ${apiViewPath} from the tag ${packageName}_${lastStableVersion}.`);
        } else {
            logger.info(`Create Api View file from last stable package successfully`);
        }
    });
}

export function tryCreateLastChangeLog(packageFolderPath: string, packageName: string, version: string, targetChangelogPath: string) {
    logger.info(`Start to get and clone CHANGELOG.md from latest ${packageName} release tag.`);
    const changelogPathInRepo = path.join(packageFolderPath, "CHANGELOG.md")
    const gitCommand = `git --no-pager show ${packageName}_${version}:${changelogPathInRepo}`;

    const latestChangeLog = shell.exec(gitCommand, { silent: true });

    if (latestChangeLog.code !== 0) {
        logger.error(`Failed to read CHANGELOG.md in ${changelogPathInRepo} from the tag ${packageName}_${version}.`)
    }
    const latestChangeLogContext = latestChangeLog.stdout;

    writeFile(targetChangelogPath, latestChangeLogContext, (err) => {
        if (err) {
            logger.error(`Failed to write CHANGELOG.md in ${changelogPathInRepo} from the tag ${packageName}_${version}.`);
        } else {
            logger.info(`Create CHANGELOG.md from the latest npm package successfully`);
        }
    });
}