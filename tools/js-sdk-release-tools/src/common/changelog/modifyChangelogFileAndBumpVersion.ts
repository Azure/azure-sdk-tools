import { Changelog } from "../../changelog/changelogGenerator.js";
import { updateUserAgent } from "../../xlc/codeUpdate/updateUserAgent.js";

import fs from 'fs';
import * as path from 'path';
import { getSDKType, getCurrentDate, getApiReviewPath, fixChangelogFormat, tryReadNpmPackageChangelog, extractNextVersionPackage } from "../utils.js";
import { SDKType, ModularSDKType } from "../types.js";
import { getModularSDKType } from "../../utils/generateInputUtils.js";
import { NpmViewParameters, tryCreateLastestStableNpmViewFromGithub } from '../npmUtils.js';
import { DifferenceDetector } from '../../changelog/v2/DifferenceDetector.js';
import { ChangelogGenerator } from '../../changelog/v2/ChangelogGenerator.js';
import { getversionDate } from "../../utils/version.js";
import { 
    bumpPatchVersion, 
    bumpPreviewVersion, 
    getNewVersion, 
    isBetaVersion 
} from "../../utils/version.js";
import { execSync } from "child_process";
import { logger } from "../../utils/logger.js";

/**
 * Generate changelog by comparing API review files
 * Sets up API comparison environment, detects differences, and generates changelog
 */
export async function generateChangelogFromApiComparison(packageFolderPath: string, packageName: string, stableVersion: string, jsSdkRepoPath: string): Promise<any> {
    logger.info('Start to generate changelog by comparing api.md.');
    const npmPackageRoot = path.join(packageFolderPath, 'changelog-temp', 'package');
    const apiMdFileNPM = getApiReviewPath(npmPackageRoot);
    const apiMdFileLocal = getApiReviewPath(packageFolderPath);
    const lastestStableApiView: NpmViewParameters = {
        file: "ApiView",
        version: stableVersion,
        packageName: packageName,
        packageFolderPath: packageFolderPath,
        sdkRootPath: jsSdkRepoPath,
        npmPackagePath: npmPackageRoot,
    }
    if (!fs.existsSync(apiMdFileNPM)) {
        fs.mkdirSync(path.join(npmPackageRoot, "review"));
        await tryCreateLastestStableNpmViewFromGithub(lastestStableApiView);
    }
    const oldSDKType = getSDKType(npmPackageRoot);
    const newSDKType = getSDKType(packageFolderPath);
    const diffDetector = new DifferenceDetector(
        { path: apiMdFileNPM, sdkType: oldSDKType },
        { path: apiMdFileLocal, sdkType: newSDKType },
    );
    const detectResult = await diffDetector.detect();
    const detectContext = diffDetector.getDetectContext();
    const changelogGenerator = new ChangelogGenerator(
        detectContext,
        detectResult,
    );
    const changelog = changelogGenerator.generate();
    return changelog;
}

/**
 * Calculate new version based on changelog changes
 * If changelog has no breaking changes or features, bumps fix version
 * Otherwise calculates version based on breaking changes and release type
 */
export function calculateNewVersion(
    changelog: any,
    stableVersion: string,
    usedVersions: string[],
    isStableRelease: boolean,
    packageFolderPath: string,
    jsSdkRepoPath: string
): string {
    let newVersion = '';
    if (!changelog.hasBreakingChange && !changelog.hasFeature) {
        logger.warn('Failed to generate changelog because the codes of local and npm may be the same.');
        logger.info('Start to bump a fix version.');
        const oriPackageJson = execSync(`git show HEAD:${path.relative(jsSdkRepoPath, path.join(packageFolderPath, 'package.json')).replace(/\\/g, '/')}`, { encoding: 'utf-8' });
        const oriVersion = JSON.parse(oriPackageJson).version;
        const oriVersionReleased = !usedVersions ? false : usedVersions.includes(oriVersion);
        newVersion = oriVersion;
        if (oriVersionReleased) {
            newVersion = isBetaVersion(oriVersion) ? bumpPreviewVersion(oriVersion, usedVersions) : bumpPatchVersion(oriVersion, usedVersions);
        }
    } else {
        newVersion = getNewVersion(stableVersion, usedVersions, changelog.hasBreakingChange, isStableRelease);
    }
    return newVersion;
}

