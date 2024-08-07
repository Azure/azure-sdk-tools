import { runCommand, runCommandOptions } from './utils';
import { logger } from '../utils/logger';
import { CommentArray, CommentJSONValue, CommentObject, assign, parse, stringify } from 'comment-json';
import { access, readFile, writeFile } from 'node:fs/promises';
import { getArtifactName, getNpmPackageInfo } from './npmUtils';
import { posix, join } from 'node:path';
import { VersionPolicyName } from './types';

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
        logger.logInfo(`${projectItem.packageName} exists, no need to update rush.json.`);
        return;
    }
    // add new project and keep comment at the same time
    const newProjects = assign(projects, [...projects, projectItem]);
    const newRushJson = assign(rushJson, { ...(rushJson as CommentObject), projects: newProjects });
    const newRushJsonContent = stringify(newRushJson, undefined, 2);
    writeFile('rush.json', newRushJsonContent, { encoding: 'utf-8' });
    logger.logInfo('Updated rush.json successfully.');
}

async function packPackage(packageDirectory: string) {
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, cwd };
    logger.logInfo(`Start rushx pack.`);
    await runCommand('rushx', ['pack'], options);
    logger.logInfo(`rushx pack successfully.`);
}

export async function buildPackage(packageDirectory: string, versionPolicyName: VersionPolicyName) {
    logger.logInfo(`Start building package in ${packageDirectory}.`);
    const { name } = await getNpmPackageInfo(packageDirectory);
    await updateRushJson({
        packageName: name,
        projectFolder: packageDirectory,
        versionPolicyName: versionPolicyName
    });
    await runCommand(`rush`, ['update'], runCommandOptions);
    logger.logInfo(`Rush update successfully.`);
    await runCommand('rush', ['build', '-t', name, '--verbose'], runCommandOptions);
    logger.logInfo(`Build package "${name}" successfully.`);
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryBuildSamples(packageDirectory: string) {
    logger.logInfo(`Start building samples in ${packageDirectory}.`);

    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, cwd };
    let output: { stdout: string; stderr: string } | undefined;
    try {
        await runCommand(`rushx`, ['build:samples'], options, false, 300);
        logger.logInfo(`built samples successfully.`);
    } catch (err) {
        logger.logError(`Building samples failed due to: ${(err as Error)?.stack ?? err}`);
    }
}

// no exception will be thrown, since we don't want it stop sdk generation. sdk author will need to resolve the failure
export async function tryTestPackage(packageDirectory: string) {
    logger.logInfo(`Start testing package in ${packageDirectory}.`);

    const env = { ...process.env, TEST_MODE: 'record' };
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, env, cwd };
    try {
        await runCommand(`rushx`, ['test'], options, false, 300);
        logger.logInfo(`tested package successfully.`);
    } catch (err) {
        logger.logError(`Test package failed due to: ${(err as Error)?.stack ?? err}`);
    }
}

export async function createArtifact(packageDirectory: string) {
    logger.logInfo(`Start creating artifact in ${packageDirectory}`);
    await packPackage(packageDirectory);
    const info = await getNpmPackageInfo(packageDirectory);
    const artifactName = getArtifactName(info);
    const artifactPath = posix.join(packageDirectory, artifactName);
    await access(artifactPath);
    logger.logInfo(`Creating artifact ${info.name} successfully.`);
    return artifactPath;
}
