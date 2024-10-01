import shell from 'shelljs';
import path, { join, posix } from 'path';
import fs from 'fs';
import { SDKType } from './types';
import { logger } from '../utils/logger';
import { Project, ScriptTarget, SourceFile } from 'ts-morph';
import { readFile } from 'fs/promises';
import { parse } from 'yaml';
import { access } from 'node:fs/promises';
import { SpawnOptions, spawn } from 'child_process';

// ./eng/common/scripts/TypeSpec-Project-Process.ps1 script forces to use emitter '@azure-tools/typespec-ts',
// so do NOT change the emitter
const emitterName = '@azure-tools/typespec-ts';

// 1 hour in milliseconds unit
export const defaultChildProcessTimeout = 60 * 60 * 1000;

// TODO: remove it after we generate and use options by ourselves
const messageToTspConfigSample =
    'Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.';

const errorKeywordsInLowercase = new Set<string>(['error', 'err_pnpm_no_matching_version']);

function removeLastNewline(line: string): string {
    return line.replace(/\n$/, '')
}

function replaceAll(original: string, from: string, to: string) { 
    return original.split(from).join(to);
}

function printErrorDetails(
    output: { stdout: string; stderr: string; code: number | null } | undefined,
    printDetails: boolean = false
) {
    if (!output) return;
    const getErrorSummary = (content: string) =>
        content
            .split('\n')
            .filter((line) => {
                for (const keyword of errorKeywordsInLowercase) {
                    if (line.toLowerCase().includes(keyword)) return true;
                }
                return false;
            })
            .map((line) => `  ${line}\n`);
    let summary = [...getErrorSummary(output.stderr), ...getErrorSummary(output.stdout)];
    logger.error(`Exit code: ${output.code}`);
    if (summary.length > 0) {
    logger.error(`Summary:`);
        summary.forEach((line) => logger.error(removeLastNewline(line)));
    }
    if (printDetails) {
        const stderr = removeLastNewline(output.stderr);
        const stdout = removeLastNewline(output.stdout);
        logger.error(`Details:`);
        if (stderr) {
            logger.error(`  stderr:`);
            stderr.split('\n').forEach((line) => logger.warn(`    ${line}`));
        }
        if (stdout) {
            logger.error(`  stdout:`);
            stdout.split('\n').forEach((line) => logger.warn(`    ${line}`));
        }
    }
}

export const runCommandOptions: SpawnOptions = { shell: true, stdio: ['pipe', 'pipe', 'pipe'] };

export function getClassicClientParametersPath(packageRoot: string): string {
    return path.join(packageRoot, 'src', 'models', 'parameters.ts');
}

export function getSDKType(packageRoot: string): SDKType {
    const paraPath = getClassicClientParametersPath(packageRoot);
    const packageName = getNpmPackageName(packageRoot);
    if (packageName.startsWith('@azure-rest/')) {
        return SDKType.RestLevelClient;
    }
    const exist = shell.test('-e', paraPath);
    const type = exist ? SDKType.HighLevelClient : SDKType.ModularClient;
    logger.info(`SDK type '${type}' is detected in '${packageRoot}'.`);
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

export function tryReadNpmPackageChangelog(changelogPath: string): string {
    try {
        if (!fs.existsSync(changelogPath)) {
            logger.warn(`NPM package's changelog '${changelogPath}' does not exist.`);
            return "";
        }
        const originalChangeLogContent = fs.readFileSync(changelogPath, { encoding: 'utf-8' });
        return originalChangeLogContent;
    } catch (err) {
        logger.warn(`Failed to read NPM package's changelog '${changelogPath}': ${(err as Error)?.stack ?? err}`);
        return '';
    }
}

export async function loadTspConfig(typeSpecDirectory: string): Promise<Exclude<any, null | undefined>> {
    const configPath = join(typeSpecDirectory, 'tspconfig.yaml');
    const content = await readFile(configPath, { encoding: 'utf-8' });
    const config = parse(content.toString());
    if (!config) {
        throw new Error(`Failed to parse tspconfig.yaml in ${typeSpecDirectory}`);
    }
    return config;
}

// generated path is in posix format
// e.g. sdk/mongocluster/arm-mongocluster
export async function getGeneratedPackageDirectory(typeSpecDirectory: string, sdkRepoRoot: string): Promise<string> {
    const tspConfig = await loadTspConfig(typeSpecDirectory);
    const serviceDir = tspConfig.parameters?.['service-dir']?.default;
    if (!serviceDir) {
        throw new Error(`Miss service-dir in parameters section of tspconfig.yaml. ${messageToTspConfigSample}`);
    }
    const packageDir = tspConfig.options?.[emitterName]?.['package-dir'];
    if (!packageDir) {
        throw new Error(`Miss package-dir in ${emitterName} options of tspconfig.yaml. ${messageToTspConfigSample}`);
    }
    const packageDirFromRoot = posix.join(sdkRepoRoot, serviceDir, packageDir);
    return packageDirFromRoot;
}


export async function runCommand(
    command: string,
    args: readonly string[],
    options: SpawnOptions = runCommandOptions,
    realtimeOutput: boolean = true,
    timeoutSeconds: number | undefined = undefined
): Promise<{ stdout: string; stderr: string; code: number | null }> {
    let stdout = '';
    let stderr = '';
    const commandStr = `${command} ${args.join(' ')}`;
    logger.info(`Start to run command: '${commandStr}'.`);
    const child = spawn(command, args, options);

    let timedOut = false;
    const timer =
        timeoutSeconds &&
        setTimeout(() => {
            timedOut = true;
            child.kill();
            throw new Error(`Process timed out after ${timeoutSeconds}s`);
        }, timeoutSeconds * 1000);

    child.stdout?.setEncoding('utf8');
    child.stderr?.setEncoding('utf8');
    
    child.stdout?.on('data', (data) => {
        const str = data.toString();
        stdout += str;
        if (realtimeOutput) logger.info(str);
    });

    child.stderr?.on('data', (data) => {
        const str = data.toString();
        stderr += str;
        if (realtimeOutput) logger.warn(str);
    });

    let resolve: (value: void | PromiseLike<void>) => void;
    let reject: (reason?: any) => void;
    const promise = new Promise<void>((res, rej) => {
        resolve = res;
        reject = rej;
    });
    let code: number | null = 0;
    
    child.on('exit', (exitCode, signal) => {
        if (timer) clearTimeout(timer);
        if (timedOut || !signal) { return; }
        logger.error(`Command '${commandStr}' exited with signal '${signal ?? 'SIGTERM'}' and code ${exitCode}.`);
    });

    child.on('close', (exitCode) => {
        if (exitCode === 0) {
            resolve();
            logger.info(`Command '${commandStr}' closed with code '${exitCode}'.`);
            return;
        }
        code = exitCode;
        logger.error(`Command closed with code '${exitCode}'.`);
        printErrorDetails({ stdout, stderr, code: exitCode }, !realtimeOutput);
        reject(Error(`Command closed with code '${exitCode}'.`));
    });
    
    child.on('error', (err) => {
        logger.error((err as Error)?.stack ?? err);
        printErrorDetails({ stdout, stderr, code: null }, !realtimeOutput);
        reject(err);
    });

    await promise;
    return {stdout, stderr, code};
}

export async function existsAsync(path: string): Promise<boolean> {
    try {
        await access(path);
        return true;
    } catch (error) {
        logger.warn(`Fail to find ${path} for error: ${error}`);
        return false;
    }
}
