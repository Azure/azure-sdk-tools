import {Changelog} from "../../changelog/changelogGenerator.js";
import { updateUserAgent } from "../../xlc/codeUpdate/updateUserAgent.js";

import fs from 'fs';
import * as path from 'path';
import { getSDKType} from "../utils.js";
import { SDKType, ModularSDKType } from "../types.js";
import { getModularSDKType } from "../../utils/generateInputUtils.js";
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

export async function makeChangesForFirstRelease(packageFolderPath: string, isStableRelease: boolean) {
    const newVersion = isStableRelease? '1.0.0' : '1.0.0-beta.1';
    const contentLog = getFirstReleaseContent(packageFolderPath, isStableRelease);
    const content = `# Release History
    
## ${newVersion} (${date})

### Features Added

${contentLog}
`;
    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), content, 'utf8');
    changePackageJSON(packageFolderPath, newVersion);
    await updateUserAgent(packageFolderPath, newVersion);
}

export async function makeChangesForMigrateTrack1ToTrack2(packageFolderPath: string, nextPackageVersion: string) {
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
    changePackageJSON(packageFolderPath, nextPackageVersion);
    await updateUserAgent(packageFolderPath, nextPackageVersion)
}

function changePackageJSON(packageFolderPath: string, packageVersion: string) {
    const data: string = fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8');
    const result = data.replace(/"version": "[0-9.a-z-]+"/g, '"version": "' + packageVersion + '"');
    fs.writeFileSync(path.join(packageFolderPath, 'package.json'), result, 'utf8');
}

export async function makeChangesForReleasingTrack2(packageFolderPath: string, packageVersion: string, changeLog: string, originalChangeLogContent: string, comparedVersion:string) {
    let pacakgeVersionDetail = `## ${packageVersion} (${date})`;
    if(packageVersion.includes("beta")){
        pacakgeVersionDetail +=`\nCompared with version ${comparedVersion}`
    }
    
    const modularSDKType = getModularSDKType(packageFolderPath);
    let finalChangeLog: string;
    
    if (modularSDKType === ModularSDKType.DataPlane) {
        finalChangeLog = `Please manually update the changelog with the appropriate changes.\n`;
    } else {
        finalChangeLog = changeLog;
    }
    
    const modifiedChangelogContent = `# Release History

${pacakgeVersionDetail}

${finalChangeLog}
${originalChangeLogContent.replace(/.*Release History[\n\r]*/g, '')}`;

    fs.writeFileSync(path.join(packageFolderPath, 'CHANGELOG.md'), modifiedChangelogContent, {encoding: 'utf-8'});

    changePackageJSON(packageFolderPath, packageVersion);
    await updateUserAgent(packageFolderPath, packageVersion);
}
