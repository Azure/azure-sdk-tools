import { join } from 'path';
import { ModularClientPackageOptions } from '../../../common/types';
import { getGeneratedPackageDirectory, runCommand, runCommandOptions } from '../../../common/utils';
import { logger } from '../../../utils/logger';
import { load } from '@npmcli/package-json';

export async function updatePackageVersion(packageDirectory: string, version: string): Promise<void> {
    const packageJson = await load(packageDirectory);
    packageJson.content.version = version;
    packageJson.save();
}

export async function generateTypeScriptCodeFromTypeSpec(
    options: ModularClientPackageOptions,
    originalVersion: string | undefined,
    packageDirectory: string
): Promise<void> {
    const tspConfigPath = join(options.typeSpecDirectory, 'tspconfig.yaml');
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
