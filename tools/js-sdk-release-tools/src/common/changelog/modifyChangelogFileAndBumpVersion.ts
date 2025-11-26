import { Changelog } from "../../changelog/changelogGenerator.js";
import { updateUserAgent } from "../../xlc/codeUpdate/updateUserAgent.js";
import { UpdateMode } from "./automaticGenerateChangeLogAndBumpVersion.js";

import fs from 'fs';
import * as path from 'path';
import { getSDKType } from "../utils.js";
import { SDKType } from "../types.js";

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

export async function makeChangesForFirstRelease(
    packageFolderPath: string, 
    sdkReleaseDate: string, 
    isStableRelease: boolean,
    updateMode: UpdateMode = UpdateMode.Both
) {
    const newVersion = '1.0.0-beta.1';
    const contentLog = getFirstReleaseContent(packageFolderPath, isStableRelease);
    const content = `# Release History
    
## ${newVersion} (${sdkReleaseDate})

### Features Added

${contentLog}
`;
    
    // Decide how to handle changelog based on update mode
    if (updateMode === UpdateMode.ChangelogOnly || updateMode === UpdateMode.Both) {
        // Generate new changelog content
        fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
    } else if (updateMode === UpdateMode.VersionOnly) {
        // Only update version information in existing changelog
        await updateChangelog(packageFolderPath, newVersion, sdkReleaseDate);
    }
    
    // Decide whether to update version based on mode
    if (updateMode === UpdateMode.VersionOnly || updateMode === UpdateMode.Both) {
        changePackageJSON(packageFolderPath, newVersion);
        await updateUserAgent(packageFolderPath, newVersion);
    }
}

export async function makeChangesForMigrateTrack1ToTrack2(
    packageFolderPath: string, 
    nextPackageVersion: string, 
    sdkReleaseDate: string, 
    updateMode: UpdateMode
) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
    const content = `# Release History
    
## ${nextPackageVersion} (${sdkReleaseDate})
### Features Added

The package of ${packageJsonData.name} is using our next generation design principles since version ${nextPackageVersion}, which contains breaking changes.

To understand the detail of the change, please refer to [Changelog](https://aka.ms/js-track2-changelog).

To migrate the existing applications to the latest version, please refer to [Migration Guide](https://aka.ms/js-track2-migration-guide).

To learn more, please refer to our documentation [Quick Start](https://aka.ms/azsdk/js/mgmt/quickstart).
`;
    
    // Decide how to handle changelog based on update mode
    if (updateMode === UpdateMode.ChangelogOnly || updateMode === UpdateMode.Both) {
        // Generate new changelog content
        fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
    } else if (updateMode === UpdateMode.VersionOnly) {
        // Only update version information in existing changelog
        await updateChangelog(packageFolderPath, nextPackageVersion, sdkReleaseDate);
    }
    
    // Decide whether to update version based on mode
    if (updateMode === UpdateMode.VersionOnly || updateMode === UpdateMode.Both) {
        changePackageJSON(packageFolderPath, nextPackageVersion);
        await updateUserAgent(packageFolderPath, nextPackageVersion)
    }
}

function changePackageJSON(packageFolderPath: string, packageVersion: string) {
    const data: string = fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8');
    const result = data.replace(/"version": "[0-9.a-z-]+"/g, '"version": "' + packageVersion + '"');
    fs.writeFileSync(path.join(packageFolderPath, 'package.json'), result, 'utf8');
}

export async function makeChangesForReleasingTrack2(
    packageFolderPath: string, 
    packageVersion: string, 
    changeLog: string, 
    originalChangeLogContent: string, 
    comparedVersion: string, 
    sdkReleaseDate: string, 
    updateMode: UpdateMode
) {
    let pacakgeVersionDetail = `## ${packageVersion} (${sdkReleaseDate})`;
    if (packageVersion.includes("beta")) {
        pacakgeVersionDetail += `\nCompared with version ${comparedVersion}`
    }
    const modifiedChangelogContent = `# Release History

${pacakgeVersionDetail}

${changeLog}
${originalChangeLogContent.replace(/.*Release History[\n\r]*/g, '')}`;

    // Decide how to handle changelog based on update mode
    if (updateMode === UpdateMode.ChangelogOnly || updateMode === UpdateMode.Both) {
        // Generate new changelog content
        fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), modifiedChangelogContent, { encoding: 'utf-8' });
    } else if (updateMode === UpdateMode.VersionOnly) {
        // Only update version information in existing changelog
        await updateChangelog(packageFolderPath, packageVersion, sdkReleaseDate);
    }

    // Decide whether to update version based on mode
    if (updateMode === UpdateMode.VersionOnly || updateMode === UpdateMode.Both) {
        changePackageJSON(packageFolderPath, packageVersion);
        await updateUserAgent(packageFolderPath, packageVersion);
    }
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