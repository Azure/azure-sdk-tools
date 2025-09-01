import fs from 'fs';
import path from 'path';
import shell from 'shelljs';

import {
    makeChangesForFirstRelease,
    makeChangesForMigrateTrack1ToTrack2,
    makeChangesForReleasingTrack2
} from "./modifyChangelogFileAndBumpVersion.js";
import { logger } from "../../utils/logger.js";
import {
    bumpPatchVersion,
    bumpPreviewVersion,
    getLatestStableVersion,
    getNewVersion,
    getNextBetaVersion,
    getUsedVersions,
    isBetaVersion,
    isStableSDKReleaseType,
} from "../../utils/version.js";
import { execSync } from "child_process";
import { getversionDate } from "../../utils/version.js";
import { SDKType } from "../types.js"
import { getApiVersionType } from '../../xlc/apiVersion/apiVersionTypeExtractor.js'
import { fixChangelogFormat, getApiReviewPath, getNpmPackageName, getSDKType, tryReadNpmPackageChangelog, extractNpmPackage, extractNextVersionPackage, cleanupResources } from '../utils.js';
import { NpmViewParameters, tryCreateLastestStableNpmViewFromGithub, tryGetNpmView, getGitFileContent } from '../npmUtils.js';
import { DifferenceDetector } from '../../changelog/v2/DifferenceDetector.js';
import { ChangelogGenerator } from '../../changelog/v2/ChangelogGenerator.js';

export async function generateChangelogAndBumpVersion(packageFolderPath: string,  options: { apiVersion: string | undefined, sdkReleaseType: string | undefined }) {
    logger.info(`Start to generate changelog and bump version in ${packageFolderPath}`);
    const jsSdkRepoPath = String(shell.pwd());
    packageFolderPath = path.join(jsSdkRepoPath, packageFolderPath);
    const apiVersionType = await getApiVersionType(packageFolderPath);
    const isStableRelease = await isStableSDKReleaseType(apiVersionType, options)
    const packageName = getNpmPackageName(packageFolderPath);
    const npmViewResult = await tryGetNpmView(packageName);
    const stableVersion = npmViewResult ? getLatestStableVersion(npmViewResult) : undefined;
    const nextVersion = getNextBetaVersion(npmViewResult);

    if (!npmViewResult || (!!stableVersion && isBetaVersion(stableVersion) && isStableRelease)) {
        logger.info(`Package ${packageName} is first ${!npmViewResult ? ' ' : ' stable'} release, start to generate changelogs and set version for first ${!npmViewResult ? ' ' : ' stable'} release.`);
        await makeChangesForFirstRelease(packageFolderPath, isStableRelease);
        logger.info(`Generated changelogs and setting version for first${!npmViewResult ? ' ' : ' stable'} release successfully`);
    } else {
        if (!stableVersion) {
            logger.error(`Invalid latest version ${stableVersion}`);
            process.exit(1);
        }
        logger.info(`Package ${packageName} has been released before, start to check whether previous release is track2 sdk.`)
        const usedVersions = getUsedVersions(npmViewResult);
        // in our rule, we always compare to stableVersion. But here wo should pay attention to the some stableVersion which contains beta, which means the package has not been GA.
        try {
            extractNpmPackage(packageFolderPath, packageName, stableVersion);

            // only track2 sdk includes sdk-type with value mgmt
            const sdkType = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'changelog-temp', 'package', 'package.json'), { encoding: 'utf-8' }))['sdk-type'];
            const clientType = getSDKType(packageFolderPath);
            if (sdkType && sdkType === 'mgmt' || clientType === SDKType.RestLevelClient) {
                logger.info(`Package ${packageName} released before is track2 sdk.`);
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

                    // Since we delete all contents before SDK generation, restore changelog from GitHub main branch
                    logger.info('Restoring changelog from GitHub main branch since no new features or breaking changes detected.');
                    try {
                        const changelogPath = path.relative(jsSdkRepoPath, path.join(packageFolderPath, 'CHANGELOG.md')).replace(/\\/g, '/');
                        const mainBranchChangelog = getGitFileContent('origin/main', changelogPath);
                        if (mainBranchChangelog.trim()) {
                            originalChangeLogContent = mainBranchChangelog;
                            logger.info('Successfully restored changelog from GitHub main branch.');
                        } else {
                            logger.warn('No changelog content found on GitHub main branch.');
                        }
                    } catch (error) {
                        logger.warn('Failed to restore changelog from GitHub main branch, using existing changelog.');
                        logger.debug(`Error details: ${error}`);
                    }
                } else {
                    newVersion = getNewVersion(stableVersion, usedVersions, changelog.hasBreakingChange, isStableRelease);
                    logger.info('Generated changelogs and set version for track2 release successfully.');
                }
                const changelogContent = changelog.content.length === 0 ? `### Features Added\n` : changelog.content; 
                await makeChangesForReleasingTrack2(packageFolderPath, newVersion, changelogContent, originalChangeLogContent, stableVersion);
                return changelog;
            } else {
                logger.info(`Package ${packageName} released before is track1 sdk.`);
                logger.info('Start to generate changelog of migrating track1 to track2 sdk.');
                const newVersion = getNewVersion(stableVersion, usedVersions, true, isStableRelease);
                await makeChangesForMigrateTrack1ToTrack2(packageFolderPath, newVersion);
                logger.info('Generated changelogs and setting version for migrating track1 to track2 successfully.');
            }
        } finally {
            cleanupResources(packageFolderPath, jsSdkRepoPath);
        }
    }
}