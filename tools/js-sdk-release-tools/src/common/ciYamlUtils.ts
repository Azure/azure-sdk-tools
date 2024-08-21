import { NpmPackageInfo, VersionPolicyName } from './types';
import { basename, join, posix, resolve } from 'path';
import { getNpmPackageName, getNpmPackageSafeName } from './npmUtils';
import { parse, stringify } from 'yaml';
import { readFile, writeFile } from 'fs/promises';

import { existsAsync } from './utils';
import { logger } from '../utils/logger';

interface ArtifactInfo {
    name: string;
    safeName: string;
}

const comment = '# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.\n\n';

async function createOrUpdateManagePlaneCiYaml(
    packageDirToSdkRoot: string,
    npmPackageInfo: NpmPackageInfo
): Promise<string> {
    const serviceDirToSDKDir = posix.join(packageDirToSdkRoot, '..');
    const ciMgmtPath = posix.join(serviceDirToSDKDir, 'ci.mgmt.yml');

    if (!(await existsAsync(ciMgmtPath))) {
        await createManagementPlaneCiYaml(
            packageDirToSdkRoot,
            ciMgmtPath,
            serviceDirToSDKDir,
            npmPackageInfo
        );
        return ciMgmtPath;
    }
    await updateManagementPlaneCiYaml(packageDirToSdkRoot, ciMgmtPath, npmPackageInfo);
    return ciMgmtPath;
}

function tryAddItemInArray<TItem>(
    array: TItem[],
    item: TItem,
    include: (array: TItem[], item: TItem) => boolean = (a, i) => a.includes(i)
): boolean {
    let needUpdate = false;
    if (include(array, item) !== true) {
        needUpdate = true;
        array.push(item);
    }
    return needUpdate;
}

function makeSureArrayAvailableInCiYaml(current: any, path: string[]) {
    path.forEach((p, i) => {
        if (!current?.[p]) {
            current[p] = i === path.length - 1 ? [] : {};
        }
        current = current[p];
    });
}

async function updateManagementPlaneCiYaml(
    generatedPackageDirectory: string,
    ciMgmtPath: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const content = await readFile(ciMgmtPath, { encoding: 'utf-8' });
    let parsed = parse(content.toString());

    makeSureArrayAvailableInCiYaml(parsed, ['trigger', 'branches', 'exclude']);
    makeSureArrayAvailableInCiYaml(parsed, ['pr', 'branches', 'exclude']);
    makeSureArrayAvailableInCiYaml(parsed, ['trigger', 'paths', 'include']);
    makeSureArrayAvailableInCiYaml(parsed, ['pr', 'paths', 'include']);
    makeSureArrayAvailableInCiYaml(parsed, ['extends', 'parameters', 'Artifacts']);

    var artifact: ArtifactInfo = getArtifact(npmPackageInfo);
    var artifactInclude = (array: ArtifactInfo[], item: ArtifactInfo) => array.map((a) => a.name).includes(item.name);

    let needUpdate = false;
    needUpdate = tryAddItemInArray(parsed.trigger.branches.exclude, 'feature/v4') || needUpdate;
    needUpdate = tryAddItemInArray(parsed.pr.branches.exclude, 'feature/v4') || needUpdate;
    needUpdate = tryAddItemInArray(parsed.trigger.paths.include, generatedPackageDirectory) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.trigger.paths.include, ciMgmtPath) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.pr.paths.include, generatedPackageDirectory) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.pr.paths.include, ciMgmtPath) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.extends.parameters.Artifacts, artifact, artifactInclude) || needUpdate;

    writeCiYaml(ciMgmtPath, parsed);
}

function getArtifact(npmPackageInfo: NpmPackageInfo): ArtifactInfo {
    const name = getNpmPackageName(npmPackageInfo);
    const safeName = getNpmPackageSafeName(npmPackageInfo);
    return { name, safeName };
}

async function createManagementPlaneCiYaml(
    packageDirToSdkRoot: string,
    ciMgmtPath: string,
    serviceDirToSdkRoot: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const artifact = getArtifact(npmPackageInfo);
    const templatePath = join(__dirname, 'ciYamlTemplates/ci.mgmt.template.yml');
    const template = await readFile(templatePath, { encoding: 'utf-8' });
    const parsed = parse(template.toString());
    parsed.trigger.paths.include = [packageDirToSdkRoot, ciMgmtPath];
    parsed.pr.paths.include = [packageDirToSdkRoot, ciMgmtPath];
    parsed.extends.parameters.ServiceDirectory = serviceDirToSdkRoot;
    parsed.extends.parameters.Artifacts = [artifact];

    await writeCiYaml(ciMgmtPath, parsed);
}

async function writeCiYaml(ciMgmtPath: string, config: any) {
    const content = comment + stringify(config);
    await writeFile(ciMgmtPath, content, { encoding: 'utf-8', flush: true });
    logger.info(`Created Management CI file '${resolve(ciMgmtPath)}' with content: \n${content}`);
}

async function createOrUpdateDataPlaneCiYaml(
    generatedPackageDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<string> {
    throw new Error('Not implemented function');
}

export async function createOrUpdateCiYaml(
    relativeGeneratedPackageDirectoryToSdkRoot: string,
    versionPolicyName: VersionPolicyName,
    npmPackageInfo: NpmPackageInfo
): Promise<string> {
    logger.info('Start to create or update CI files');
    switch (versionPolicyName) {
        case 'management': {
            const ciPath = await createOrUpdateManagePlaneCiYaml(
                relativeGeneratedPackageDirectoryToSdkRoot,
                npmPackageInfo
            );
            logger.info('Created or updated MPG CI files successfully.');
            return ciPath;
        }
        case 'client': {
            const ciPath = await createOrUpdateDataPlaneCiYaml(
                relativeGeneratedPackageDirectoryToSdkRoot,
                npmPackageInfo
            );
            logger.info('Created or updated DPG CI files successfully.');
            return ciPath;
        }
        default:
            throw new Error(`Unsupported version policy name: ${versionPolicyName}`);
    }
}
