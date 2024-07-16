import { GeneratedPackageInfo, ModularClientPackageOptions, ChangelogInfo, NpmPackageInfo } from '../../common/types';
import { Changelog } from '../../changelog/changelogGenerator';
import { logger } from '../../utils/logger';
import { generateChangelogAndBumpVersion } from '../changlog/generateChangelog';
import { createOrUpdateCiFile } from './utils/ciYamlUtils';
import { getNpmPackageInfo } from './utils/npmUtils';
import { buildPackage, createArtifact } from './utils/rushUtils';
import { generateTypeScriptCodeFromTypeSpec, getGeneratedPackageDirectory } from './utils/typeSpecUtils';

async function generatePackageInfo(
    generatedPackageDir: string,
    changelog: Changelog | undefined,
    npmPackageInfo: NpmPackageInfo
): Promise<GeneratedPackageInfo> {
    const breakingChangeItems = changelog?.getBreakingChangeItems() ?? [];
    const hasBreakingChange = changelog?.hasBreakingChange ?? false;
    const content = changelog?.displayChangeLog() ?? '';
    const changelogInfo: ChangelogInfo = { content, hasBreakingChange, breakingChangeItems };

    const packageInfo: GeneratedPackageInfo = {
        packageName: npmPackageInfo.name,
        version: npmPackageInfo.version,
        path: ['rush.json', 'common/config/rush/pnpm-lock.yaml'],
        artifacts: [],
        changelog: changelogInfo,
        result: 'succeeded' // TODO: consider when is failed
    };
    return packageInfo;
}

// !!!IMPORTANT:
// this function should be used ONLY in
//   1. the CodeGen pipeline of azure-rest-api-specs pull request for generating packages in azure-sdk-for-js
//   2. in the root directory of azure-sdk-for-js repo
// it has extra steps to generate a releasable azure sdk package (no modular client's doc for now, use RLC's for now) after typescript code is generate:
// https://github.com/Azure/azure-sdk-for-js/blob/main/documentation/steps-after-generations.md
export async function generateAzureSDKPackage(options: ModularClientPackageOptions): Promise<GeneratedPackageInfo> {
    logger.logInfo(`Start to generate modular client package for azure-sdk-for-js.`);

    try {
        // TODO: check if clean last generation
        await generateTypeScriptCodeFromTypeSpec(options);
        const generatedPackageDir = await getGeneratedPackageDirectory(options.typeSpecDirectory);
        await buildPackage(generatedPackageDir, options);
        // changelog generation will compute package version and bump it in package.json,
        // so changelog generation should be put before any task needs package.json's version
        const changelog = await generateChangelogAndBumpVersion(generatedPackageDir);
        const npmPackageInfo = await getNpmPackageInfo(generatedPackageDir);
        const artifactPath = await createArtifact(generatedPackageDir);
        const ciFilePath = await createOrUpdateCiFile(generatedPackageDir, options, npmPackageInfo);
        const packageInfo = generatePackageInfo(
            generatedPackageDir,
            changelog,
            npmPackageInfo,
            artifactPath,
            ciFilePath
        );
        console.log('~~~~~~~packageInfo\n', packageInfo);
        return packageInfo;
    } catch (err) {
    } finally {
    }
}
