import { join } from 'path';
import { ModularClientPackageOptions } from '../../../common/types';
import { getGeneratedPackageDirectory, runCommand, runCommandOptions } from '../../../common/utils';
import { logger } from '../../../utils/logger';

export async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions): Promise<string> {
    const tspConfigPath = join(options.typeSpecDirectory, 'tspconfig.yaml');
    try {
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
        );
        logger.info(`Generated typescript code successfully.`);

        return getGeneratedPackageDirectory(options.typeSpecDirectory, options.sdkRepoRoot);
    } catch (err) {
        logger.error(`Failed to run command due to: ${(err as Error)?.stack ?? err}`);
        throw err;
    }
}
