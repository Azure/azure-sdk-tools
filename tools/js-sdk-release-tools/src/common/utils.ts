import shell from 'shelljs';
import path from 'path';
import fs from 'fs';
import { spawn } from 'child_process';
import { SDKType } from './types';
import { logger } from '../utils/logger';
import { Project, ScriptTarget, SourceFile } from 'ts-morph';
import { replaceAll } from '@ts-common/azure-js-dev-tools';
import { access } from 'node:fs/promises';
import { SpawnOptions } from 'child_process';

export const runCommandOptions: SpawnOptions = { shell: true, stdio: ['inherit', 'pipe', 'pipe'] };

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
            const packageName = npmPackageName.substring('@azure/'.length);
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
    content = replaceAll(content, '**Breaking Changes**', '### Breaking Changes')!;
    content = replaceAll(content, '**Bugs Fixed**', '### Bugs Fixed')!;
    content = replaceAll(content, '**Other Changes**', '### Other Changes')!;
    return content;
}

export function tryReadNpmPackageChangelog(packageFolderPath: string): string {
    const changelogPath = path.join(packageFolderPath, 'changelog-temp', 'package', 'CHANGELOG.md');
    try {
        if (!fs.existsSync(changelogPath)) {
            logger.logWarn(`NPM package's changelog "${changelogPath}" does not exists`);
            return '';
        }
        const originalChangeLogContent = fs.readFileSync(changelogPath, { encoding: 'utf-8' });
        return originalChangeLogContent;
    } catch (err) {
        logger.logWarn(`Failed to read NPM package's changelog "${changelogPath}": ${(err as Error)?.stack ?? err}`);
        return '';
    }
}

export async function existsAsync(path: string): Promise<boolean> {
    try {
        await access(path);
        return true;
    } catch (error) {
        logger.logWarn(`Fail to find ${path} for error: ${error}`);
        return false;
    }
}

export function runCommand(
    command: string,
    args: readonly string[],
    options: SpawnOptions,
    realtimeOutput: boolean = true
): Promise<{ stdout: string; stderr: string }> {
    return new Promise((resolve, reject) => {
        let stdout = '';
        let stderr = '';
        const child = spawn(command, args, options);
        child.stdout?.on('data', (data) => {
            const str = data.toString();
            stdout += str;
            if (realtimeOutput) console.log(str);
        });

        child.stderr?.on('data', (data) => {
            const str = data.toString();
            stderr += str;
            if (realtimeOutput) console.error(str);
        });

        child.on('close', (code) => {
            if (code === 0) {
                resolve({ stdout, stderr });
            } else {
                console.log(`run command exit: ${code}`);
                reject(new Error(`run command exit: ${code}`));
            }
        });

        child.on('error', (err) => {
            console.log('run command err', err);
            reject(err);
        });
    });
}
