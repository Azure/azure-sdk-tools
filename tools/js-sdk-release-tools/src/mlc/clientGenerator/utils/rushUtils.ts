import { ModularClientPackageOptions } from '../../../common/types';
import { runCommand, runCommandOptions } from '../../../common/utils';
import { logger } from '../../../utils/logger';
import { CommentArray, CommentJSONValue, CommentObject, assign, parse, stringify } from 'comment-json';
import { access, readFile, writeFile } from 'node:fs/promises';
import { getArtifactName, getNpmPackageInfo } from './npmUtils';
import { posix, join } from 'node:path';

interface ProjectItem {
    packageName: string;
    projectFolder: string;
    versionPolicyName: string;
}

// TODO: remove
// only used for local debugging
const dev_rush_build_package = false;
const dev_rush_pack_package = true;

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
    if (!dev_rush_pack_package) {
        return;
    }
    const env = { ...process.env, TEST_MODE: 'record' };
    const cwd = join(packageDirectory);
    const options = { ...runCommandOptions, env, cwd };

    logger.logInfo(`Start rushx test.`);
    try {
        await runCommand('rushx', ['test'], options);
        logger.logInfo(`rushx test successfully.`);

        logger.logInfo(`Start rushx pack.`);
        await runCommand('rushx', ['pack'], options);
        logger.logInfo(`rushx pack successfully.`);
    } catch (err) {
        logger.logError(`Run command failed due to: ${(err as Error)?.stack ?? err}`);
        throw err;
    }
}

function isRushTestPass(testOutput: string): boolean {
    return true;
}

export async function buildPackage(packageDirectory: string, options: ModularClientPackageOptions) {
    logger.logInfo(`Building package in ${packageDirectory}.`);
    const { name } = await getNpmPackageInfo(packageDirectory);
    await updateRushJson({
        packageName: name,
        projectFolder: packageDirectory,
        versionPolicyName: options.versionPolicyName
    });

    if (!dev_rush_build_package) {
        return;
    }
    try {
        await runCommand(`rush`, ['update'], runCommandOptions);
        logger.logInfo(`Rush update successfully.`);
        await runCommand('rush', ['build', '-t', name], runCommandOptions);
        logger.logInfo(`Build package "${name}" successfully.`);
    } catch (err) {
        logger.logError(`Run command failed due to: ${(err as Error)?.stack ?? err}`);
        throw err;
    }
}

export async function createArtifact(packageDirectory: string) {
    logger.logInfo(`Creating artifact in ${packageDirectory}`);
    await packPackage(packageDirectory);
    const info = await getNpmPackageInfo(packageDirectory);
    const artifactName = getArtifactName(info);
    const artifactPath = posix.join(packageDirectory, artifactName);

    try {
        await access(artifactPath);
    } catch (error) {
        throw new Error(`Failed to find artifact ${artifactPath}`);
    }
    logger.logInfo(`Creating artifact ${info.name} successfully.`);
    return artifactPath;
}
