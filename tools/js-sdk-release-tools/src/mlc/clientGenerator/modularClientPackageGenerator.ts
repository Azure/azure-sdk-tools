import { ModularClientPackageOptions, PackageResult } from '../../common/types';
import { buildPackage, createArtifact, tryBuildSamples, tryTestPackage } from '../../common/rushUtils';
import { initPackageResult, updateChangelogResult, updateNpmPackageResult } from '../../common/packageResultUtils';
import { normalize, posix, relative } from 'node:path';

import { createOrUpdateCiYaml } from '../../common/ciYamlUtils';
import { generateChangelogAndBumpVersion } from '../changlog/generateChangelog';
import { generateTypeScriptCodeFromTypeSpec } from './utils/typeSpecUtils';
import { getGeneratedPackageDirectory } from '../../common/utils';
import { getNpmPackageInfo } from '../../common/npmUtils';
import { logger } from '../../utils/logger';
import { remove } from 'fs-extra';
import unixify from 'unixify';

// !!!IMPORTANT:
// this function should be used ONLY in
//   1. the CodeGen pipeline of azure-rest-api-specs pull request for generating packages in azure-sdk-for-js
//   2. in the root directory of azure-sdk-for-js repo
// it has extra steps to generate a releasable azure sdk package (no modular client's doc for now, use RLC's for now) after typescript code is generate:
// https://github.com/Azure/azure-sdk-for-js/blob/main/documentation/steps-after-generations.md
export async function generateAzureSDKPackage(options: ModularClientPackageOptions): Promise<PackageResult> {
    logger.info(`Start to generate modular client package for azure-sdk-for-js.`);
    const packageResult = initPackageResult();
    try {
        const packageDirectory = await getGeneratedPackageDirectory(options.typeSpecDirectory, options.sdkRepoRoot);
        await remove(packageDirectory);

        const generatedPackageDir = await generateTypeScriptCodeFromTypeSpec(options);
        const relativePackageDirToSdkRoot = relative(normalize(options.sdkRepoRoot), normalize(generatedPackageDir));

        await buildPackage(relativePackageDirToSdkRoot, options.versionPolicyName, packageResult);

        // changelog generation will compute package version and bump it in package.json,
        // so changelog generation should be put before any task needs package.json's version,
        // TODO: consider to decouple version bump and changelog generation
        // TODO: to be compatible with current tool, input relative generated package dir
        const changelog = await generateChangelogAndBumpVersion(relativePackageDirToSdkRoot);
        updateChangelogResult(packageResult, changelog);

        // build sample and test package will NOT throw exceptions
        await tryBuildSamples(generatedPackageDir);
        await tryTestPackage(generatedPackageDir);

        const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);
        const relativeTypeSpecDirToSpecRoot = posix.relative(
            unixify(options.specRepoRoot),
            unixify(options.typeSpecDirectory)
        );
        updateNpmPackageResult(
            packageResult,
            npmPackageInfo,
            relativeTypeSpecDirToSpecRoot,
            relativePackageDirToSdkRoot
        );

        const artifactPath = await createArtifact(generatedPackageDir);
        const relativeArtifactPath = posix.relative(unixify(options.sdkRepoRoot), unixify(artifactPath));
        packageResult.artifacts.push(relativeArtifactPath);

        const ciYamlPath = await createOrUpdateCiYaml(
            relativePackageDirToSdkRoot,
            options.versionPolicyName,
            npmPackageInfo
        );
        packageResult.path.push(ciYamlPath);

        packageResult.result = 'succeeded';
        logger.info(`Generated package successfully.`);
        logger.info(`Package summary: ${JSON.stringify(packageResult, undefined, 2)}`);
    } catch (err) {
        packageResult.result = 'failed';
        logger.error(`Failed to generate package due to ${(err as Error).stack ?? err}`);
        throw err;
    } finally {
        return packageResult;
    }
}
