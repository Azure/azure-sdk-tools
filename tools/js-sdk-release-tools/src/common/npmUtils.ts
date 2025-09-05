import pkg from '@npmcli/package-json';
const { load } = pkg;
import { NpmPackageInfo } from './types.js';
import * as fetch from 'npm-registry-fetch';
import { getApiReviewPath, getApiReviewBasePath } from './utils.js';
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
    if (info.name.startsWith('@azure-rest/')) {
        return info.name.replace('@azure-rest/', 'azure-rest-');
    } else if (info.name.startsWith('@azure/')) {
        return info.name.replace('@azure/', 'azure-');
    } else {
        return info.name;
    }
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
    const targetFilePath = file === "CHANGELOG.md" ? path.join(npmPackagePath, file) : getApiReviewPath(npmPackagePath);
    const tag = `${packageName}_${version}`;
    const defaultContent = "```ts\n```";
    logger.info(`Start to get and clone ${npmPackagePath} from latest ${packageName} release tag.`);

    // Check if tag exists
    const tagCheckCommand = `git tag -l ${tag}`;
    const tagExists = shell.exec(tagCheckCommand, { silent: true }).stdout.trim();
    if (!tagExists) {
        logger.warn(`Warning: Git tag '${tag}' does not exist in the repository.`);
        if(file !== "CHANGELOG.md") {
            fs.writeFileSync(targetFilePath, defaultContent, { encoding: 'utf-8' });
        }        
        return;
    }

    try {
        if (file === "CHANGELOG.md") {
            sdkFilePath = relative(sdkRootPath, path.join(packageFolderPath, file)).replace(/\\/g, "/");
            // For CHANGELOG.md, use sdkFilePath directly
            const gitCommand = `git --no-pager show ${tag}:${sdkFilePath}`;
            const changelogContent = shell.exec(gitCommand, { silent: true }).stdout;
            if (!changelogContent.trim()) {
                logger.warn(`Warning: CHANGELOG.md content is empty for tag ${tag} at path ${sdkFilePath}.`);
            }
            fs.writeFileSync(targetFilePath, changelogContent, { encoding: 'utf-8' });
        }
        else {
            sdkFilePath = relative(sdkRootPath, getApiReviewBasePath(packageFolderPath)).replace(/\\/g, "/");
            // For API review files, generate two file paths with different suffixes
            const nodeApiFilePath = `${sdkFilePath}-node.api.md`;
            const standardApiFilePath = `${sdkFilePath}.api.md`;

            // Generate two git commands
            const nodeApiGitCommand = `git --no-pager show ${tag}:${nodeApiFilePath}`;
            const standardApiGitCommand = `git --no-pager show ${tag}:${standardApiFilePath}`;

            // Execute both git commands
            const nodeApiResult = shell.exec(nodeApiGitCommand, { silent: true }).stdout;
            const standardApiResult = shell.exec(standardApiGitCommand, { silent: true }).stdout;

            // Use nodeApi result if it has content, otherwise use standardApi result
            let apiViewContent = nodeApiResult.trim() ? nodeApiResult : standardApiResult;
            if (!apiViewContent.trim()) {
                logger.warn(`Warning: No API view content found for either ${nodeApiFilePath} or ${standardApiFilePath}. Using default content.`);
            }
            fs.writeFileSync(targetFilePath, apiViewContent, { encoding: 'utf-8' });
        }
        logger.info(`Create ${packageFolderPath} from the tag ${tag} successfully`);
    } catch (error) {
        logger.error(`Failed to read ${packageFolderPath} in ${sdkFilePath} from the tag ${tag}.\n Error details: ${error}`)
    }
}
