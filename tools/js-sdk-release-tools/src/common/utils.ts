import shell from 'shelljs';
import path, { join, posix } from 'path';
import fs from 'fs';

import { SDKType } from './types'
import { logger } from "../utils/logger";
import { Project, ScriptTarget, SourceFile } from 'ts-morph';
import { replaceAll } from '@ts-common/azure-js-dev-tools';
import { readFile } from 'fs/promises';
import { parse } from 'yaml';

// ./eng/common/scripts/TypeSpec-Project-Process.ps1 script forces to use emitter '@azure-tools/typespec-ts',
// so do NOT change the emitter
const emitterName = '@azure-tools/typespec-ts';

// TODO: remove it after we generate and use options by ourselves
const messageToTspConfigSample =
    'Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.';

async function loadTspConfig(typeSpecDirectory: string): Promise<Exclude<any, null | undefined>> {
    const configPath = join(typeSpecDirectory, 'tspconfig.yaml');
    const content = await readFile(configPath, { encoding: 'utf-8' });
    const config = parse(content.toString());
    if (!config) {
        throw new Error(`Failed to parse tspconfig.yaml in ${typeSpecDirectory}`);
    }
    return config;
}

export function getClassicClientParametersPath(packageRoot: string): string {
    return path.join(packageRoot, 'src', 'models', 'parameters.ts');
}

export function getSDKType(packageRoot: string): SDKType {
    const paraPath = getClassicClientParametersPath(packageRoot);
    const exist = shell.test('-e', paraPath);
    const type = exist ? SDKType.HighLevelClient : SDKType.ModularClient;
    logger.logInfo(`SDK type: ${type} detected in ${packageRoot}`);
    return type;
}

export function getNpmPackageName(packageRoot: string): string {
    const packageJsonPath = path.join(packageRoot, 'package.json');
    const packageJson = fs.readFileSync(packageJsonPath, { encoding: 'utf-8' });
    const packageName = JSON.parse(packageJson).name;
    return packageName;
}

export function getApiReviewPath(packageRoot: string): string {
    const sdkType = getSDKType(packageRoot);
    const reviewDir = path.join(packageRoot, 'review');
    switch (sdkType) {
        case SDKType.ModularClient:
            const npmPackageName = getNpmPackageName(packageRoot);
            const packageName = npmPackageName.substring("@azure/".length);
            const apiViewFileName = `${packageName}.api.md`;
            return path.join(packageRoot, 'review', apiViewFileName);
        case SDKType.HighLevelClient:
        case SDKType.RestLevelClient:
        default:
            // only one xxx.api.md
            return path.join(packageRoot, 'review', fs.readdirSync(reviewDir)[0]);
    }
}

export function getTsSourceFile(filePath: string): SourceFile | undefined {
    const target = ScriptTarget.ES2015;
    const compilerOptions = { target };
    const project = new Project({ compilerOptions });
    project.addSourceFileAtPath(filePath);
    return project.getSourceFile(filePath);
}

// changelog policy: https://aka.ms/azsdk/guideline/changelogs
export function fixChangelogFormat(content: string) {
    content = replaceAll(content, '**Features**', '### Features Added')!;
    content  = replaceAll(content, '**Breaking Changes**', '### Breaking Changes')!;
    content  = replaceAll(content, '**Bugs Fixed**', '### Bugs Fixed')!;
    content  = replaceAll(content, '**Other Changes**', '### Other Changes')!;
    return content;
}

export function tryReadNpmPackageChangelog(packageFolderPath: string): string {
    const changelogPath = path.join(packageFolderPath, 'changelog-temp', 'package', 'CHANGELOG.md');
    try {
        if (!fs.existsSync(changelogPath)) {
            logger.logWarn(`NPM package's changelog "${changelogPath}" does not exists`);
            return "";
        }
        const originalChangeLogContent = fs.readFileSync(changelogPath, { encoding: 'utf-8' });
        return originalChangeLogContent;
    } catch (err) {
        logger.logWarn(`Failed to read NPM package's changelog "${changelogPath}": ${(err as Error)?.stack ?? err}`);
        return '';
    }
}

// generated path is in posix format
// e.g. sdk/mongocluster/arm-mongocluster
export async function getGeneratedPackageDirectory(typeSpecDirectory: string): Promise<string> {
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
