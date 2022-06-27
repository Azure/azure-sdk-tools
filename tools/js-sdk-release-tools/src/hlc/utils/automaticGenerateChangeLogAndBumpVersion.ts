import {extractExportAndGenerateChangelog, readSourceAndExtractMetaData} from "../../changelog/extractMetaData";
import {Changelog, changelogGenerator} from "../../changelog/changelogGenerator";
import {NPMScope, NPMViewResult} from "@ts-common/azure-js-dev-tools";
import {
    makeChangesForFirstRelease,
    makeChangesForMigrateTrack1ToTrack2, makeChangesForPatchReleasingTrack2,
    makeChangesForReleasingTrack2
} from "./modifyChangelogFileAndBumpVersion";
import {logger} from "../../utils/logger";
import {
    bumpPatchVersion,
    bumpPreviewVersion,
    getLatestStableVersion,
    getNewVersion,
    isBetaVersion
} from "../../utils/version";
import {isGeneratedCodeStable} from "./isGeneratedCodeStable";
import {execSync} from "child_process";

const fs = require('fs');
const path = require('path');

export async function generateChangelogAndBumpVersion(packageFolderPath: string) {
    const shell = require('shelljs');
    const jsSdkRepoPath = String(shell.pwd());
    packageFolderPath = path.join(jsSdkRepoPath, packageFolderPath);
    const isStableRelease = isGeneratedCodeStable(path.join(packageFolderPath, 'src', 'models', 'parameters.ts'));
    const packageName = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), {encoding: 'utf-8'})).name;
    const npm = new NPMScope({ executionFolderPath: packageFolderPath });
    const npmViewResult: NPMViewResult = await npm.view({ packageName });
    const stableVersion = getLatestStableVersion(npmViewResult);

    if (npmViewResult.exitCode !== 0 || (!!stableVersion && isBetaVersion(stableVersion) && isStableRelease)) {
        logger.log(`Package ${packageName} is first${npmViewResult.exitCode !== 0? ' ': ' stable'} release, generating changelogs and setting version for first${npmViewResult.exitCode !== 0? ' ': ' stable'} release...`);
        makeChangesForFirstRelease(packageFolderPath, isStableRelease);
        logger.log(`Generate changelogs and setting version for first${npmViewResult.exitCode !== 0? ' ': ' stable'} release successfully`);
    } else {
        if (!stableVersion) {
            logger.logError(`Invalid latest version ${stableVersion}`);
            process.exit(1);
        }
        logger.log(`Package ${packageName} has been released before, checking whether previous release is track2 sdk...`)
        const usedVersions = npmViewResult['versions'];
        // in our rule, we always compare to stableVersion. But here wo should pay attention to the some stableVersion which contains beta, which means the package has not been GA.
        try {
            await shell.mkdir(path.join(packageFolderPath, 'changelog-temp'));
            await shell.cd(path.join(packageFolderPath, 'changelog-temp'));
            await shell.exec(`npm pack ${packageName}@${stableVersion}`);
            await shell.exec('tar -xzf *.tgz');
            await shell.cd(packageFolderPath);
            // only track2 sdk includes sdk-type with value mgmt
            const sdkType = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'changelog-temp', 'package', 'package.json'), {encoding: 'utf-8'}))['sdk-type'];
            if (sdkType && sdkType === 'mgmt') {
                logger.log(`Package ${packageName} released before is track2 sdk`);
                logger.log('Generating changelog by comparing api.md...');
                const reviewFolder = path.join(packageFolderPath, 'changelog-temp', 'package', 'review');
                let apiMdFileNPM: string = path.join(reviewFolder, fs.readdirSync(reviewFolder)[0]);
                let apiMdFileLocal: string = path.join(packageFolderPath, 'review', fs.readdirSync(path.join(packageFolderPath, 'review'))[0]);
                const changelog: Changelog = await extractExportAndGenerateChangelog(apiMdFileNPM, apiMdFileLocal);
                if (!changelog.hasBreakingChange && !changelog.hasFeature) {
                    logger.logError('Cannot generate changelog because the codes of local and npm may be the same.');
                    logger.log('Try to bump a fix version');
                    const oriPackageJson = execSync(`git show HEAD:${path.relative(jsSdkRepoPath, path.join(packageFolderPath, 'package.json')).replace(/\\/g, '/')}`, {encoding: 'utf-8'});
                    const oriVersion = JSON.parse(oriPackageJson).version;
                    const oriVersionReleased = !usedVersions? false : usedVersions.includes(oriVersion);
                    let newVersion = oriVersion;
                    if (oriVersionReleased) {
                        newVersion = isBetaVersion(oriVersion)? bumpPreviewVersion(oriVersion, usedVersions) : bumpPatchVersion(oriVersion, usedVersions);
                    }
                    makeChangesForPatchReleasingTrack2(packageFolderPath, newVersion);
                } else {
                    const newVersion = getNewVersion(stableVersion, usedVersions, changelog.hasBreakingChange, isStableRelease);
                    makeChangesForReleasingTrack2(packageFolderPath, newVersion, changelog);
                    logger.log('Generate changelogs and setting version for track2 release successfully');
                    return changelog;
                }
            } else {
                logger.log(`Package ${packageName} released before is track1 sdk`);
                logger.log('Generating changelog of migrating track1 to track2 sdk...');
                const newVersion = getNewVersion(stableVersion, usedVersions, true, isStableRelease);
                makeChangesForMigrateTrack1ToTrack2(packageFolderPath, newVersion);
                logger.log('Generate changelogs and setting version for migrating track1 to track2 successfully');
            }
        } finally {
            await shell.exec(`rm -r ${path.join(packageFolderPath, 'changelog-temp')}`);
            await shell.cd(jsSdkRepoPath);
        }
    }
}