/**
 * Get and process original changelog content from npm or next version
 * Handles fetching changelog from stable or next version and applies necessary fixes
 */
export async function getProcessedOriginalChangelog(packageFolderPath: string, packageName: string, stableVersion: string, nextVersion: string | undefined, npmViewResult: any, jsSdkRepoPath: string): Promise<string> {
    const npmPackageRoot = path.join(packageFolderPath, 'changelog-temp', 'package');
    const changelogPath = path.join(npmPackageRoot, 'CHANGELOG.md');
    const lastStableChangelog: NpmViewParameters = {
        file: "CHANGELOG.md",
        version: stableVersion,
        packageName: packageName,
        packageFolderPath: packageFolderPath,
        sdkRootPath: jsSdkRepoPath,
        npmPackagePath: npmPackageRoot,
    }
    let originalChangeLogContent = tryReadNpmPackageChangelog(changelogPath, lastStableChangelog);

    if (nextVersion) {
        logger.info(`Next version is ${nextVersion}, start to prepare next version package.`);
        extractNextVersionPackage(packageFolderPath, packageName, nextVersion);
        logger.info("Created next folder successfully.")

        const latestDate = getversionDate(npmViewResult, stableVersion);
        const nextDate = getversionDate(npmViewResult, nextVersion);
        if (latestDate && nextDate && latestDate <= nextDate) {
            const nextChangelogPath = path.join(packageFolderPath, 'changelog-temp', 'next', 'package', 'CHANGELOG.md');
            const nextNPMPackageRoot = path.join(packageFolderPath, 'changelog-temp', 'next', 'package');
            const latestNextChangelog: NpmViewParameters = {
                file: "CHANGELOG.md",
                version: nextVersion,
                packageName: packageName,
                packageFolderPath: packageFolderPath,
                sdkRootPath: jsSdkRepoPath,
                npmPackagePath: nextNPMPackageRoot,
            }
            originalChangeLogContent = tryReadNpmPackageChangelog(nextChangelogPath, latestNextChangelog);
            logger.info('Keep previous preview changelog.');
        }
    }

    if (originalChangeLogContent.includes("https://aka.ms/js-track2-quickstart")) {
        originalChangeLogContent = originalChangeLogContent.replace("https://aka.ms/js-track2-quickstart", "https://aka.ms/azsdk/js/mgmt/quickstart");
    }
    originalChangeLogContent = fixChangelogFormat(originalChangeLogContent);

    return originalChangeLogContent;
}

export function getFirstReleaseContent(packageFolderPath: string, isStableRelease: boolean) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
    const sdkType = getSDKType(packageFolderPath);
    const firstBetaContent = `Initial release of the ${packageJsonData.name} package`;
    const firstStableContent = `This is the first stable version with the package of ${packageJsonData.name}`;
    const hlcClientContent = `The package of ${packageJsonData.name} is using our next generation design principles. To learn more, please refer to our documentation [Quick Start](https://aka.ms/azsdk/js/mgmt/quickstart).`
    switch (sdkType) {
        case SDKType.ModularClient:
            return isStableRelease ? firstStableContent : firstBetaContent;
        case SDKType.HighLevelClient:
            return hlcClientContent;
        case SDKType.RestLevelClient:
            return isStableRelease ? firstStableContent : firstBetaContent;
        default:
            throw new Error(`Unsupported SDK type: ${sdkType}`);
    }
}

export async function generateChangelogForFirstRelease(packageFolderPath: string, isStableRelease: boolean) {
    const newVersion = '1.0.0-beta.1';
    const contentLog = getFirstReleaseContent(packageFolderPath, isStableRelease);
    const date = getCurrentDate();
    const content = `# Release History
    
## ${newVersion} (${date})

### Features Added

${contentLog}
`;
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
}

