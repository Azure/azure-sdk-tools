import {logger} from "../utils/logger";
import {execSync} from "child_process";

import fs from "fs";
import * as path from "path";
import {getChangedCiYmlFilesInSpecificFolder, getChangedPackageDirectory} from "../utils/git";
import {generateChangelogAndBumpVersion} from "./utils/automaticGenerateChangeLogAndBumpVersion";
import {Changelog} from "../changelog/changelogGenerator";
import {changeRushJson} from "../utils/changeRushJson";
import {modifyOrGenerateCiYml} from "../utils/changeCiYaml";
import {changeConfigOfTestAndSample, ChangeModel, SdkType} from "../utils/changeConfigOfTestAndSample";
import {changeReadmeMd} from "./utils/changeReadmeMd";
import {RunningEnvironment} from "../utils/runningEnvironment";
import {getOutputPackageInfo} from "../utils/getOutputPackageInfo";
import {getReleaseTool} from "./utils/getReleaseTool";
import { addApiViewInfo } from "../utils/addApiViewInfo";

export async function generateMgmt(options: {
    sdkRepo: string,
    swaggerRepo: string,
    readmeMd: string;
    gitCommitId: string,
    tag?: string,
    use?: string,
    additionalArgs?: string;
    outputJson?: any;
    swaggerRepoUrl?: string;
    downloadUrlPrefix?: string;
    skipGeneration?: boolean,
    runningEnvironment?: RunningEnvironment;
}) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    let cmd = '';
    if (!options.skipGeneration) {
        cmd = `autorest --version=3.9.7 --typescript --modelerfour.lenient-model-deduplication --azure-arm --head-as-boolean=true --license-header=MICROSOFT_MIT_NO_VERSION --generate-test --typescript-sdks-folder=${options.sdkRepo} ${path.join(options.swaggerRepo, options.readmeMd)}`;

        if (options.tag) {
            cmd += ` --tag=${options.tag}`;
        }

        if (options.use) {
            cmd += ` --use=${options.use}`;
        }

        if (options.additionalArgs) {
            cmd += ` ${options.additionalArgs}`;
        }

        logger.logGreen('Executing command:');
        logger.logGreen('------------------------------------------------------------');
        logger.logGreen(cmd);
        logger.logGreen('------------------------------------------------------------');
        try {
            execSync(cmd, {stdio: 'inherit'});
        } catch (e) {
            throw new Error(`An error occurred while generating codes for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
        }
    }

    const changedPackageDirectories: Set<string> = await getChangedPackageDirectory(!options.skipGeneration);
    for (const changedPackageDirectory of changedPackageDirectories) {
        const packagePath: string = path.join(options.sdkRepo, changedPackageDirectory);
        let outputPackageInfo = getOutputPackageInfo(options.runningEnvironment, options.readmeMd, undefined);

        try {
            logger.logGreen(`Installing dependencies for ${changedPackageDirectory}...`);
            const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
            const packageName = packageJson.name;

            if (!options.skipGeneration) {
                changeRushJson(options.sdkRepo, packageJson.name, changedPackageDirectory, 'management');

                // change configuration to skip build test, sample
                changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Hlc);

                const metaInfo: any = {
                    commit: options.gitCommitId,
                    readme: options.readmeMd,
                    autorest_command: cmd,
                    repository_url: options.swaggerRepoUrl ? `${options.swaggerRepoUrl}.git` : 'https://github.com/Azure/azure-rest-api-specs.git',
                    release_tool: getReleaseTool()
                };
                if (options.tag) {
                    metaInfo['tag'] = options.tag;
                }
                if (options.use) {
                    metaInfo['use'] = options.use;
                }

                fs.writeFileSync(path.join(packagePath, '_meta.json'), JSON.stringify(metaInfo, null, '  '), {encoding: 'utf-8'});
                modifyOrGenerateCiYml(options.sdkRepo, changedPackageDirectory, packageName, true);
            }

            // @ts-ignore
            if (options.outputJson && options.runningEnvironment !== undefined && outputPackageInfo !== undefined) {
                outputPackageInfo.packageName = packageJson.name;

                if (options.runningEnvironment === RunningEnvironment.SdkGeneration) {
                    outputPackageInfo.packageFolder = changedPackageDirectory;
                }

                outputPackageInfo.path.push(changedPackageDirectory);
                for (const file of await getChangedCiYmlFilesInSpecificFolder(path.dirname(changedPackageDirectory))) {
                    outputPackageInfo.path.push(file);
                }
            }

            logger.logGreen(`rush update`);
            execSync('rush update', {stdio: 'inherit'});
            logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
            execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
            logger.logGreen('Generating Changelog and Bumping Version...');
            let changelog: Changelog | undefined;
            if (!options.skipGeneration) {
                changelog = await generateChangelogAndBumpVersion(changedPackageDirectory);
            }
            logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`);
            execSync(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`, {stdio: 'inherit'});
            if (!options.skipGeneration) {
                changeReadmeMd(packagePath);
            }

            // @ts-ignore
            if (options.outputJson && options.runningEnvironment !== undefined && outputPackageInfo !== undefined) {
                if (changelog) {
                    outputPackageInfo.changelog.hasBreakingChange = changelog.hasBreakingChange;
                    outputPackageInfo.changelog.content = changelog.displayChangeLog();
                    const breakingChangeItems = changelog.getBreakingChangeItems();
                    if (!!breakingChangeItems && breakingChangeItems.length > 0) {
                        outputPackageInfo.changelog['breakingChangeItems'] = breakingChangeItems;
                    } else {
                        outputPackageInfo.changelog['breakingChangeItems'] = [];
                    }
                }
                
                const newPackageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
                const newVersion = newPackageJson['version'];
                outputPackageInfo['version'] = newVersion;

                let artifactName: string | undefined = undefined;
                for (const file of fs.readdirSync(packagePath)) {
                    if (file.startsWith('azure-arm') && file.endsWith('.tgz')) {
                        outputPackageInfo.artifacts.push(path.join(changedPackageDirectory, file));
                        artifactName = file;
                    }
                }
                addApiViewInfo(outputPackageInfo, packagePath, changedPackageDirectory);
                if (!outputPackageInfo.packageName.startsWith('@azure/arm-')) {
                    throw new Error(`Unexpected package name: ${outputPackageInfo.packageName}`);
                }
                if (!!options.downloadUrlPrefix && !!artifactName) {
                    outputPackageInfo.installInstructions = {
                        full: `Please install the package by \`npm install ${options.downloadUrlPrefix}${outputPackageInfo.packageName.replace('/', '_')}/${artifactName}\``
                    }
                }
            }
        } catch (e) {
            logger.logError('Error:');
            logger.logError(`An error occurred while run build for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            if (outputPackageInfo) {
                outputPackageInfo.result = 'failed';
            }
        } finally {
            if (options.outputJson && outputPackageInfo) {
                options.outputJson.packages.push(outputPackageInfo);
            }
            if (!options.skipGeneration) {
                changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Hlc);
            }
        }
    }

    logger.log(`>>>>>>>>>>>>>>>>>>> End: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    logger.log();
}
