import {logger} from "../utils/logger";
import {execSync} from "child_process";

import fs from "fs";
import * as path from "path";
import {getChangedPackageDirectory} from "../utils/git";
import {generateChangelogAndBumpVersion} from "./automaticGenerateChangeLogAndBumpVersion";
import {Changelog} from "../changelog/changelogGenerator";
import {changeRushJson} from "../utils/changeRushJson";
import {modifyOrGenerateCiYaml} from "../utils/changeCiYaml";
import {isBetaVersion} from "../utils/version";

const commentJson = require('comment-json');
const yaml = require('yaml');

export interface OutputPackageInfo {
    packageName: string;
    path: string[];
    readmeMd: string[];
    changelog: {
        content: string;
        hasBreakingChange: boolean;
    };
    artifacts: string[];
    result: string;
}

function changeReadmeMd(packageFolderPath: string) {
    let isPreview = false;
    if (fs.existsSync(path.join(packageFolderPath, 'package.json'))) {
        const packageJson = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), {encoding: 'utf-8'}));
        isPreview = isBetaVersion(packageJson.version)
    }
    if (fs.existsSync(path.join(packageFolderPath, 'README.md'))) {
        let content = fs.readFileSync(path.join(packageFolderPath, 'README.md'), {encoding: 'utf-8'});
        content = content.replace(/\?view=azure-node-preview/, '');
        if (isPreview) {
            const match = /API reference documentation[^ )]*/.exec(content);
            if (!!match) {
                content = content.replace(match[0], `${match[0]}?view=azure-node-preview`)
            }
        }
        fs.writeFileSync(path.join(packageFolderPath, 'README.md'), content, {encoding: 'utf-8'});
    }

}

export async function generateSdkAutomatically(azureSDKForJSRepoRoot: string, absoluteReadmeMd: string, relativeReadmeMd: string, gitCommitId: string, tag?: string, use?: string, useDebugger?: boolean, additionalArgs?: string, outputJson?: any, swaggerRepoUrl?: string) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${absoluteReadmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);

    let cmd = `autorest --version=3.7.3 --typescript --modelerfour.lenient-model-deduplication --head-as-boolean=true --license-header=MICROSOFT_MIT_NO_VERSION --generate-test --typescript-sdks-folder=${azureSDKForJSRepoRoot} ${absoluteReadmeMd}`;

    if (tag) {
        cmd += ` --tag=${tag}`;
    }

    if (use) {
        cmd += ` --use=${use}`;
    } else {
        const localAutorestTypeScriptFolderPath = path.resolve(azureSDKForJSRepoRoot, '..', 'autorest.typescript');
        if (fs.existsSync(localAutorestTypeScriptFolderPath) && fs.lstatSync(localAutorestTypeScriptFolderPath).isDirectory()) {
            cmd += ` --use=${localAutorestTypeScriptFolderPath}`;
        }
    }

    if (useDebugger) {
        cmd += ` --typescript.debugger`;
    }

    if (additionalArgs) {
        cmd += ` ${additionalArgs}`;
    }

    logger.logGreen('Executing command:');
    logger.logGreen('------------------------------------------------------------');
    logger.logGreen(cmd);
    logger.logGreen('------------------------------------------------------------');
    try {
        execSync(cmd, {stdio: 'inherit'});
    } catch (e) {
        throw new Error(`An error occurred while generating codes for readme file: "${absoluteReadmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
    }

    const changedPackageDirectories: Set<string> = await getChangedPackageDirectory();
    for (const changedPackageDirectory of changedPackageDirectories) {
        const outputPackageInfo: OutputPackageInfo = {
            "packageName": "",
            "path": [
                'rush.json',
                'common/config/rush/pnpm-lock.yaml'
            ],
            "readmeMd": [
                relativeReadmeMd
            ],
            "changelog": {
                "content": "",
                "hasBreakingChange": false
            },
            "artifacts": [],
            "result": "succeeded"
        };
        try {
            const packageFolderPath: string = path.join(azureSDKForJSRepoRoot, changedPackageDirectory);
            logger.logGreen(`Installing dependencies for ${changedPackageDirectory}...`);
            if (packageFolderPath) {
                const packageJson = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), {encoding: 'utf-8'}));

                changeRushJson(azureSDKForJSRepoRoot, packageJson.name, changedPackageDirectory, 'management');

                logger.logGreen(`rush update`);
                execSync('rush update', {stdio: 'inherit'});
                logger.logGreen(`node common/scripts/install-run-rush.js build --from ${packageJson.name} --verbose -p max`);
                execSync(`node common/scripts/install-run-rush.js build --from ${packageJson.name} --verbose -p max`, {stdio: 'inherit'});
                logger.logGreen('Generating Changelog and Bumping Version...');
                const changelog: Changelog | undefined = await generateChangelogAndBumpVersion(changedPackageDirectory);
                logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`);
                execSync(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`, {stdio: 'inherit'});
                if (outputJson) {
                    outputPackageInfo.packageName = 'track2_' + packageJson.name;
                    if (changelog) {
                        outputPackageInfo.changelog.hasBreakingChange = changelog.hasBreakingChange;
                        outputPackageInfo.changelog.content = changelog.displayChangeLog();
                        const breakingChangeItems = changelog.getBreakingChangeItems();
                        if (!!breakingChangeItems && breakingChangeItems.length > 0) {
                            outputPackageInfo.changelog['breakingChangeItems'] = breakingChangeItems;
                        }
                        const newPackageJson = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), {encoding: 'utf-8'}));
                        ;
                        const newVersion = newPackageJson['version'];
                        outputPackageInfo['version'] = newVersion;
                    }
                    outputPackageInfo.path.push(path.dirname(changedPackageDirectory));
                    for (const file of fs.readdirSync(packageFolderPath)) {
                        if (file.startsWith('azure-arm') && file.endsWith('.tgz')) {
                            outputPackageInfo.artifacts.push(path.join(changedPackageDirectory, file));
                        }
                    }
                }
                const metaInfo: any = {
                    commit: gitCommitId,
                    readme: relativeReadmeMd,
                    autorest_command: cmd,
                    repository_url: swaggerRepoUrl ? `${swaggerRepoUrl}.git` : 'https://github.com/Azure/azure-rest-api-specs.git'
                };
                if (tag) {
                    metaInfo['tag'] = tag;
                }
                if (use) {
                    metaInfo['use'] = use;
                }
                fs.writeFileSync(path.join(packageFolderPath, '_meta.json'), JSON.stringify(metaInfo, undefined, '  '), {encoding: 'utf-8'});
                modifyOrGenerateCiYaml(azureSDKForJSRepoRoot, changedPackageDirectory, packageJson.name, true);
                changeReadmeMd(packageFolderPath);
            } else {
                throw 'find undefined packageFolderPath'
            }
        } catch (e) {
            logger.logError('Error:');
            logger.logError(`An error occurred while run build for readme file: "${absoluteReadmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            outputPackageInfo.result = 'failed';
        } finally {
            if (outputJson) {
                outputJson.packages.push(outputPackageInfo);
            }
        }
    }

    logger.log(`>>>>>>>>>>>>>>>>>>> End: "${absoluteReadmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    logger.log();
}
