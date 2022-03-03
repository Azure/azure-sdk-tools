import fs from "fs";
import path from "path";
import {isBetaVersion} from "../../utils/version";

async function getTrack2StartedVersion(packageName: string) {
    if (!packageName) return undefined;
    let track2StartedVersion: string | undefined;
    const axios = require('axios');
    const stable = require('semver-stable');
    const response = await axios.get(`https://registry.npmjs.com/${packageName}`);
    const info = response.data;
    const latestPackageVersion = info['dist-tags']['latest'];
    let hasTrack1Package: boolean = false;
    if (info['versions'][latestPackageVersion] && info['versions'][latestPackageVersion]['sdk-type'] === 'mgmt') { // stable release track2
        const stableVersions = Object.keys(info['versions']).filter((value, index, array) => {
            return stable.is(value);
        });
        for (let index = stableVersions.length - 1; index >= 0; index--) {
            if (info['versions'][stableVersions[index]] && info['versions'][stableVersions[index]]['sdk-type'] === 'mgmt') {
                track2StartedVersion = stableVersions[index];
            } else {
                hasTrack1Package = true;
                break;
            }
        }
    }
    return hasTrack1Package ? track2StartedVersion : undefined;
}

export async function changeReadmeMd(packageFolderPath: string) {
    if (!fs.existsSync(path.join(packageFolderPath, 'package.json')) || !fs.existsSync(path.join(packageFolderPath, 'README.md'))) return;
    const packageJson = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), {encoding: 'utf-8'}));
    let isPreview = isBetaVersion(packageJson.version);
    let track2StartedVersion = await getTrack2StartedVersion(packageJson.name);
    let content = fs.readFileSync(path.join(packageFolderPath, 'README.md'), {encoding: 'utf-8'});
    content = content.replace(/\?view=azure-node-preview/, '');
    if (isPreview) {
        const match = /API reference documentation[^ )]*/.exec(content);
        if (!!match) {
            content = content.replace(match[0], `${match[0]}?view=azure-node-preview`)
        }
    }

    if (!!track2StartedVersion) {
        const match = /This package contains an isomorphic SDK \(runs both in Node\.js and in browsers\) for.*/gm.exec(content);
        if (!!match) {
            content = content.replace(match[0], `${match[0]}
            
⚠️This package ${packageJson.name} with versions lower than ${track2StartedVersion} are going to be deprecated in March 2022, we strongly recommend you to upgrade your dependency on it to version ${track2StartedVersion} or above as soon as possible. The deprecate means, it starts the end of support for that library. You can continue to use the libraries indefinitely (as long as the service is running), but after 1 year, no further bug fixes or security fixes will be provided. To migrate to the new version, please check [Migration Guide](https://aka.ms/js-track2-migration-guide)`);
        }

    }

    fs.writeFileSync(path.join(packageFolderPath, 'README.md'), content, {encoding: 'utf-8'});

}
