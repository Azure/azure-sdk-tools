import { ModularClientPackageOptions } from '../../../common/types';
import { logger } from '../../../utils/logger';
import { join, posix } from 'node:path';
import { readFile } from 'node:fs/promises';
import { parse } from 'yaml';
import { runCommand, runCommandOptions } from '../../../common/utils';

// TODO: remove
// only used for local debugging
const dev_generate_ts_code = false;

const emitterName = '@azure-tools/typespec-ts';
// TODO: remove it after we generate and use options by ourselves
const messageToTspConfigSample =
    'Please refer to https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml for the right schema.';

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

export async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions) {
    if (!dev_generate_ts_code) {
        return;
    }
    await runCommand(
        'pwsh',
        [
            './eng/common/scripts/TypeSpec-Project-Process.ps1',
            options.typeSpecDirectory,
            options.gitCommitId,
            options.repoUrl
        ],
        runCommandOptions
    );
    logger.logInfo(`Generated typescript code successfully.`);
}
