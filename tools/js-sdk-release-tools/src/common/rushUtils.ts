import { CommentArray, CommentJSONValue, CommentObject, assign, parse, stringify } from 'comment-json';
import { ModularClientPackageOptions, PackageResult } from './types.js';
import { access } from 'node:fs/promises';
import { basename, join, normalize, posix, relative, resolve } from 'node:path';
import pkg from 'fs-extra';
const { ensureDir, readFile, writeFile } = pkg;
import { getArtifactName, getNpmPackageInfo } from './npmUtils.js';
import { runCommand, runCommandOptions } from './utils.js';

import { glob } from 'glob';
import { logger } from '../utils/logger.js';
import { migratePackage } from './migration.js';

async function packPackage(packageDirectory: string, packageName: string) {
    const cwd = join(packageDirectory);
    await runCommand('pnpm', ['pack'], { ...runCommandOptions, cwd }, false);
    logger.info(`Pack '${packageName}' successfully.`);
}

async function addApiViewInfo(
    packageDirectory: string,
    sdkRoot: string,
    packageResult: PackageResult
): Promise<{ name: string; content: string }> {
    const apiViewPathPattern = posix.join(packageDirectory, 'temp', '**/*.api.json');
    const apiViews = await glob(apiViewPathPattern);
    if (!apiViews || apiViews.length === 0) throw new Error(`Failed to get API views in '${apiViewPathPattern}'. cwd: ${process.cwd()}`);
    if (apiViews && apiViews.length > 1) throw new Error(`Failed to get exactly one API view: ${apiViews}.`);
    packageResult.apiViewArtifact = relative(sdkRoot, apiViews[0]);
    const content = (await readFile(apiViews[0], { encoding: 'utf-8' })).toString();
    const name = basename(apiViews[0]);
    return { content, name };
}

export async function buildPackage(
    packageDirectory: string,
    options: ModularClientPackageOptions,
    packageResult: PackageResult
) {
    const relativePackageDirectoryToSdkRoot = relative(normalize(options.sdkRepoRoot), normalize(packageDirectory));
    logger.info(`Start to build package in '${relativePackageDirectoryToSdkRoot}'.`);

    const { name } = await getNpmPackageInfo(relativePackageDirectoryToSdkRoot);

    logger.info(`Start to pnpm install.`);
    await runCommand(`pnpm`, ['install'], runCommandOptions, false);
    logger.info(`Pnpm install successfully.`);

    await migratePackage(packageDirectory);

    logger.info(`Start to build package '${name}'.`);
    await runCommand('pnpm', ['build', '--filter', name], runCommandOptions);
    const apiViewContext = await addApiViewInfo(packageDirectory, options.sdkRepoRoot, packageResult);
    logger.info(`Build package '${name}' successfully.`);

    // build sample and test package will NOT throw exceptions
    // note: these commands will delete temp folder
    await tryBuildSamples(packageDirectory);    
    await tryTestPackage(packageDirectory);
    await formatSdk(packageDirectory);

    // restore in temp folder
    const tempFolder = join(packageDirectory, 'temp');
    await ensureDir(tempFolder);
    const apiViewPath = join(tempFolder, apiViewContext.name);
    await writeFile(apiViewPath, apiViewContext.content, { encoding: 'utf-8', flush: true });
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryBuildSamples(packageDirectory: string) {
    logger.info(`Start to build samples in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };
    try {
        await runCommand(`pnpm`, ['run', 'build:samples'], options, true, 300, true);
        logger.info(`built samples successfully.`);
    } catch (err) {
        logger.warn(`Failed to build samples due to: ${(err as Error)?.stack ?? err}`);
    }
}

export async function formatSdk(packageDirectory: string) {
    logger.info(`Start to format code in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };
    const formatCommand = 'run vendored prettier --write --config ../../../.prettierrc.json --ignore-path ../../../.prettierignore \"src/**/*.{ts,cts,mts}\" \"test/**/*.{ts,cts,mts}\" \"*.{js,cjs,mjs,json}\" \"samples-dev/*.ts\"';
    
    await runCommand(`npm`, ['exec', '--', 'dev-tool', formatCommand], options, true, 300, true);
    logger.info(`format sdk successfully.`);
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryTestPackage(packageDirectory: string) {
    logger.info(`Start to test package in '${packageDirectory}'.`);
    const env = { ...process.env, TEST_MODE: 'record' };
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, env, cwd };
    try {
        await runCommand(`pnpm`, ['run', 'test:node'], options, true, 300, true);
        logger.info(`tested package successfully.`);
    } catch (err) {
        logger.warn(`Failed to test package due to: ${(err as Error)?.stack ?? err}`);
    }
}

export async function createArtifact(packageDirectory: string): Promise<string> {
    logger.info(`Start to create artifact in '${packageDirectory}'`);
    const info = await getNpmPackageInfo(packageDirectory);
    await packPackage(packageDirectory, info.name);
    const artifactName = getArtifactName(info);
    const artifactPath = posix.join(packageDirectory, artifactName);
    await access(artifactPath);
    logger.info(`Created artifact '${info.name}' in '${resolve(artifactPath)}' successfully.`);
    return artifactPath;
}
