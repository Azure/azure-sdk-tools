import { execSync } from "child_process";
import fs from "fs";
import * as yaml from "js-yaml";
import * as path from "path";
import { addApiViewInfo } from "../../utils/addApiViewInfo";
import { modifyOrGenerateCiYml } from "../../utils/changeCiYaml";
import { changeConfigOfTestAndSample, ChangeModel, SdkType } from "../../utils/changeConfigOfTestAndSample";
import { changeRushJson } from "../../utils/changeRushJson";
import { getOutputPackageInfo } from "../../utils/getOutputPackageInfo";
import { getChangedCiYmlFilesInSpecificFolder, getChangedPackageDirectory } from "../../utils/git";
import { logger } from "../../utils/logger";
import { RunningEnvironment } from "../../utils/runningEnvironment";
import { prepareCommandToInstallDependenciesForTypeSpecProject } from '../utils/prepareCommandToInstallDependenciesForTypeSpecProject';
import {
    generateAutorestConfigurationFileForMultiClientByPrComment,
    generateAutorestConfigurationFileForSingleClientByPrComment, replaceRequireInAutorestConfigurationFile
} from '../utils/generateSampleReadmeMd';
import { updateTypeSpecProjectYamlFile } from '../utils/updateTypeSpecProjectYamlFile';
import { getRelativePackagePath } from "../utils/utils";
import { defaultChildProcessTimeout, getGeneratedPackageDirectory } from "../../common/utils";
import { remove } from 'fs-extra';
import { generateChangelogAndBumpVersion } from "../../common/changlog/automaticGenerateChangeLogAndBumpVersion";
import { updateChangelogResult } from "../../common/packageResultUtils";

