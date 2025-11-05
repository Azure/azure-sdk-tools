import { CommentArray, CommentJSONValue, CommentObject, assign, parse, stringify } from 'comment-json';
import { ModularClientPackageOptions, ModularSDKType, PackageResult, RunMode } from './types.js';
import { access } from 'node:fs/promises';
import { basename, join, normalize, posix, relative, resolve } from 'node:path';
import pkg from 'fs-extra';
const { ensureDir, readFile, writeFile } = pkg;
import { getArtifactName, getNpmPackageInfo } from './npmUtils.js';
import { runCommand, runCommandOptions } from './utils.js';

import { glob } from 'glob';
import { logger } from '../utils/logger.js';
import unixify from 'unixify';
import { existsSync } from 'fs';
import { customizeCodes, formatSdk, lintFix, updateSnippets } from './devToolUtils.js';
import { getModularSDKType } from '../utils/generateInputUtils.js';

interface ProjectItem {
    packageName: string;
    projectFolder: string;
    versionPolicyName: string;
}

async function updateRushJson(projectItem: ProjectItem) {
    const content = await readFile('rush.json', { encoding: 'utf-8' });
    const rushJson = parse(content.toString());
    const projects = (rushJson as CommentObject)?.['projects'] as CommentArray<CommentJSONValue>;
    if (!projects) {
        throw new Error('Failed to parse projects in rush.json.');
    }
    const isCurrentPackageExist = projects.filter((p) => p?.['packageName'] === projectItem.packageName).length > 0;
    if (isCurrentPackageExist) {
        logger.info(`'${projectItem.packageName}' exists, no need to update rush.json.`);
        return;
    }
    // add new project and keep comment at the same time
    const newProjects = assign(projects, [...projects, projectItem]);
    const newRushJson = assign(rushJson, { ...(rushJson as CommentObject), projects: newProjects });
    const newRushJsonContent = stringify(newRushJson, undefined, 2);
    writeFile('rush.json', newRushJsonContent, { encoding: 'utf-8', flush: true });
    logger.info('Updated rush.json successfully.');
}

async function packPackage(packageDirectory: string, packageName: string, rushxScript: string, sdkRepoRoot: string) {
    const cwd = join(packageDirectory);
    if (isRushRepo(sdkRepoRoot)) {
        await runCommand('node', [rushxScript, 'pack'], { ...runCommandOptions, cwd }, false);
    } else {
        await runCommand('pnpm', ['run', '--filter', `${packageName}...`, 'pack'], { ...runCommandOptions, cwd }, false);
    }
    logger.info(`Pack '${packageName}' successfully.`);
}

async function ensurePnpmInstalled() {
    try {
        await runCommand('pnpm', ['--version'], runCommandOptions, false);
        logger.info('pnpm is already installed.');
    } catch (error) {
        logger.info('pnpm not found. Installing...');
        await runCommand('npm', ['install', '-g', 'pnpm'], runCommandOptions);
        logger.info('pnpm installed successfully.');
    }
}

async function addApiViewInfo(
    packageDirectory: string,
    sdkRoot: string,
    packageName: string,
    packageResult: PackageResult
): Promise<{ name: string; content: string }> {
    // Extract the actual package name part from scoped package name 
    // (e.g., @azure/arm-oracledatabase -> arm-oracledatabase, @azure-rest/ai-language-conversations -> ai-language-conversations)
    let actualPackageName = packageName;
    if (packageName.startsWith('@azure/')) {
        actualPackageName = packageName.substring('@azure/'.length);
    } else if (packageName.startsWith('@azure-rest/')) {
        actualPackageName = packageName.substring('@azure-rest/'.length);
    }
    // Try both possible API view file name formats
    const nodeApiViewFileName = `${actualPackageName}-node.api.json`;
    const standardApiViewFileName = `${actualPackageName}.api.json`;
    const nodeApiViewPattern = posix.join(packageDirectory, 'temp', '**', nodeApiViewFileName);
    const standardApiViewPattern = posix.join(packageDirectory, 'temp', '**', standardApiViewFileName);

    // Search for both possible API view file name formats simultaneously
    const [nodeApiViews, standardApiViews] = await Promise.all([
        glob(nodeApiViewPattern),
        glob(standardApiViewPattern)
    ]);
    const apiViews = [...nodeApiViews, ...standardApiViews];

    if (!nodeApiViews.length && !standardApiViews.length) {
        throw new Error(`Failed to find any API view files matching '${nodeApiViewPattern}' or '${standardApiViewPattern}'. cwd: ${process.cwd()}`);
    }

    const selectedApiView = nodeApiViews.length > 0 ? nodeApiViews[0] : standardApiViews[0];

    packageResult.apiViewArtifact = relative(sdkRoot, selectedApiView);
    const content = (await readFile(apiViews[0], { encoding: 'utf-8' })).toString();
    const name = basename(apiViews[0]);
    return { content, name };
}

export function isRushRepo(sdkRepoRoot: string): boolean {
    const rushJsonPath = join(sdkRepoRoot, 'rush.json');
    return existsSync(rushJsonPath);
}

