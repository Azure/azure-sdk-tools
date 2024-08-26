import { CommentArray, CommentJSONValue, CommentObject, assign, parse, stringify } from 'comment-json';
import { ModularClientPackageOptions, PackageResult, VersionPolicyName } from './types';
import { access, writeFile } from 'node:fs/promises';
import { basename, join, normalize, posix, relative, resolve } from 'node:path';
import { ensureDir, readFile } from 'fs-extra';
import { getArtifactName, getNpmPackageInfo } from './npmUtils';
import { runCommand, runCommandOptions } from './utils';

import { glob } from 'glob';
import { logger } from '../utils/logger';
import unixify from 'unixify';

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
    writeFile('rush.json', newRushJsonContent, { encoding: 'utf-8' });
    logger.info('Updated rush.json successfully.');
}

async function packPackage(packageDirectory: string, packageName: string, rushxScript: string) {
    const cwd = join(packageDirectory);
    await runCommand('node', [rushxScript, 'pack'], { ...runCommandOptions, cwd }, false);
    logger.info(`Pack '${packageName}' successfully.`);
}

async function addApiViewInfo(
    relativePackageDirectoryToSdkRoot: string,
    packageResult: PackageResult
): Promise<{ name: string; content: string }> {
    const apiViewPathPattern = posix.join(relativePackageDirectoryToSdkRoot, 'temp', '**/*.api.json');
    const apiViews = await glob(apiViewPathPattern);
    if (!apiViews || apiViews.length === 0) throw new Error(`Failed to get API views.`);
    if (apiViews && apiViews.length > 1) throw new Error(`Failed to get exactly one API view: ${apiViews}.`);
    packageResult.apiViewArtifact = apiViews[0];
    const content = (await readFile(apiViews[0], { encoding: 'utf-8' })).toString();
    const name = basename(apiViews[0]);
    return { content, name };
}

export async function buildPackage(
    generatedPackageDir: string,
    options: ModularClientPackageOptions,
    packageResult: PackageResult,
    rushScript: string,
    rushxScript: string
) {
    const relativePackageDirectoryToSdkRoot = relative(normalize(options.sdkRepoRoot), normalize(generatedPackageDir));
    logger.info(`Start building package in '${relativePackageDirectoryToSdkRoot}'.`);

    const { name } = await getNpmPackageInfo(relativePackageDirectoryToSdkRoot);
    await updateRushJson({
        packageName: name,
        projectFolder: unixify(relativePackageDirectoryToSdkRoot),
        versionPolicyName: options.versionPolicyName
    });
    await runCommand(`node`, [rushScript, 'update'], runCommandOptions, false);
    logger.info(`Rush update successfully.`);

    await runCommand('node', [rushScript, 'build', '-t', name, '--verbose'], runCommandOptions);
    const apiViewContext = await addApiViewInfo(relativePackageDirectoryToSdkRoot, packageResult);
    logger.info(`Build package '${name}' successfully.`);

    // build sample and test package will NOT throw exceptions
    // note: these commands will delete temp folder
    await tryBuildSamples(generatedPackageDir, rushxScript);
    await tryTestPackage(generatedPackageDir, rushxScript);

    // restore in temp folder
    const tempFolder = join(generatedPackageDir, 'temp');
    await ensureDir(tempFolder);
    const apiViewPath = join(tempFolder, apiViewContext.name);
    await writeFile(apiViewPath, apiViewContext.content, { encoding: 'utf-8' });
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryBuildSamples(packageDirectory: string, rushxScript: string) {
    logger.info(`Start to build samples in '${packageDirectory}'.`);
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, cwd };
    try {
        await runCommand(`node`, [rushxScript, 'build:samples'], options, true, 300);
        logger.info(`built samples successfully.`);
    } catch (err) {
        logger.error(`Failed to build samples due to: ${(err as Error)?.stack ?? err}`);
    }
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryTestPackage(packageDirectory: string, rushxScript: string) {
    logger.info(`Start to test package in '${packageDirectory}'.`);
    const env = { ...process.env, TEST_MODE: 'record' };
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, env, cwd };
    try {
        await runCommand(`node`, [rushxScript, 'test:node'], options, true, 300);
        logger.info(`tested package successfully.`);
    } catch (err) {
        logger.error(`Failed to test package due to: ${(err as Error)?.stack ?? err}`);
    }
}

export async function createArtifact(packageDirectory: string, rushxScript: string): Promise<string> {
    logger.info(`Start to create artifact in '${packageDirectory}'`);
    const info = await getNpmPackageInfo(packageDirectory);
    await packPackage(packageDirectory, info.name, rushxScript);
    const artifactName = getArtifactName(info);
    const artifactPath = posix.join(packageDirectory, artifactName);
    await access(artifactPath);
    logger.info(`Created artifact '${info.name}' in '${resolve(artifactPath)}' successfully.`);
    return artifactPath;
}
