import { NpmPackageInfo, VersionPolicyName } from './types.js';
import { dirname, posix } from 'path';
import { getNpmPackageName, getNpmPackageSafeName } from './npmUtils.js';
import { parse, stringify } from 'yaml';
import { readFile, writeFile } from 'fs/promises';

import { existsAsync } from './utils.js';
import { logger } from '../utils/logger.js';
import { fileURLToPath } from 'url';

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

    await writeCiYaml(ciMgmtPath, parsed);
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
    // Use two ways to get the dirname to avoid failures caused by node version issues.
    const __dirname = import.meta.dirname || dirname(fileURLToPath(import.meta.url));
    const templatePath = posix.join(__dirname, 'ciYamlTemplates/ci.mgmt.template.yml');
    const template = await readFile(templatePath, { encoding: 'utf-8' });
    const parsed = parse(template.toString());
    parsed.trigger.paths.include = [packageDirToSdkRoot, ciMgmtPath];
    parsed.pr.paths.include = [packageDirToSdkRoot, ciMgmtPath];
    parsed.extends.parameters.ServiceDirectory = serviceDirToSdkRoot.split('/')[1];
    parsed.extends.parameters.Artifacts = [artifact];

    await writeCiYaml(ciMgmtPath, parsed);
}

async function writeCiYaml(ciPath: string, config: any) {
    const content = comment + stringify(config);
    await writeFile(ciPath, content, { encoding: 'utf-8', flush: true });
    logger.info(`Created or updated CI file '${posix.resolve(ciPath)}' with content: \n${content}`);
}

async function updateDataPlaneCiYaml(
    ciPath: string,
    serviceDirToSdkRoot: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const content = await readFile(ciPath, { encoding: 'utf-8' });
    let parsed = parse(content.toString());

    makeSureArrayAvailableInCiYaml(parsed, ['trigger', 'branches', 'exclude']);
    makeSureArrayAvailableInCiYaml(parsed, ['pr', 'branches', 'exclude']);
    makeSureArrayAvailableInCiYaml(parsed, ['trigger', 'paths', 'include']);
    makeSureArrayAvailableInCiYaml(parsed, ['pr', 'paths', 'include']);
    makeSureArrayAvailableInCiYaml(parsed, ['extends', 'parameters', 'Artifacts']);

    const artifact: ArtifactInfo = getArtifact(npmPackageInfo);
    const artifactInclude = (array: ArtifactInfo[], item: ArtifactInfo) => array.map((a) => a.name).includes(item.name);
    const serviceDirectory = `${serviceDirToSdkRoot}/`;
    const ciMgmtPath = posix.join(serviceDirToSdkRoot, 'ci.mgmt.yml');

    let needUpdate = false;
    needUpdate = tryAddItemInArray(parsed.trigger.branches.exclude, 'feature/v4') || needUpdate;
    needUpdate = tryAddItemInArray(parsed.pr.branches.exclude, 'feature/v4') || needUpdate;
    
    needUpdate = tryAddItemInArray(parsed.trigger.paths.include, serviceDirectory) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.pr.paths.include, serviceDirectory) || needUpdate;
    needUpdate = tryAddItemInArray(parsed.extends.parameters.Artifacts, artifact, artifactInclude) || needUpdate;

    // Ensure ServiceDirectory is set correctly under extends.parameters
    const expectedServiceDirectory = serviceDirToSdkRoot;
    if (parsed.extends.parameters.ServiceDirectory !== expectedServiceDirectory) {
        parsed.extends.parameters.ServiceDirectory = expectedServiceDirectory;
        needUpdate = true;
    }
    // Sync exclusions from ci.mgmt.yml if it exists
    if (await existsAsync(ciMgmtPath)) {
        const mgmtContent = await readFile(ciMgmtPath, { encoding: 'utf-8' });
        const mgmtParsed = parse(mgmtContent.toString());
        const mgmtPaths = Array.from(new Set([ciMgmtPath, ...(mgmtParsed?.trigger?.paths?.include ?? [])]));
        makeSureArrayAvailableInCiYaml(parsed, ['trigger', 'paths', 'exclude']);
        makeSureArrayAvailableInCiYaml(parsed, ['pr', 'paths', 'exclude']);
        for (const p of mgmtPaths) {
            needUpdate = tryAddItemInArray(parsed.trigger.paths.exclude, p) || needUpdate;
            needUpdate = tryAddItemInArray(parsed.pr.paths.exclude, p) || needUpdate;
        }
    }

    if (needUpdate) {
        await writeCiYaml(ciPath, parsed);
    }
}

async function createDataPlaneCiYaml(
    ciPath: string,
    serviceDirToSdkRoot: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const artifact = getArtifact(npmPackageInfo);
    const __dirname = import.meta.dirname || dirname(fileURLToPath(import.meta.url));
    const templatePath = posix.join(__dirname, 'ciYamlTemplates/ci.template.yml');
    const template = await readFile(templatePath, { encoding: 'utf-8' });
    const parsed = parse(template.toString());
    const serviceDirectory = `${serviceDirToSdkRoot}/`;
    const ciMgmtPath = posix.join(serviceDirToSdkRoot, 'ci.mgmt.yml');

    parsed.trigger.paths.include = [serviceDirectory];
    parsed.pr.paths.include = [serviceDirectory];
    parsed.extends.parameters.ServiceDirectory = serviceDirToSdkRoot.split('/')[1];
    parsed.extends.parameters.Artifacts = [artifact];

    // Exclude management plane paths if ci.mgmt.yml exists
    if (await existsAsync(ciMgmtPath)) {
        const mgmtContent = await readFile(ciMgmtPath, { encoding: 'utf-8' });
        const mgmtParsed = parse(mgmtContent.toString());
        const mgmtPaths = Array.from(new Set([ciMgmtPath, ...(mgmtParsed?.trigger?.paths?.include ?? [])]));
        parsed.trigger.paths.exclude = mgmtPaths;
        parsed.pr.paths.exclude = mgmtPaths;
    } else {
        // When no ci.mgmt.yml exists, omit exclude keys entirely
        delete parsed.trigger.paths.exclude;
        delete parsed.pr.paths.exclude;
    }

    await writeCiYaml(ciPath, parsed);
}

async function createOrUpdateDataPlaneCiYaml(
    generatedPackageDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<string> {
    const serviceDirToSDKDir = posix.join(generatedPackageDirectory, '..');
    const ciPath = posix.join(serviceDirToSDKDir, 'ci.yml');

    if (!(await existsAsync(ciPath))) {
        await createDataPlaneCiYaml(ciPath, serviceDirToSDKDir, npmPackageInfo);
        return ciPath;
    }
    await updateDataPlaneCiYaml(ciPath, serviceDirToSDKDir, npmPackageInfo);
    return ciPath;
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
