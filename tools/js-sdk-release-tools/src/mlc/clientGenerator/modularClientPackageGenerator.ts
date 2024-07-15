import { readFile, writeFile } from 'node:fs/promises';
import { parse, stringify } from 'yaml';
import { join } from 'path';
import { execSync } from 'child_process';
import { GeneratedPackageInfo, ModularClientPackageOptions, ChangelogInfo } from '../../common/types';
import { Changelog } from '../../changelog/changelogGenerator';
import { logger } from '../../utils/logger';
import { load } from '@npmcli/package-json';
import { generateChangelogAndBumpVersion } from '../changlog/generateChangelog';
import { CommentArray, CommentJSONValue, CommentObject, parse as parseCommentJson, assign } from 'comment-json';
import { execOptions } from '../../common/utils';
import { basename, posix } from 'node:path';
import { existsSync } from 'node:fs';

// TODO: remove
// only used for local debugging
const dev_generate_ts_code = false;
const dev_rush_build_package = false;

const emitterName = '@azure-tools/typespec-ts';
const comment = '# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.\n\n';
// TODO: remove it after we generate and use options by ourselves
const messageToTspConfigSample =
    'Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.';

async function loadTspConfig(typeSpecDirectory: string): Promise<Exclude<any, null | undefined>> {
    const configPath = join(typeSpecDirectory, 'tspconfig.yaml');
    const content = await readFile(configPath, { encoding: 'utf-8' });
    console.log('content', content.toString());
    const config = parse(content.toString());
    if (!config) {
        throw new Error(`Failed to parse tspconfig.yaml in ${typeSpecDirectory}`);
    }
    return config;
}

// TODO
async function buildPackage(generatedPackageDirectory: string, options: ModularClientPackageOptions) {
    logger.logInfo(`Building package in ${generatedPackageDirectory}.`);
    const { name } = await getNpmPackageInfo(generatedPackageDirectory);
    await updateRushJson({
        packageName: name,
        projectFolder: generatedPackageDirectory,
        versionPolicyName: options.versionPolicyName
    });
    if (!dev_rush_build_package) {
        return;
    }
    execSync(`rush update`, execOptions);
    logger.logInfo(`Rush update successfully.`);
    execSync(`rush build -t ${name}`);
    logger.logInfo(`Build package "${name}" successfully.`);
}

async function createOrUpdateCiFiles(
    generatedPackageDirectory: string,
    options: ModularClientPackageOptions,
    npmPackageInfo: NpmPackageInfo
) {
    logger.logInfo('Generating CI files');
    switch (options.versionPolicyName) {
        case 'management':
            await createOrUpdateMPGCiFiles(generatedPackageDirectory, npmPackageInfo);
            logger.logInfo('Generated management plane CI files successfully.');
            return;
        case 'client':
            await createOrUpdateDPGCiFiles(generatedPackageDirectory, npmPackageInfo);
            logger.logInfo('Generated data plane CI files successfully.');
            return;
        default:
            throw new Error(`Unsupported version policy name: ${options.versionPolicyName}`);
    }
}

async function createOrUpdateMPGCiFiles(
    generatedPackageDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const serviceDir = posix.join(generatedPackageDirectory, '..');
    const ciMgmtPath = posix.join(serviceDir, 'ci.mgmt.yml');
    const serviceDirRelativeToSDKDir = basename(serviceDir);

    if (!existsSync(ciMgmtPath)) {
        await createMPGCiFiles(generatedPackageDirectory, ciMgmtPath, serviceDirRelativeToSDKDir, npmPackageInfo);
        return;
    }
    await updateMPGCiFiles(generatedPackageDirectory, ciMgmtPath, serviceDirRelativeToSDKDir, npmPackageInfo);
}

function tryAddPathInCiYaml(includePaths: any, path: string): boolean {
    const paths = includePaths as string[];
    let needUpdate = false;
    if (paths?.includes(path) !== true) {
        needUpdate = true;
        paths.push(path);
    }
    needUpdate ||= needUpdate;
    return needUpdate;
}

function tryAddArtifact(): boolean {}

// TODO: add check to existing CI yaml, may lost many config: like exclude branch...
async function updateMPGCiFiles(
    generatedPackageDirectory: string,
    ciMgmtPath: string,
    serviceDirRelativeToSDKDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const content = await readFile(ciMgmtPath, { encoding: 'utf-8' });
    let parsed = parse(content.toString());
    let needUpdate = false;
    needUpdate ||= tryAddPathInCiYaml(parsed.trigger.paths.include, generatedPackageDirectory);
    needUpdate ||= tryAddPathInCiYaml(parsed.pr.paths.include, generatedPackageDirectory);
}

