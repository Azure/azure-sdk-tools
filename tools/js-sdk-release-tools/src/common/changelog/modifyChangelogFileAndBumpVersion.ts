import { Changelog } from "../../changelog/changelogGenerator.js";
import { updateUserAgent } from "../../xlc/codeUpdate/updateUserAgent.js";

import fs from 'fs';
import * as path from 'path';
import { getSDKType } from "../utils.js";
import { SDKType } from "../types.js";
const todayDate = new Date();
const dd = String(todayDate.getDate()).padStart(2, '0');
const mm = String(todayDate.getMonth() + 1).padStart(2, '0'); //January is 0!
const yyyy = todayDate.getFullYear();

const date = yyyy + '-' + mm + '-' + dd;

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
    const newVersion = isStableRelease ? '1.0.0' : '1.0.0-beta.1';
    const contentLog = getFirstReleaseContent(packageFolderPath, isStableRelease);
    const content = `# Release History
    
## ${newVersion} (${date})

### Features Added

${contentLog}
`;
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
}

export async function makeChangesForFirstRelease(packageFolderPath: string, isStableRelease: boolean) {
    await generateChangelogForFirstRelease(packageFolderPath, isStableRelease);

    const newVersion = isStableRelease ? '1.0.0' : '1.0.0-beta.1';
    changePackageJSON(packageFolderPath, newVersion);
    await updateUserAgent(packageFolderPath, newVersion);
}

export async function generateChangelogForMigratingToTrack2(packageFolderPath: string, nextPackageVersion: string) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
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
