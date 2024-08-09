import { ModularClientPackageOptions } from '../../../common/types';
import { runCommand, runCommandOptions } from '../../../common/utils';
import { logger } from '../../../utils/logger';
import { join, posix } from 'node:path';
import { readFile } from 'node:fs/promises';
import { parse } from 'yaml';

// ./eng/common/scripts/TypeSpec-Project-Process.ps1 script forces to use emitter '@azure-tools/typespec-ts',
// so do NOT change the emitter
const emitterName = '@azure-tools/typespec-ts';

// TODO: remove it after we generate and use options by ourselves
const messageToTspConfigSample =
    'Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.';

export async function loadTspConfig(typeSpecDirectory: string): Promise<Exclude<any, null | undefined>> {
    const configPath = join(typeSpecDirectory, 'tspconfig.yaml');
    const content = await readFile(configPath, { encoding: 'utf-8' });
    console.log('content', content.toString());
    const config = parse(content.toString());
    if (!config) {
        throw new Error(`Failed to parse tspconfig.yaml in ${typeSpecDirectory}`);
    }
    return config;
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

// ./eng/common/scripts/TypeSpec-Project-Process.ps1 script forces to use emitter '@azure-tools/typespec-ts'
export async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions): Promise<string> {
    const script = './eng/common/scripts/TypeSpec-Project-Process.ps1';
    try {
        await runCommand(
            'pwsh',
            [script, options.typeSpecDirectory, options.gitCommitId, options.repoUrl],
            runCommandOptions
        );
        logger.logInfo(`Generated typescript code successfully.`);

        return getGeneratedPackageDirectory(options.typeSpecDirectory);
    } catch (err) {
        logger.logError(`Run command failed due to: ${(err as Error)?.stack ?? err}`);
        throw err;
    }
}
