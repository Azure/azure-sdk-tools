import { join } from 'path';
import { ModularClientPackageOptions } from '../../../common/types.js';
import { runCommand, updateApiVersionInTspConfig } from '../../../common/utils.js';
import { logger } from '../../../utils/logger.js';
import pkg from '@npmcli/package-json';
const { load } = pkg;

export async function updatePackageVersion(packageDirectory: string, version: string): Promise<void> {
    const packageJson = await load(packageDirectory);
    packageJson.content.version = version;
    await packageJson.save();
}

export async function generateTypeScriptCodeFromTypeSpec(
    options: ModularClientPackageOptions,
    originalVersion: string | undefined,
    packageDirectory: string
): Promise<void> {    
    const tspConfigPath = join(options.typeSpecDirectory, 'tspconfig.yaml');
    logger.info(`mlc:tspConfigPath: ${tspConfigPath}`);   
    updateApiVersionInTspConfig(tspConfigPath, options.apiVersion);
    
    logger.info('Start to generate code by tsp-client.');
    await runCommand(
        'tsp-client',
        [
            'init',
            '--debug',
            '--tsp-config',
            tspConfigPath,
            '--local-spec-repo',
            options.typeSpecDirectory,
            '--repo',
            options.specRepoRoot,
            '--commit',
            options.gitCommitId
        ],
        { shell: true, stdio: 'inherit' },
        false
    );

    if (originalVersion) await updatePackageVersion(packageDirectory, originalVersion);
    logger.info(`Generated typescript code successfully.`);
}
