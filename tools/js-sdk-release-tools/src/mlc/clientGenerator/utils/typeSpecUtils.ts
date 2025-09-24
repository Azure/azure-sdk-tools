import { join } from 'path';
import { ModularClientPackageOptions } from '../../../common/types.js';
import { generateRepoDataInTspLocation, runCommand } from '../../../common/utils.js';
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
    logger.info('Start to generate code by tsp-client.');
    const repoUrl = generateRepoDataInTspLocation(options.repoUrl);
    const tspClientDir = join(process.cwd(), 'eng', 'common', 'tsp-client');
    
    const currentDir = process.cwd();
    logger.info(`Changing directory to: ${tspClientDir}`);
    process.chdir(tspClientDir);
    
    await runCommand(
        'npm exec --no -- tsp-client',
        [
            'init',
            '--debug',
            '--tsp-config',
            tspConfigPath,
            '--local-spec-repo',
            options.typeSpecDirectory,
            '--repo',
            repoUrl,
            '--commit',
            options.gitCommitId
        ],
        { shell: true, stdio: 'inherit' },
        false
    );
    
    logger.info(`Changing back to original directory: ${currentDir}`);
    process.chdir(currentDir);

    if (originalVersion) await updatePackageVersion(packageDirectory, originalVersion);
    logger.info(`Generated typescript code successfully.`);
}
