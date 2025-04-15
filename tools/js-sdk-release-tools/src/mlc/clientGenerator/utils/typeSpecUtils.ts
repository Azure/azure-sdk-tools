import { join } from 'path';
import { ModularClientPackageOptions } from '../../../common/types.js';
import { getGeneratedPackageDirectory, runCommand, runCommandOptions } from '../../../common/utils.js';
import { logger } from '../../../utils/logger.js';
import pkg from '@npmcli/package-json';
const { load } = pkg;
import * as fs from 'fs';
import { dump, load as yamlLoad } from 'js-yaml';

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
    console.log(`tspConfigPath: ${tspConfigPath}`);
    // update tspconfig first.      
    updateApiVersionInTspConfig(tspConfigPath, options.apiVersion);
    //process.exit(1);
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

function updateApiVersionInTspConfig(tspConfigPath: string, apiVersion: string | undefined) {

    if (!apiVersion) {
        logger.warn('No API version provided. Skipping tspconfig update.');
        return;
    }

    if (!fs.existsSync(tspConfigPath)) {
        throw new Error(`tspconfig.yaml not found at path: ${tspConfigPath}`);
    }

    const tspConfigContent = fs.readFileSync(tspConfigPath, 'utf8');
    const tspConfig = yamlLoad(tspConfigContent);

    if (typeof tspConfig !== 'object' || tspConfig === null) {
        throw new Error('Invalid tspconfig.yaml format.');
    }

    tspConfig['options']['@azure-tools/typespec-ts']['api-version'] = apiVersion;

    const updatedTspConfigContent = dump(tspConfig);
    fs.writeFileSync(tspConfigPath, updatedTspConfigContent, 'utf8');
    logger.info(`Updated API version to ${apiVersion} in tspconfig.yaml.`);
}
