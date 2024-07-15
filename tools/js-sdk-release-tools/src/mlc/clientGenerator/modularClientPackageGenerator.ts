import { readFile, writeFile} from 'node:fs/promises';
import { parse } from 'yaml';
import { join } from 'path';
import { execSync } from 'child_process';
import { GeneratedPackageInfo, ModularClientPackageOptions, ChangelogInfo } from '../../common/types';
import { Changelog } from '../../changelog/changelogGenerator';
import { logger } from '../../utils/logger';
import { load } from '@npmcli/package-json';
import { generateChangelogAndBumpVersion } from '../changlog/generateChangelog';
import {
    CommentArray,
    CommentJSONValue,
    CommentObject,
    parse as parseCommentJson,
    assign,
    stringify
} from 'comment-json';
import { execOptions } from '../../common/utils';
import { posix } from 'node:path';
import { existsSync } from 'node:fs';

// TODO: remove
// only used for local debugging
const dev_generate_ts_code = false;
const dev_rush_build_package = false;

const emitterName = '@azure-tools/typespec-ts';
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
    logger.log(`Rush update successfully.`);
    execSync(`rush build -t ${name}`);
    logger.log(`Build package "${name}" successfully.`);
}

async function generateCiFiles(generatedPackageDirectory: string, options: ModularClientPackageOptions) {
    switch (options.versionPolicyName) {
        case 'management':
            return generateManagementPlaneCiFiles(generatedPackageDirectory);
        case 'client':
            return generateDataPlaneCiFiles(generatedPackageDirectory);
        default:
            throw new Error(`Unsupported version policy name: &{options.versionPolicyName}`);
    }
}

async function generateManagementPlaneCiFiles(generatedPackageDirectory: string) {
    const serviceDir = join(generatedPackageDirectory, '..');
    const ciMgmtPath = join(serviceDir, 'ci.mgmt.yml')
    if (!existsSync(ciMgmtPath)) {
        const template = await readFile(join(__dirname, 'ciYamlTemplates/ci.mgmt.template.yml'), {encoding: 'utf-8'})
        const parsed = parse(template.toString());
        parsed.trigger.paths.include = []
        parsed.pr.paths.include = []
        parsed.extends.parameters.ServiceDirectory = "";
        parsed.extends.parameters.Artifacts.name = "";
        parsed.extends.parameters.Artifacts.safeName = "";
        return;
    }
    // TODO
}

async function generateDataPlaneCiFiles(generatedPackageDirectory: string) {
    throw new Error('Not implemented function');
}

async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions) {
    const command = `pwsh ./eng/common/scripts/TypeSpec-Project-Process.ps1 ${options.typeSpecDirectory} ${options.gitCommitId} ${options.repoUrl}`;
    execSync(command, execOptions);
    logger.logInfo('Generated modular client successfully.');
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
    logger.logInfo(`Generated typescript code successfully.`);

    const generatedPackageDir = await getGeneratedPackageDirectory(options.typeSpecDirectory);
    await buildPackage(generatedPackageDir, options);
    logger.logInfo(`build package successfully.`);

    // changelog generation will compute package version and bump it in package.json
    const changelog = await generateChangelogAndBumpVersion(generatedPackageDir);
    const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);
    logger.logInfo(`Generated changelog successfully.`);

    // TODO: add CI files
    await generateCiFiles(generatedPackageDir, options);

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