export async function buildPackage(
    packageDirectory: string,
    options: ModularClientPackageOptions,
    packageResult: PackageResult,
    rushScript: string,
    rushxScript: string
) {
    const relativePackageDirectoryToSdkRoot = relative(normalize(options.sdkRepoRoot), normalize(packageDirectory));
    logger.info(`Start to build package in '${relativePackageDirectoryToSdkRoot}'.`);

    const { name } = await getNpmPackageInfo(relativePackageDirectoryToSdkRoot);
    let buildStatus = `succeeded`;
    if (isRushRepo(options.sdkRepoRoot)) {
        await updateRushJson({
            packageName: name,
            projectFolder: unixify(relativePackageDirectoryToSdkRoot),
            versionPolicyName: options.versionPolicyName
        });

        logger.info(`Start to rush update.`);
        await runCommand(`node`, [rushScript, 'update'], runCommandOptions, false);
        logger.info(`Rush update successfully.`);

        logger.info(`Start to build package '${name}'.`);
        await runCommand('node', [rushScript, 'build', '-t', name, '--verbose'], runCommandOptions);
    } else {
        await ensurePnpmInstalled();
        logger.info(`Start to pnpm install.`);
        await runCommand(`pnpm`, ['install'], runCommandOptions, false);
        logger.info(`Pnpm install successfully.`);

        if (options.runMode === RunMode.Local || options.runMode === RunMode.Release) {
            await lintFix(packageDirectory);
        }

        logger.info(`Start to build package '${name}'.`);
        const modularSDKType = getModularSDKType(packageDirectory);
        let errorAsWarning = false;
        if (modularSDKType === ModularSDKType.DataPlane) {
            await customizeCodes(packageDirectory);
            errorAsWarning = true;
        }
        try {
            await runCommand('pnpm', ['turbo', 'build', '--filter', `${name}...`, '--token 1'], runCommandOptions, true, undefined, errorAsWarning);
        } catch (error) {
            logger.warn(`Failed to build data plane package due to ${(error as Error)?.stack ?? error}`);
            buildStatus = `failed`;
        }
    }
    if (buildStatus === `succeeded`) {
        const apiViewContext = await addApiViewInfo(packageDirectory, options.sdkRepoRoot, name, packageResult);
        logger.info(`Build package '${name}' successfully.`);

        // restore in temp folder
        const tempFolder = join(packageDirectory, 'temp');
        await ensureDir(tempFolder);
        const apiViewPath = join(tempFolder, apiViewContext.name);
        await writeFile(apiViewPath, apiViewContext.content, { encoding: 'utf-8', flush: true });
    }

    // build sample and test package will NOT throw exceptions
    // note: these commands will delete temp folder
    await tryTestPackage(packageDirectory, rushxScript, options.sdkRepoRoot);
    await formatSdk(packageDirectory);
    await updateSnippets(packageDirectory);
}

// no exception will be thrown in non-release mode, since we don't want it stop sdk generation. sdk author will need to resolve the failure
// in release mode, exceptions will be thrown to ensure sample build succeeds
export async function tryBuildSamples(packageDirectory: string, rushxScript: string, sdkRepoRoot: string, runMode: RunMode) {
    logger.info(`Start to build samples in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };
    const errorAsWarning = runMode !== RunMode.Release;
    try {
        if (isRushRepo(sdkRepoRoot)) {
            await runCommand(`node`, [rushxScript, 'build:samples'], options, true, 300, errorAsWarning);
        } else {
            await runCommand(`pnpm`, ['run', 'build:samples'], options, true, 300, errorAsWarning);
        }
        logger.info(`built samples successfully.`);
    } catch (err) {
        logger.warn(`Failed to build samples due to: ${(err as Error)?.stack ?? err}`);
    }
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryTestPackage(packageDirectory: string, rushxScript: string, sdkRepoRoot: string) {
    logger.info(`Start to test package in '${packageDirectory}'.`);
    const env = { ...process.env, TEST_MODE: 'record' };
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, env, cwd };
    try {
        if (isRushRepo(sdkRepoRoot)) {
            await runCommand(`node`, [rushxScript, 'test:node'], options, true, 300, true);
        } else {
            await runCommand(`pnpm`, ['run', 'test:node'], options, true, 300, true);
        }
        logger.info(`tested package successfully.`);
    } catch (err) {
        logger.warn(`Failed to test package due to: ${(err as Error)?.stack ?? err}`);
    }
}

export async function createArtifact(packageDirectory: string, rushxScript: string, sdkRepoRoot: string): Promise<string> {
    logger.info(`Start to create artifact in '${packageDirectory}'`);
    const info = await getNpmPackageInfo(packageDirectory);
    await packPackage(packageDirectory, info.name, rushxScript, sdkRepoRoot);
    const artifactName = getArtifactName(info);
    const artifactPath = posix.join(packageDirectory, artifactName);
    await access(artifactPath);
    logger.info(`Created artifact '${info.name}' in '${resolve(artifactPath)}' successfully.`);
    return artifactPath;
}