export async function makeChangesForFirstRelease(packageFolderPath: string, isStableRelease: boolean) {
    await generateChangelogForFirstRelease(packageFolderPath, isStableRelease);

    const newVersion = '1.0.0-beta.1';
    changePackageJSON(packageFolderPath, newVersion);
    await updateUserAgent(packageFolderPath, newVersion);
}

export async function generateChangelogForMigratingToTrack2(packageFolderPath: string, nextPackageVersion: string) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
    const date = getCurrentDate();
    const content = `# Release History
    
## ${nextPackageVersion} (${date})

### Features Added

The package of ${packageJsonData.name} is using our next generation design principles since version ${nextPackageVersion}, which contains breaking changes.

To understand the detail of the change, please refer to [Changelog](https://aka.ms/js-track2-changelog).

To migrate the existing applications to the latest version, please refer to [Migration Guide](https://aka.ms/js-track2-migration-guide).

To learn more, please refer to our documentation [Quick Start](https://aka.ms/azsdk/js/mgmt/quickstart).
`;
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
}

export async function makeChangesForMigrateTrack1ToTrack2(packageFolderPath: string, nextPackageVersion: string) {
    await generateChangelogForMigratingToTrack2(packageFolderPath, nextPackageVersion);
    changePackageJSON(packageFolderPath, nextPackageVersion);
    await updateUserAgent(packageFolderPath, nextPackageVersion)
}

export function changePackageJSON(packageFolderPath: string, packageVersion: string) {
    const data: string = fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8');
    const result = data.replace(/"version": "[0-9.a-z-]+"/g, '"version": "' + packageVersion + '"');
    fs.writeFileSync(path.join(packageFolderPath, 'package.json'), result, 'utf8');
}

export async function generateChangelogForReleasingTrack2(packageFolderPath: string, packageVersion: string, changeLog: string, originalChangeLogContent: string, comparedVersion: string) {
    const date = getCurrentDate();
    let packageVersionDetail = `## ${packageVersion} (${date})`;
    if (packageVersion.includes("beta")) {
        packageVersionDetail += `\nCompared with version ${comparedVersion}`
    }
    const modifiedChangelogContent = `# Release History

${packageVersionDetail}

${changeLog}
${originalChangeLogContent.replace(/.*Release History[\n\r]*/g, '')}`;

    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), modifiedChangelogContent, { encoding: 'utf-8' });
}

export async function makeChangesForReleasingTrack2(packageFolderPath: string, packageVersion: string, changeLog: string, originalChangeLogContent: string, comparedVersion: string) {
    await generateChangelogForReleasingTrack2(packageFolderPath, packageVersion, changeLog, originalChangeLogContent, comparedVersion);
    changePackageJSON(packageFolderPath, packageVersion);
    await updateUserAgent(packageFolderPath, packageVersion);
}

export async function updateChangelog(packageFolderPath: string, newVersion: string, sdkReleaseDate?: string) {
    const originalChangeLogContent = fs.readFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), 'utf8');
    let modifiedChangelogContent = originalChangeLogContent;
    
    // Parse the first ## heading to extract the current version and release date
    // Pattern: ## 2.0.0-beta.1 (2025-10-27)
    const firstHeadingRegex = /##\s+([\d\w\.\-]+)\s+\((\d{4}-\d{2}-\d{2})\)/;
    const match = originalChangeLogContent.match(firstHeadingRegex);
    
    if (match) {
        const currentVersion = match[1];
        const currentReleaseDate = match[2];
        
        // Replace the current version with newVersion
        const versionPattern = currentVersion.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        modifiedChangelogContent = modifiedChangelogContent.replace(new RegExp(versionPattern, 'g'), newVersion);
        
        // Replace the current release date with sdkReleaseDate if provided
        if (sdkReleaseDate) {
            const datePattern = currentReleaseDate.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            // Only replace the date in the first heading line
            const firstHeadingPattern = new RegExp(`(##\\s*${newVersion.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*)\\(${datePattern}\\)`);
            modifiedChangelogContent = modifiedChangelogContent.replace(firstHeadingPattern, `$1(${sdkReleaseDate})`);
        }
    }
    
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), modifiedChangelogContent, { encoding: 'utf-8' });
}