async function createMPGCiFiles(
    generatedPackageDirectory: string,
    ciMgmtPath: string,
    serviceDirRelativeToSDKDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    const artifactName = npmPackageInfo.name.replace('@azure/', 'azure-');
    const artifactSafeName = artifactName.replace(/-/g, '');

    const templatePath = join(__dirname, 'ciYamlTemplates/ci.mgmt.template.yml');
    const template = await readFile(templatePath, { encoding: 'utf-8' });
    const parsed = parse(template.toString());
    parsed.trigger.paths.include = [generatedPackageDirectory, ciMgmtPath];
    parsed.pr.paths.include = [generatedPackageDirectory, ciMgmtPath];
    parsed.extends.parameters.ServiceDirectory = serviceDirRelativeToSDKDirectory;
    parsed.extends.parameters.Artifacts = [{ name: artifactName, safeName: artifactSafeName }];

    const content = comment + stringify(parsed);
    await writeFile(ciMgmtPath, content);
    return;
}

async function createOrUpdateDPGCiFiles(
    generatedPackageDirectory: string,
    npmPackageInfo: NpmPackageInfo
): Promise<void> {
    throw new Error('Not implemented function');
}

async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions) {
    const command = `pwsh ./eng/common/scripts/TypeSpec-Project-Process.ps1 ${options.typeSpecDirectory} ${options.gitCommitId} ${options.repoUrl}`;
    execSync(command, execOptions);
    logger.logInfo(`Generated typescript code successfully.`);
}

// generated path is in posix format
// e.g. sdk/mongocluster/arm-mongocluster
async function getGeneratedPackageDirectory(typeSpecDirectory: string): Promise<string> {
    const tspConfig = await loadTspConfig(typeSpecDirectory);
    const serviceDir = tspConfig.parameters?.['service-dir']?.default;
    if (!serviceDir) {
        throw new Error(`Misses service-dir in parameters section of tspconfig.yaml. ${messageToTspConfigSample}`);
    }
    const packageDir = tspConfig.options?.[emitterName]?.['package-dir'];
    if (!packageDir) {
        throw new Error(`Misses package-dir in ${emitterName} options of tspconfig.yaml. ${messageToTspConfigSample}`);
    }
    const packageDirFromRoot = posix.join(serviceDir, packageDir);
    return packageDirFromRoot;
}

async function generatePackageInfo(
    generatedPackageDir: string,
    changelog: Changelog | undefined,
    npmPackageInfo: NpmPackageInfo
): Promise<GeneratedPackageInfo> {
    const changelogInfo: ChangelogInfo | undefined = {
        content: changelog.displayChangeLog(),
        hasBreakingChange: changelog.hasBreakingChange
    };

    const packageInfo: GeneratedPackageInfo = {
        packageName: npmPackageInfo.name,
        version: npmPackageInfo.version,
        path: ['rush.json', 'common/config/rush/pnpm-lock.yaml'],
        artifacts: [],
        changelog: changelogInfo,
        result: 'succeeded' // TODO: consider when is failed
    };
    return packageInfo;
}

interface ProjectItem {
    packageName: string;
    projectFolder: string;
    versionPolicyName: string;
}

async function updateRushJson(projectItem: ProjectItem) {
    const content = await readFile('rush.json', { encoding: 'utf-8' });
    const rushJson = parseCommentJson(content.toString());
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

interface NpmPackageInfo {
    name: string;
    version: string;
}

async function getNpmPackageInfo(generatedPackageDir): Promise<NpmPackageInfo> {
    const packageJson = await load(generatedPackageDir);
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

// !!!IMPORTANT:
// this function should be used ONLY in the CodeGen pipeline of azure-rest-api-specs pull request for generating packages in azure-sdk-for-js
// it has extra steps to generate a azure sdk package (no modular client's doc for now, use RLC's for now):
// https://github.com/Azure/azure-sdk-for-js/blob/main/documentation/steps-after-generations.md
export async function generateAzureSDKPackage(options: ModularClientPackageOptions): Promise<GeneratedPackageInfo> {
    logger.logInfo(`Start to generate modular client package for azure-sdk-for-js.`);

    // TODO: check if clean last generation

    if (dev_generate_ts_code) {
        await generateTypeScriptCodeFromTypeSpec(options);
    }

    const generatedPackageDir = await getGeneratedPackageDirectory(options.typeSpecDirectory);
    await buildPackage(generatedPackageDir, options);

    // changelog generation will compute package version and bump it in package.json
    const changelog = await generateChangelogAndBumpVersion(generatedPackageDir);
    const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);

    // TODO: add CI files
    await createOrUpdateCiFiles(generatedPackageDir, options, npmPackageInfo);

    // TODO: skip build test and sample

    // TODO: modify packageInfo
    const packageInfo = generatePackageInfo(generatedPackageDir, changelog, npmPackageInfo);
    return packageInfo;
}

// // step 2: build package to generate required files. e.g apiViews
// const generatedPackageDir = await getGeneratedPackageDirectory(options.typeSpecDirectory);
// await buildPackage(generatedPackageDir, options);

// // step 3: generate CHANGELOG.md and dump version in package.json
// const changelog = await generateChangelogAndBumpVersion(generatedPackageDir);
// logger.logInfo(`Generated changelog successfully.`);

// // step 4: get npm package config
// const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);
