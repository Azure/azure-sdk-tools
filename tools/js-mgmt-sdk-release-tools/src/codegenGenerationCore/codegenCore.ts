import {logger} from "../utils/logger";
import { execSync } from "child_process";

import fs from "fs";
import * as path from "path";
import {getChangedPackageDirectory} from "../utils/git";
import {generateChangelogAndBumpVersion} from "../changelogGenerationAndVersionBumpCore/automaticGenerateChangeLogAndBumpVersion";
import {Changelog} from "../changelogGenerationAndVersionBumpCore/changelogGenerator";

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

function changeRushJson(azureSDKForJSRepoRoot: string, packageName: any, relativePackageFolderPath: string) {
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
            projectFolder: relativePackageFolderPath,
            versionPolicyName: "management"
        });
        fs.writeFileSync(path.join(azureSDKForJSRepoRoot, 'rush.json'), commentJson.stringify(rushJson,undefined, 2), {encoding: 'utf-8'});
    }
}

function addExcludeBranch(branches: any) {
    if (branches && branches.include.includes('feature/*')) {
        if (!branches['exclude']) {
            branches['exclude'] = [];
        }
        if (!branches['exclude'].includes('feature/v4')) {
            branches['exclude'].push('feature/v4');
            return true;
        }
    }
    return false;
}

function addArtifact(artifacts: any, name: string, safeName: string) {
    if (!artifacts) return false;
    for (const artifact of artifacts) {
        if (name === artifact.name) return false;
    }
    artifacts.push({
        name: name,
        safeName: safeName
    });
    return true;
}

function modifyOrGenerateCiYaml(azureSDKForJSRepoRoot: string, changedPackageDirectory: string, packageName: string) {
    const relativeRpFolderPathRegexResult = /sdk\/[^\/]*\//.exec(changedPackageDirectory);
    if (relativeRpFolderPathRegexResult) {
        const relativeRpFolderPath = relativeRpFolderPathRegexResult[0];
        const rpFolderName = path.basename(relativeRpFolderPath);
        const rpFolderPath = path.join(azureSDKForJSRepoRoot, relativeRpFolderPath);
        const ciYamlPath = path.join(rpFolderPath, 'ci.yml');
        const name = packageName.replace('@', '').replace('/', '-');
        const safeName = name.replace(/-/g, '');
        if (fs.existsSync(ciYamlPath)) {
            const ciYaml = yaml.parse(fs.readFileSync(ciYamlPath, {encoding: 'utf-8'}));
            let changed = addExcludeBranch(ciYaml?.trigger?.branches);
            changed = addExcludeBranch(ciYaml?.pr?.branches) || changed;
            changed = addArtifact(ciYaml?.extends?.parameters?.Artifacts, name, safeName) || changed;
            if (changed) {
                fs.writeFileSync(ciYamlPath, yaml.stringify(ciYaml), {encoding: 'utf-8'});
            }
        } else {
            const ciYaml = `# NOTE: Please refer to https://aka.ms/azsdk/engsys/ci-yaml before editing this file.
trigger:
  branches:
    include:
      - main
      - release/*
      - hotfix/*
  paths:
    include:
      - ${relativeRpFolderPath}

pr:
  branches:
    include:
      - main
      - release/*
      - hotfix/*
  paths:
    include:
      - ${relativeRpFolderPath}

extends:
  template: ../../eng/pipelines/templates/stages/archetype-sdk-client.yml
  parameters:
    ServiceDirectory: ${rpFolderName}
    Artifacts:
      - name: ${name}
        safeName: ${safeName}
        `;
            fs.writeFileSync(ciYamlPath, ciYaml, {encoding: 'utf-8'});
        }
    }
}

export async function generateSdkAutomatically(azureSDKForJSRepoRoot: string, absoluteReadmeMd: string, relativeReadmeMd: string, gitCommitId: string, tag?: string, use?: string, useDebugger?: boolean, outputJson?: any, swaggerRepoUrl?: string) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${absoluteReadmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);

    let cmd = `autorest --version=3.1.3 --typescript --modelerfour.lenient-model-deduplication --head-as-boolean=true --license-header=MICROSOFT_MIT_NO_VERSION --generate-test --typescript-sdks-folder=${azureSDKForJSRepoRoot} ${absoluteReadmeMd}`;

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

    try {
        logger.logGreen('Executing command:');
        logger.logGreen('------------------------------------------------------------');
        logger.logGreen(cmd);
        logger.logGreen('------------------------------------------------------------');

        execSync(cmd, { stdio: 'inherit' });

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
                    const packageJson = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), { encoding: 'utf-8' }));

                    changeRushJson(azureSDKForJSRepoRoot, packageJson.name, changedPackageDirectory);

                    logger.logGreen(`rush update`);
                    execSync('rush update', { stdio: 'inherit' });
                    logger.logGreen(`node common/scripts/install-run-rush.js build --from ${packageJson.name} --verbose -p max`);
                    execSync(`node common/scripts/install-run-rush.js build --from ${packageJson.name} --verbose -p max`, { stdio: 'inherit' });
                    logger.logGreen('Generating Changelog and Bumping Version...');
                    const changelog: Changelog | undefined = await generateChangelogAndBumpVersion(changedPackageDirectory);
                    logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`);
                    execSync(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`, { stdio: 'inherit' });
                    if (outputJson) {
                        outputPackageInfo.packageName = 'track2_' + packageJson.name;
                        if (changelog) {
                            outputPackageInfo.changelog.hasBreakingChange = changelog.hasBreakingChange;
                            outputPackageInfo.changelog.content = changelog.displayChangeLog();
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
                        repository_url: swaggerRepoUrl? `${swaggerRepoUrl}.git` : 'https://github.com/Azure/azure-rest-api-specs.git'
                    };
                    if (tag) {
                        metaInfo['tag'] = tag;
                    }
                    if (use) {
                        metaInfo['use'] = use;
                    }
                    fs.writeFileSync(path.join(packageFolderPath, '_meta.json'), JSON.stringify(metaInfo, undefined, '  '), {encoding: 'utf-8'});
                    modifyOrGenerateCiYaml(azureSDKForJSRepoRoot, changedPackageDirectory, packageJson.name);
                } else {
                    throw 'find undefined packageFolderPath'
                }
            } catch (e) {
                logger.logError('Error:');
                logger.logError(`An error occurred while generating codes and run build for readme file: "${absoluteReadmeMd}":\nErr: ${e}\nStderr: "${e.stderr}\nStdout: "${e.stdout}"`);
                outputPackageInfo.result = 'failed';
            } finally {
                if (outputJson) {
                    outputJson.packages.push(outputPackageInfo);
                }
            }
        }
    } catch (err) {
        logger.log('Error:');
        logger.log(`An error occurred while generating client for readme file: "${absoluteReadmeMd}":\nErr: ${err}\nStderr: "${err.stderr}"`);
    }

    logger.log(`>>>>>>>>>>>>>>>>>>> End: "${absoluteReadmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    logger.log();
}