export async function generateRLCInPipeline(options: {
    sdkRepo: string;
    swaggerRepo: string;
    readmeMd: string | undefined;
    typespecProject: string | undefined;
    autorestConfig: string | undefined;
    sdkGenerationType: "script" | "command";
    swaggerRepoUrl: string;
    gitCommitId: string;
    typespecEmitter: string;
    use?: string;
    outputJson?: any;
    additionalArgs?: string;
    skipGeneration?: boolean, 
    runningEnvironment?: RunningEnvironment;
}) {
    let packagePath: string | undefined = undefined;
    let relativePackagePath: string | undefined = undefined;
    if (options.typespecProject) {
        const typespecProject = path.join(options.swaggerRepo, options.typespecProject); 
        const generatedPackageDir = await getGeneratedPackageDirectory(typespecProject, options.sdkRepo);
        await remove(generatedPackageDir);

        if (!options.skipGeneration) {
            logger.info(`Start to generate rest level client SDK from '${options.typespecProject}'.`);
            // TODO: remove it, since this function is used in pipeline.
            if(options.sdkGenerationType === "command") {
                logger.info("Start to run TypeSpec command directly.");
                const copyPackageJsonName = 'emitter-package.json';
                logger.info(`Start to copy package.json file if not exist from SDK repo '${copyPackageJsonName}'.`);
                const installCommand = prepareCommandToInstallDependenciesForTypeSpecProject(path.join(options.sdkRepo, 'eng', copyPackageJsonName), path.join(options.swaggerRepo, options.typespecProject, 'package.json'));
                logger.info(`Start to run command: '${installCommand}'`);
                execSync(installCommand, {
                    stdio: 'inherit',
                    cwd: path.join(options.swaggerRepo, options.typespecProject)
                });
                updateTypeSpecProjectYamlFile(path.join(options.swaggerRepo, options.typespecProject, 'tspconfig.yaml'), options.sdkRepo, options.typespecEmitter);
                let typespecSource = '.';
                if (fs.existsSync(path.join(options.swaggerRepo, options.typespecProject, 'client.tsp'))) {
                    typespecSource = 'client.tsp';
                }
                logger.info(`Start to run command: 'npx tsp compile ${typespecSource} --emit ${options.typespecEmitter} --arg "js-sdk-folder=${options.sdkRepo}"'.`);
                execSync(`npx tsp compile ${typespecSource} --emit ${options.typespecEmitter} --arg "js-sdk-folder=${options.sdkRepo}"`, {
                    stdio: 'inherit',
                    cwd: path.join(options.swaggerRepo, options.typespecProject)
                });
                logger.info("End with TypeSpec command.");
            } else {
                logger.info("Start to generate code by tsp-client.");
                const tspDefDir = path.join(options.swaggerRepo, options.typespecProject);
                const scriptCommand = ['tsp-client', 'init', '--debug', '--tsp-config', path.join(tspDefDir, 'tspconfig.yaml'), '--local-spec-repo', tspDefDir, '--repo', options.swaggerRepo, '--commit', options.gitCommitId].join(" ");
                logger.info(`Start to run command: '${scriptCommand}'`);
                execSync(scriptCommand, {stdio: 'inherit'});
                logger.info("Generated code by tsp-client successfully.");
            } 
        }
    } else {
        logger.info(`Start to generate SDK from '${options.readmeMd}'.`);
        if (!options.skipGeneration) {
            let autorestConfigFilePath: string | undefined;
            let isMultiClient: boolean = false;
            if (!!options.autorestConfig) {
                logger.info(`Start to find autorest configuration in PR comment: '${options.autorestConfig}'.`);
                logger.info(`Start to parse the autorest configuration in PR comment.`);
                const yamlBlocks: {
                    condition: string;
                    yamlContent: any;
                }[] = [];
                try {
                    const regexToExtractAutorestConfig = new RegExp(
                        '(?<=``` *(?<condition>yaml.*)\\r\\n)(?<yaml>[^(```)]*)(?=\\r\\n```)', 'g');
                    let match = regexToExtractAutorestConfig.exec(options.autorestConfig);
                    while (!!match) {
                        if (!!match.groups) {
                            // try to load the yaml to check whether it's valid
                            yamlBlocks.push({
                                condition: match.groups.condition,
                                yamlContent: yaml.load(match.groups.yaml)
                            });
                        }
                        match = regexToExtractAutorestConfig.exec(options.autorestConfig);
                    }
                } catch (e: any) {
                    logger.error(`Failed to parse autorestConfig from PR comment: \nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
                    logger.error(`Please check out https://github.com/Azure/autorest/blob/main/docs/troubleshooting.md to troubleshoot the issue.`);
                    throw e;
                }

                yamlBlocks.forEach(e => {
                    if (e.condition.includes(`multi-client`)) {
                        isMultiClient = true;
                    }
                });

                if (isMultiClient) {
                    autorestConfigFilePath = await generateAutorestConfigurationFileForMultiClientByPrComment(yamlBlocks, options.swaggerRepo, options.sdkRepo);
                } else {
                    if (yamlBlocks.length !== 1) {
                        throw new Error(`The yaml config in comment should be 1, but find autorestConfig length: ${yamlBlocks.length}`);
                    }
                    const yamlContent = yamlBlocks[0].yamlContent;
                    autorestConfigFilePath = await generateAutorestConfigurationFileForSingleClientByPrComment(yamlContent, options.swaggerRepo, options.sdkRepo);
                }
            } else {
                logger.info(`Autorest configuration is not found in spec PR comment, and start to find it in sdk repository.`);
                const sdkFolderPath = path.join(options.sdkRepo, 'sdk');
                for (const rp of fs.readdirSync(sdkFolderPath)) {
                    if (!!autorestConfigFilePath) break;
                    const rpFolderPath = path.join(sdkFolderPath, rp);
                    if (fs.lstatSync(rpFolderPath).isDirectory()) {
                        for (const packageFolder of fs.readdirSync(rpFolderPath)) {
                            if (!!autorestConfigFilePath) break;
                            if (!packageFolder.endsWith('-rest')) continue;
                            const packageFolderPath = path.join(rpFolderPath, packageFolder);
                            if (!fs.lstatSync(packageFolderPath).isDirectory()) {
                                continue;
                            }
                            const currentAutorestConfigFilePath = path.join(packageFolderPath, 'swagger', 'README.md');
                            if (!fs.existsSync(currentAutorestConfigFilePath)) {
                                continue;
                            }
                            const autorestConfigFilterRegex = new RegExp(`require:[\\s]*-?[\\s]*(.*${options.readmeMd!.replace(/\//g, '\\/').replace(/\./, '\\.')})`);
                            const regexExecResult = autorestConfigFilterRegex.exec(fs.readFileSync(currentAutorestConfigFilePath, 'utf-8'));
                            if (!regexExecResult || regexExecResult.length < 2) {
                                continue;
                            }
                            if (regexExecResult.length !== 2) {
                                logger.error(`Found ${regexExecResult.length} matches in '${currentAutorestConfigFilePath}'. The autorest configuration file should only contain one require with one readme.md file`);
                                continue;
                            }
                            replaceRequireInAutorestConfigurationFile(currentAutorestConfigFilePath, regexExecResult[1], path.join(options.swaggerRepo, options.readmeMd!));
                            autorestConfigFilePath = currentAutorestConfigFilePath;
                            isMultiClient = fs.readFileSync(currentAutorestConfigFilePath, 'utf-8').includes('multi-client');
                            break;
                        }
                    }
                }
            }

            if (!autorestConfigFilePath) {
                logger.warn(`Don't find autorest configuration in spec PR comment or sdk repository, skip generating codes.`);
                logger.warn(`If you ensure there is autorest configuration file in sdk repository, please make sure it contains require keyword and the corresponding readme.md in swagger repository.`);
                return;
            }

            packagePath = path.dirname(path.dirname(autorestConfigFilePath));
            relativePackagePath = path.relative(options.sdkRepo, packagePath);

            let cmd = `autorest --version=3.9.7 ${path.basename(autorestConfigFilePath)} --output-folder=${packagePath}`;
            if (options.use) {
                cmd += ` --use=${options.use}`;
            }
            if (options.additionalArgs) {
                cmd += ` ${options.additionalArgs}`;
            }
            if (isMultiClient) {
                cmd += ` --multi-client=true`;
            }

            logger.info(`Start to run command: ${cmd}.`);
            try {
                execSync(cmd, {stdio: 'inherit', cwd: path.dirname(autorestConfigFilePath), timeout: defaultChildProcessTimeout});
            } catch (e: any) {
                throw new Error(`Failed to generate codes for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            }
        }
    }

    const outputPackageInfo = getOutputPackageInfo(options.runningEnvironment, options.readmeMd, options.typespecProject);

    try {
        // TODO: need to refactor
        // too tricky here, when relativePackagePath === undefined,
        // the project should be typespec,
        // and the changedPackageDirectories should be join(service-dir, package-dir)
        if (!packagePath || !relativePackagePath) {
            const changedPackageDirectories: Set<string> = await getChangedPackageDirectory(!options.skipGeneration);
            if (changedPackageDirectories.size !== 1) {
                throw new Error(`Find unexpected changed package directory. Length: ${changedPackageDirectories.size}. Value: ${[...changedPackageDirectories].join(', ')}. Please only change files in one directory`)
            }
            for (const d of changedPackageDirectories) relativePackagePath = d;
            packagePath = path.join(options.sdkRepo, relativePackagePath!);
        }

        if (!packagePath || !relativePackagePath) {
            throw new Error(`Failed to get package path`);
        }

        const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
        const packageName = packageJson.name;
        logger.info(`Start to generate some other files for '${packageName}' in '${packagePath}'.`);
        if (!options.skipGeneration) {
            await modifyOrGenerateCiYml(options.sdkRepo, packagePath, packageName, false);

            await changeRushJson(options.sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');

            // TODO: remove it for typespec project, since no need now, the test and sample are decouple from build
            // change configuration to skip build test, sample
            changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);
        }

        if (options.outputJson && options.runningEnvironment !== undefined && outputPackageInfo !== undefined) {
            outputPackageInfo.packageName = packageName;
            outputPackageInfo['version'] = packageJson.version;
            outputPackageInfo.path.push(relativePackagePath);
            for (const file of await getChangedCiYmlFilesInSpecificFolder(path.dirname(relativePackagePath))) {
                outputPackageInfo.path.push(file);
            }
            if (options.runningEnvironment === RunningEnvironment.SdkGeneration) {
                outputPackageInfo.packageFolder = relativePackagePath;
            }
        }

        logger.info(`Start to update rush.`);
        execSync('node common/scripts/install-run-rush.js update', {stdio: 'inherit'});
        logger.info(`Start to build '${packageName}', except for tests and samples, which may be written manually.`);
        // To build generated codes except test and sample, we need to change tsconfig.json.
        execSync(`node common/scripts/install-run-rush.js build -t ${packageName} --verbose`, {stdio: 'inherit'});
        logger.info(`Start to run command 'node common/scripts/install-run-rush.js pack --to ${packageName} --verbose'.`);
        execSync(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`, {stdio: 'inherit'});
        if (!options.skipGeneration) {
            const changelog = await generateChangelogAndBumpVersion(relativePackagePath);
            outputPackageInfo.changelog.breakingChangeItems = changelog?.getBreakingChangeItems() ?? [];
            outputPackageInfo.changelog.content = changelog?.displayChangeLog() ?? '';
            outputPackageInfo.changelog.hasBreakingChange = changelog?.hasBreakingChange ?? false;
        }
        if (options.outputJson && options.runningEnvironment !== undefined && outputPackageInfo !== undefined) {
            for (const file of fs.readdirSync(packagePath)) {
                if (file.startsWith('azure-rest') && file.endsWith('.tgz')) {
                    outputPackageInfo.artifacts.push(path.join(relativePackagePath, file));
                }
            }
            addApiViewInfo(outputPackageInfo, packagePath, relativePackagePath);
        }
    } catch (e: any) {
        if (options.typespecProject) {
            logger.error(`Failed to build typespec project: "${options.typespecProject}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}".`);
            logger.error(`Please check out https://github.com/Azure/autorest.typescript/blob/main/packages/typespec-ts/CONTRIBUTING.md#how-to-debug to troubleshoot the issue.`);
        } else {
            logger.error(`Failed to build for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}".`);
            logger.error(`Please check out https://github.com/Azure/autorest/blob/main/docs/troubleshooting.md to troubleshoot the issue.`);
        }
        if (outputPackageInfo) {
            outputPackageInfo.result = 'failed';
        }
        throw e;
    } finally {
        if (options.outputJson && outputPackageInfo) {
            options.outputJson.packages.push(outputPackageInfo);
        }
        if (!options.skipGeneration && !!packagePath) {
            changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
        }
    }
}
