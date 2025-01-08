import fs from 'fs';
import path from 'path';
import shell from 'shelljs';

import { extractExportAndGenerateChangelog } from "../../changelog/extractMetaData";
import { Changelog } from "../../changelog/changelogGenerator";
import {
    makeChangesForFirstRelease,
    makeChangesForMigrateTrack1ToTrack2, makeChangesForPatchReleasingTrack2,
    makeChangesForReleasingTrack2
} from "./modifyChangelogFileAndBumpVersion";
import { logger } from "../../utils/logger";
import {
    bumpPatchVersion,
    bumpPreviewVersion,
    getNewVersion,
    getVersion,
    isBetaVersion
} from "../../utils/version";
import { execSync } from "child_process";
import { getversionDate } from "../../utils/version";
import { ApiVersionType, SDKType } from "../types"
import { getApiVersionType } from '../../xlc/apiVersion/apiVersionTypeExtractor'
import { fixChangelogFormat, getApiReviewPath, getNpmPackageName, getSDKType, tryReadNpmPackageChangelog } from '../utils';
import { tryGetNpmView } from '../npmUtils';

export async function generateChangelogAndBumpVersion(packageFolderPath: string) {
    logger.info(`Start to generate changelog and bump version in ${packageFolderPath}`);
    const jsSdkRepoPath = String(shell.pwd());
    packageFolderPath = path.join(jsSdkRepoPath, packageFolderPath);
    const ApiType = await getApiVersionType(packageFolderPath);
    const isStableRelease = ApiType != ApiVersionType.Preview;
    const packageName = getNpmPackageName(packageFolderPath);
    const npmViewResult = await tryGetNpmView(packageName);
    const stableVersion = getVersion(npmViewResult, "latest");
    const nextVersion = getVersion(npmViewResult, "next");

    if (!npmViewResult || (!!stableVersion && isBetaVersion(stableVersion) && isStableRelease)) {
        logger.info(`Package ${packageName} is first ${!npmViewResult ? ' ': ' stable'} release, start to generate changelogs and set version for first ${!npmViewResult ? ' ': ' stable'} release.`);
        makeChangesForFirstRelease(packageFolderPath, isStableRelease);
        logger.info(`Generated changelogs and setting version for first${!npmViewResult ? ' ': ' stable'} release successfully`);
    } else {
        if (!stableVersion) {
            logger.error(`Invalid latest version ${stableVersion}`);
            process.exit(1);
        }
        logger.info(`Package ${packageName} has been released before, start to check whether previous release is track2 sdk.`)
        const usedVersions = Object.keys(npmViewResult['versions']);
        // in our rule, we always compare to stableVersion. But here wo should pay attention to the some stableVersion which contains beta, which means the package has not been GA.
        try {
            shell.mkdir(path.join(packageFolderPath, 'changelog-temp'));
            shell.cd(path.join(packageFolderPath, 'changelog-temp'));
            shell.exec(`npm pack ${packageName}@${stableVersion}`, { silent: true});
            const files = shell.ls('*.tgz');
            shell.exec(`tar -xzf ${files[0]}`);
            shell.cd(packageFolderPath);

            // only track2 sdk includes sdk-type with value mgmt
            const sdkType = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'changelog-temp', 'package', 'package.json'), {encoding: 'utf-8'}))['sdk-type'];
            const clientType = getSDKType(packageFolderPath);
            if (sdkType && sdkType === 'mgmt' || clientType === SDKType.RestLevelClient) {
                logger.info(`Package ${packageName} released before is track2 sdk.`);
                logger.info('Start to generate changelog by comparing api.md.');
                const npmPackageRoot = path.join(packageFolderPath, 'changelog-temp', 'package');
                const apiMdFileNPM = getApiReviewPath(npmPackageRoot);
                const apiMdFileLocal = getApiReviewPath(packageFolderPath);
                const oldSDKType = getSDKType(npmPackageRoot);
                const newSDKType = getSDKType(packageFolderPath);
                const changelog: Changelog = await extractExportAndGenerateChangelog(apiMdFileNPM, apiMdFileLocal, oldSDKType, newSDKType);
                const changelogPath = path.join(npmPackageRoot, 'CHANGELOG.md');
                let originalChangeLogContent = tryReadNpmPackageChangelog(changelogPath);
                if(nextVersion){
                    shell.cd(path.join(packageFolderPath, 'changelog-temp'));
                    shell.mkdir(path.join(packageFolderPath, 'changelog-temp', 'next'));
                    shell.cd(path.join(packageFolderPath,'changelog-temp', 'next'));
                    shell.exec(`npm pack ${packageName}@${nextVersion}`, { silent: true});
                    const files = shell.ls('*.tgz');
                    shell.exec(`tar -xzf ${files[0]}`);
                    shell.cd(packageFolderPath);
                    logger.info("Created next folder successfully.")
    
                    const latestDate = getversionDate(npmViewResult, stableVersion);
                    const nextDate = getversionDate(npmViewResult,nextVersion);
                    if (latestDate && nextDate && latestDate <= nextDate){
                        const nextChangelogPath = path.join(packageFolderPath,'changelog-temp', 'next', 'package', 'CHANGELOG.md');
                        originalChangeLogContent = tryReadNpmPackageChangelog(nextChangelogPath);
                        logger.info('Keep previous preview changelog.');
                    }
                }
                if(originalChangeLogContent.includes("https://aka.ms/js-track2-quickstart")){
                    originalChangeLogContent=originalChangeLogContent.replace("https://aka.ms/js-track2-quickstart","https://aka.ms/azsdk/js/mgmt/quickstart");
                }
                originalChangeLogContent = fixChangelogFormat(originalChangeLogContent);
                if (!changelog.hasBreakingChange && !changelog.hasFeature) {
                    logger.warn('Failed to generate changelog because the codes of local and npm may be the same.');
                    logger.info('Start to bump a fix version.');
                    const oriPackageJson = execSync(`git show HEAD:${path.relative(jsSdkRepoPath, path.join(packageFolderPath, 'package.json')).replace(/\\/g, '/')}`, {encoding: 'utf-8'});
                    const oriVersion = JSON.parse(oriPackageJson).version;
                    const oriVersionReleased = !usedVersions? false : usedVersions.includes(oriVersion);
                    let newVersion = oriVersion;
                    if (oriVersionReleased) {
                        newVersion = isBetaVersion(oriVersion)? bumpPreviewVersion(oriVersion, usedVersions) : bumpPatchVersion(oriVersion, usedVersions);
                    }
                    makeChangesForPatchReleasingTrack2(packageFolderPath, newVersion);
                } else {
                    await changelog.postProcess(npmPackageRoot, packageFolderPath, clientType)
                    const newVersion = getNewVersion(stableVersion, usedVersions, changelog.hasBreakingChange, isStableRelease);
                    makeChangesForReleasingTrack2(packageFolderPath, newVersion, changelog, originalChangeLogContent,stableVersion);
                    logger.info('Generated changelogs and set version for track2 release successfully.');
                    return changelog;
                }
            } else {
                logger.info(`Package ${packageName} released before is track1 sdk.`);
                logger.info('Start to generate changelog of migrating track1 to track2 sdk.');
                const newVersion = getNewVersion(stableVersion, usedVersions, true, isStableRelease);
                makeChangesForMigrateTrack1ToTrack2(packageFolderPath, newVersion);
                logger.info('Generated changelogs and setting version for migrating track1 to track2 successfully.');
            }
        } finally {
            shell.rm('-r', `${path.join(packageFolderPath, 'changelog-temp')}`);
            shell.cd(jsSdkRepoPath);
        }
    }
}
