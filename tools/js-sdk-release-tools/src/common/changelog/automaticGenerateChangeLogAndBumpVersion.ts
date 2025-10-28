import fs from 'fs';
import path from 'path';
import shell from 'shelljs';

import {
    makeChangesForFirstRelease,
    makeChangesForMigrateTrack1ToTrack2,
    makeChangesForReleasingTrack2,
    generateChangelogForFirstRelease,
    generateChangelogForReleasingTrack2,
    generateChangelogForMigratingToTrack2,
    changePackageJSON,
    updateChangelog
} from "./modifyChangelogFileAndBumpVersion.js";
import { updateUserAgent, } from "../../xlc/codeUpdate/updateUserAgent.js";
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
    isStableSDKReleaseTypeForCli
} from "../../utils/version.js";
import { execSync } from "child_process";
import { getversionDate } from "../../utils/version.js";
import { ApiVersionType, SDKType } from "../types.js"
import { getApiVersionType } from '../../xlc/apiVersion/apiVersionTypeExtractor.js'
import { fixChangelogFormat, getApiReviewPath, getNpmPackageName, getSDKType, tryReadNpmPackageChangelog, extractNpmPackage, extractNextVersionPackage, cleanupResources } from '../utils.js';
import { NpmViewParameters, tryCreateLastestStableNpmViewFromGithub, tryGetNpmView } from '../npmUtils.js';
import { DifferenceDetector } from '../../changelog/v2/DifferenceDetector.js';
import { ChangelogGenerator } from '../../changelog/v2/ChangelogGenerator.js';

/**
 * Generate changelog content by analyzing API differences
 * This method focuses solely on changelog generation without modifying files
 */
export async function generateChangelog(jsSdkRepoPath: string, packageFolderPath: string): Promise<any> {
    logger.info(`Start to generate changelog in ${packageFolderPath}`);
    const apiVersionType = await getApiVersionType(packageFolderPath);
    const isStableRelease = apiVersionType != ApiVersionType.Preview;
    const packageName = getNpmPackageName(packageFolderPath);
    const npmViewResult = await tryGetNpmView(packageName);
    const stableVersion = npmViewResult ? getLatestStableVersion(npmViewResult) : undefined;
    const nextVersion = getNextBetaVersion(npmViewResult);

    if (!npmViewResult || (!!stableVersion && isBetaVersion(stableVersion) && isStableRelease)) {
        logger.info(`Package ${packageName} is first ${!npmViewResult ? ' ' : ' stable'} release, start to generate changelogs and set version for first ${!npmViewResult ? ' ' : ' stable'} release.`);
        await generateChangelogForFirstRelease(packageFolderPath, isStableRelease);

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
                } else {
                    newVersion = getNewVersion(stableVersion, usedVersions, changelog.hasBreakingChange, isStableRelease);
                    logger.info('Generated changelogs for track2 release successfully.');
                }
                const changelogContent = changelog.content.length === 0 ? `### Features Added\n` : changelog.content;
                await generateChangelogForReleasingTrack2(packageFolderPath, newVersion, changelogContent, originalChangeLogContent, stableVersion);
                return changelog;
            } else {
                logger.info(`Package ${packageName} released before is track1 sdk.`);
                logger.info('Start to generate changelog of migrating track1 to track2 sdk.');
                const newVersion = getNewVersion(stableVersion, usedVersions, true, isStableRelease);
                await generateChangelogForMigratingToTrack2(packageFolderPath, newVersion);
                logger.info('Generated changelogs for migrating track1 to track2 successfully.');
            }
        } finally {
            cleanupResources(packageFolderPath, jsSdkRepoPath);
        }
    }
}

/**
 * Update and bump version based on existing changelog file
 * This method reads the generated changelog file and updates package version accordingly
 */
export async function updateBumpVersion(
    jsSdkRepoPath: string,
    packageFolderPath: string,
    sdkReleaseType: string | undefined,
    sdkVersion: string | undefined,
    sdkReleaseDate: string | undefined
): Promise<void> {
    logger.info(`Start to update bump version for ${packageFolderPath}`);
    const apiVersionType = await getApiVersionType(packageFolderPath);
    const isStableRelease = await isStableSDKReleaseTypeForCli(apiVersionType, sdkReleaseType)
    const packageName = getNpmPackageName(packageFolderPath);
    const npmViewResult = await tryGetNpmView(packageName);
    const stableVersion = npmViewResult ? getLatestStableVersion(npmViewResult) : undefined;

    if (!npmViewResult || (!!stableVersion && isBetaVersion(stableVersion) && isStableRelease)) {
        logger.info(`Package ${packageName} is first ${!npmViewResult ? ' ' : ' stable'} release, start to generate changelogs and set version for first ${!npmViewResult ? ' ' : ' stable'} release.`);
        await makeChangesForFirstRelease(packageFolderPath, isStableRelease);
        logger.info(`Setting version for first${!npmViewResult ? ' ' : ' stable'} release successfully`);
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
                let newVersion = '';
                if (!sdkVersion || sdkVersion.trim() === '') {
                    logger.info('Start to calculate new version for track2 sdk release.');
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
                    if (!changelog.hasBreakingChange && !changelog.hasFeature) {
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
                } else {
                    newVersion = sdkVersion;
                    logger.info(`SdkVersion ${sdkVersion} is provided, skip calculating new version.`);
                }

                await updateChangelog(packageFolderPath, newVersion, sdkReleaseDate);
                changePackageJSON(packageFolderPath, newVersion);
                await updateUserAgent(packageFolderPath, newVersion);
                logger.info('Set version for track2 release successfully.');
            } else {
                logger.info(`Package ${packageName} released before is track1 sdk.`);
                let newVersion = '';
                if (!sdkVersion || sdkVersion.trim() === '') {
                    logger.info('Start to calculate new version for migrating track1 to track2 sdk.');
                    newVersion = getNewVersion(stableVersion, usedVersions, true, isStableRelease);
                } else {
                    newVersion = sdkVersion;
                    logger.info(`SdkVersion ${sdkVersion} is provided, skip calculating new version.`);
                }

                await updateChangelog(packageFolderPath, newVersion, sdkReleaseDate);
                changePackageJSON(packageFolderPath, newVersion);
                await updateUserAgent(packageFolderPath, newVersion)
                logger.info('Setting version for migrating track1 to track2 successfully.');
            }
        } finally {
            cleanupResources(packageFolderPath, jsSdkRepoPath);
        }
    }
}

export async function generateChangelogAndBumpVersion(packageFolderPath: string, options: { apiVersion: string | undefined, sdkReleaseType: string | undefined }) {
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