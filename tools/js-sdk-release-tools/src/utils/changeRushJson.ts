import fs from "fs";
import path from "path";
const commentJson = require('comment-json');

export function changeRushJson(azureSDKForJSRepoRoot: string, packageName: any, relativePackageFolderPath: string, versionPolicyName: string) {
    const rushJson = commentJson.parse(fs.readFileSync(path.join(azureSDKForJSRepoRoot, 'rush.json'), { encoding: 'utf-8' }));
    const projects: any[] = rushJson.projects;
    let exist = false;
    for (const project of projects) {
        if (project.packageName === packageName) {
            exist = true;
            break;
        }
    }
    if (!exist) {
        projects.push({
            packageName: packageName,
            projectFolder: relativePackageFolderPath.replace(/\\/g, '/'),
            versionPolicyName: versionPolicyName
        });
        fs.writeFileSync(path.join(azureSDKForJSRepoRoot, 'rush.json'), commentJson.stringify(rushJson,undefined, 2), {encoding: 'utf-8'});
    }
}