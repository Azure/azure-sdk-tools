import { PackageResult, ModularClientPackageOptions } from '../../common/types';
import { logger } from '../../utils/logger';
import { generateChangelogAndBumpVersion } from '../changlog/generateChangelog';
import { createOrUpdateCiYaml } from '../../common/ciYamlUtils';
import { getNpmPackageInfo } from '../../common/npmUtils';
import { buildPackage, tryBuildSamples, createArtifact, tryTestPackage } from '../../common/rushUtils';
import { initPackageResult, updateChangelogResult, updateNpmPackageResult } from '../../common/packageResultUtils';
import { generateTypeScriptCodeFromTypeSpec } from './utils/typeSpecUtils';
import { remove } from 'fs-extra';
import { getGeneratedPackageDirectory } from '../../common/utils';

// !!!IMPORTANT:
// this function should be used ONLY in
//   1. the CodeGen pipeline of azure-rest-api-specs pull request for generating packages in azure-sdk-for-js
//   2. in the root directory of azure-sdk-for-js repo
// it has extra steps to generate a releasable azure sdk package (no modular client's doc for now, use RLC's for now) after typescript code is generate:
// https://github.com/Azure/azure-sdk-for-js/blob/main/documentation/steps-after-generations.md
export async function generateAzureSDKPackage(options: ModularClientPackageOptions): Promise<PackageResult> {
    logger.logInfo(`Start generating modular client package for azure-sdk-for-js.`);
    const packageResult = initPackageResult();
    try {
        const packageDirectory = await getGeneratedPackageDirectory(options.typeSpecDirectory);
        await remove(packageDirectory);

        const generatedPackageDir = await generateTypeScriptCodeFromTypeSpec(options);

        await buildPackage(generatedPackageDir, options.versionPolicyName, packageResult);

        // changelog generation will compute package version and bump it in package.json,
        // so changelog generation should be put before any task needs package.json's version,
        // TODO: consider to decouple version bump and changelog generation
        const changelog = await generateChangelogAndBumpVersion(generatedPackageDir);
        updateChangelogResult(packageResult, changelog);

        // build sample and test package will NOT throw exceptions
        await tryBuildSamples(generatedPackageDir);
        await tryTestPackage(generatedPackageDir);

        const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);
        updateNpmPackageResult(packageResult, npmPackageInfo, options.typeSpecDirectory, generatedPackageDir);

        const artifactPath = await createArtifact(generatedPackageDir);
        packageResult.artifacts.push(artifactPath);

        const ciYamlPath = await createOrUpdateCiYaml(generatedPackageDir, options.versionPolicyName, npmPackageInfo);
        packageResult.path.push(ciYamlPath);

        packageResult.result = 'succeeded';
        logger.logInfo(`Generated package successfully.`);
        logger.logInfo(`Package summary: ${JSON.stringify(packageResult, undefined, 2)}`);
    } catch (err) {
        packageResult.result = 'failed';
        logger.logError(`Failed to generate package due to ${(err as Error).stack ?? err}`);
        throw err;
    } finally {
        return packageResult;
    }
}
