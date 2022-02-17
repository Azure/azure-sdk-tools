import {logger} from "../utils/logger";
import {execSync} from "child_process";

import fs from "fs";
import * as path from "path";
import {getChangedPackageDirectory} from "../utils/git";
import {generateChangelogAndBumpVersion} from "./utils/automaticGenerateChangeLogAndBumpVersion";
import {Changelog} from "../changelog/changelogGenerator";
import {changeRushJson} from "../utils/changeRushJson";
import {modifyOrGenerateCiYaml} from "../utils/changeCiYaml";
import {SwaggerSdkAutomationOutputPackageInfo} from "../common-types/swaggerSdkAutomation";
import {changeConfigOfTestAndSample, ChangeModel, SdkType} from "../utils/changeConfigOfTestAndSample";
import {changeReadmeMd} from "./utils/changeReadmeMd";

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
}) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);

    let cmd = `autorest --version=3.7.3 --typescript --modelerfour.lenient-model-deduplication --head-as-boolean=true --license-header=MICROSOFT_MIT_NO_VERSION --generate-test --typescript-sdks-folder=${options.sdkRepo} ${path.join(options.swaggerRepo, options.readmeMd)}`;

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

    const changedPackageDirectories: Set<string> = await getChangedPackageDirectory();
    for (const changedPackageDirectory of changedPackageDirectories) {
        const packagePath: string = path.join(options.sdkRepo, changedPackageDirectory);
        const swaggerSdkAutomationOutputPackageInfo: SwaggerSdkAutomationOutputPackageInfo = {
            "packageName": "",
            "path": [
                'rush.json',
                'common/config/rush/pnpm-lock.yaml'
            ],
            "readmeMd": [
                options.readmeMd
            ],
            "changelog": {
                "content": "",
                "hasBreakingChange": false
            },
            "artifacts": [],
            "result": "succeeded"
        };
        try {
            logger.logGreen(`Installing dependencies for ${changedPackageDirectory}...`);
            const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
            const packageName = packageJson.name;

            changeRushJson(options.sdkRepo, packageJson.name, changedPackageDirectory, 'management');

            // change configuration to skip build test, sample
            changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Hlc);

            logger.logGreen(`rush update`);
            execSync('rush update', {stdio: 'inherit'});
            logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
            execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
            logger.logGreen('Generating Changelog and Bumping Version...');
            const changelog: Changelog | undefined = await generateChangelogAndBumpVersion(changedPackageDirectory);
            logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`);
            execSync(`node common/scripts/install-run-rush.js pack --to ${packageJson.name} --verbose`, {stdio: 'inherit'});
            if (options.outputJson) {
                swaggerSdkAutomationOutputPackageInfo.packageName = packageJson.name;
                if (changelog) {
                    swaggerSdkAutomationOutputPackageInfo.changelog.hasBreakingChange = changelog.hasBreakingChange;
                    swaggerSdkAutomationOutputPackageInfo.changelog.content = changelog.displayChangeLog();
                    const breakingChangeItems = changelog.getBreakingChangeItems();
                    if (!!breakingChangeItems && breakingChangeItems.length > 0) {
                        swaggerSdkAutomationOutputPackageInfo.changelog['breakingChangeItems'] = breakingChangeItems;
                    }
                    const newPackageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
                    const newVersion = newPackageJson['version'];
                    swaggerSdkAutomationOutputPackageInfo['version'] = newVersion;
                }
                swaggerSdkAutomationOutputPackageInfo.path.push(path.dirname(changedPackageDirectory));
                for (const file of fs.readdirSync(packagePath)) {
                    if (file.startsWith('azure-arm') && file.endsWith('.tgz')) {
                        swaggerSdkAutomationOutputPackageInfo.artifacts.push(path.join(changedPackageDirectory, file));
                    }
                }
            }
            const metaInfo: any = {
                commit: options.gitCommitId,
                readme: options.readmeMd,
                autorest_command: cmd,
                repository_url: options.swaggerRepoUrl ? `${options.swaggerRepoUrl}.git` : 'https://github.com/Azure/azure-rest-api-specs.git'
            };
            if (options.tag) {
                metaInfo['tag'] = options.tag;
            }
            if (options.use) {
                metaInfo['use'] = options.use;
            }
            fs.writeFileSync(path.join(packagePath, '_meta.json'), JSON.stringify(metaInfo, undefined, '  '), {encoding: 'utf-8'});
            modifyOrGenerateCiYaml(options.sdkRepo, changedPackageDirectory, packageJson.name, true);
            changeReadmeMd(packagePath);

        } catch (e) {
            logger.logError('Error:');
            logger.logError(`An error occurred while run build for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            swaggerSdkAutomationOutputPackageInfo.result = 'failed';
        } finally {
            if (options.outputJson) {
                options.outputJson.packages.push(swaggerSdkAutomationOutputPackageInfo);
            }
            changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Hlc);
        }
    }

    logger.log(`>>>>>>>>>>>>>>>>>>> End: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    logger.log();
}
