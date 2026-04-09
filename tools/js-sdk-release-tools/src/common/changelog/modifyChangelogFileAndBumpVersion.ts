import { updateUserAgent } from "../../xlc/codeUpdate/updateUserAgent.js";
import { UpdateMode } from "./automaticGenerateChangeLogAndBumpVersion.js";

import fs from 'fs';
import * as path from 'path';
import { getSDKType } from "../utils.js";
import { ModularSDKType, SDKType } from "../types.js";
import { isBetaVersion } from "../../utils/version.js";
import { getModularSDKType } from "../../utils/generateInputUtils.js";

function escapeRegex(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export function getFirstReleaseContent(packageFolderPath: string, isStableRelease: boolean) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
    const sdkType = getSDKType(packageFolderPath);
    const firstBetaContent = `Initial release of the ${packageJsonData.name} package`;
    const firstStableContent = `This is the first stable version with the package of ${packageJsonData.name}`;
    const hlcClientContent = `The package of ${packageJsonData.name} is using our next generation design principles. To learn more, please refer to our documentation <a href="https://aka.ms/azsdk/js/mgmt/quickstart">Quick Start</a>.`
    switch (sdkType) {
        case SDKType.ModularClient:
            if (getModularSDKType(packageFolderPath) === ModularSDKType.ManagementPlane) {
                return isStableRelease
                    ? `This is the first stable release of the ${packageJsonData.name} package. It introduces a new SDK generation with layered APIs, smaller bundles, and improved ergonomics. For more details, see the https://aka.ms/azsdk/js/sdk/quickstart.`
                    : `This is the first preview release of the ${packageJsonData.name} package. It introduces a new SDK generation with layered APIs, smaller bundles, and improved ergonomics. For more details, see the https://aka.ms/azsdk/js/sdk/quickstart.`;
            }
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
    const newVersion = isStableRelease ? '1.0.0' : '1.0.0-beta.1';
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
    const sdkType = getSDKType(packageFolderPath);
    let migrationContent: string;
    if (sdkType === SDKType.ModularClient && getModularSDKType(packageFolderPath) === ModularSDKType.ManagementPlane) {
        migrationContent = `The ${packageJsonData.name} package has been upgraded to a new SDK generation that provides layered APIs, smaller bundles, and improved ergonomics. Starting from version ${nextPackageVersion}, this release includes breaking changes.

To migrate existing applications, see the https://aka.ms/azsdk/js/sdk/migration. For more information, refer to the https://aka.ms/azsdk/js/sdk/quickstart.
`;
    } else {
        migrationContent = `The package of ${packageJsonData.name} is using our next generation design principles since version ${nextPackageVersion}, which contains breaking changes.

To understand the detail of the change, please refer to [Changelog](https://aka.ms/js-track2-changelog).

To migrate the existing applications to the latest version, please refer to [Migration Guide](https://aka.ms/js-track2-migration-guide).

To learn more, please refer to our documentation [Quick Start](https://aka.ms/azsdk/js/mgmt/quickstart).
`;
    }
    const content = `# Release History
    
## ${nextPackageVersion} (${sdkReleaseDate})
### Features Added

${migrationContent}`;
    
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
    let result = data.replace(/"version": "[0-9.a-z-]+"/g, '"version": "' + packageVersion + '"');
    result = updateApiRefLink(result, packageVersion);
    fs.writeFileSync(path.join(packageFolderPath, 'package.json'), result, 'utf8');
}

export function updateApiRefLink(packageJsonContent: string, packageVersion: string): string {
    // Remove existing ?view=azure-node-preview from apiRefLink
    let result = packageJsonContent.replace(
        /(\"apiRefLink\"\s*:\s*\"[^"]*)\?view=azure-node-preview([^"]*\")/g,
        '$1$2'
    );
    // For beta/preview versions, add ?view=azure-node-preview back to apiRefLink
    if (isBetaVersion(packageVersion)) {
        result = result.replace(
            /(\"apiRefLink\"\s*:\s*\"https:\/\/[^"?]*)(\")/g,
            '$1?view=azure-node-preview$2'
        );
    }
    return result;
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
        const versionPattern = escapeRegex(currentVersion);
        modifiedChangelogContent = modifiedChangelogContent.replace(new RegExp(versionPattern, 'g'), newVersion);
        
        // Replace the current release date with sdkReleaseDate if provided
        if (sdkReleaseDate) {
            const datePattern = escapeRegex(currentReleaseDate);
            // Only replace the date in the first heading line
            const firstHeadingPattern = new RegExp(`(##\\s*${escapeRegex(newVersion)}\\s*)\\(${datePattern}\\)`);
            modifiedChangelogContent = modifiedChangelogContent.replace(firstHeadingPattern, `$1(${sdkReleaseDate})`);
        }
    }
    
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), modifiedChangelogContent, { encoding: 'utf-8' });
}