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
import { generateChangelog } from "../utils/generateChangelog";
import {
    generateAutorestConfigurationFileForMultiClientByPrComment,
    generateAutorestConfigurationFileForSingleClientByPrComment, replaceRequireInAutorestConfigurationFile
} from '../utils/generateSampleReadmeMd';
import { updateTypeSpecProjectYamlFile } from '../utils/updateTypeSpecProjectYamlFile';
import { getRelativePackagePath } from "../utils/utils";

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
        if (!options.skipGeneration) {
            logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.typespecProject}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
            if(options.sdkGenerationType === "command") {
                logger.logGreen("Run TypeSpec command directly.");
                const copyPackageJsonName = 'emitter-package.json';
                logger.logGreen(`copy package.json file if not exist from SDK repo ${copyPackageJsonName}`);
                const installCommand = prepareCommandToInstallDependenciesForTypeSpecProject(path.join(options.sdkRepo, 'eng', copyPackageJsonName), path.join(options.swaggerRepo, options.typespecProject, 'package.json'));
                logger.logGreen(installCommand);
                execSync(installCommand, {
                    stdio: 'inherit',
                    cwd: path.join(options.swaggerRepo, options.typespecProject)
                });
                updateTypeSpecProjectYamlFile(path.join(options.swaggerRepo, options.typespecProject, 'tspconfig.yaml'), options.sdkRepo, options.typespecEmitter);
                let typespecSource = '.';
                if (fs.existsSync(path.join(options.swaggerRepo, options.typespecProject, 'client.tsp'))) {
                    typespecSource = 'client.tsp';
                }
                logger.logGreen(`npx tsp compile ${typespecSource} --emit ${options.typespecEmitter} --arg "js-sdk-folder=${options.sdkRepo}"`);
                execSync(`npx tsp compile ${typespecSource} --emit ${options.typespecEmitter} --arg "js-sdk-folder=${options.sdkRepo}"`, {
                    stdio: 'inherit',
                    cwd: path.join(options.swaggerRepo, options.typespecProject)
                });
                logger.logGreen("End with TypeSpec command.");
            } else {
                logger.logGreen("Run ./eng/common/scripts/TypeSpec-Project-Process.ps1 script directly.");
                const tspDefDir = path.join(options.swaggerRepo, options.typespecProject);
                const scriptCommand = ['pwsh', './eng/common/scripts/TypeSpec-Project-Process.ps1', tspDefDir,  options.gitCommitId, options.swaggerRepoUrl].join(" ");
                logger.logGreen(`${scriptCommand}`);
                execSync(scriptCommand, {stdio: 'inherit'});
                logger.logGreen("End with ./eng/common/scripts/TypeSpec-Project-Process.ps1 script.");
            } 
        }
    } else {
        logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
        if (!options.skipGeneration) {
            let autorestConfigFilePath: string | undefined;
            let isMultiClient: boolean = false;
            if (!!options.autorestConfig) {
                logger.logGreen(`Find autorest configuration in PR comment: ${options.autorestConfig}`);
                logger.logGreen(`Parsing the autorest configuration in PR comment`);
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
                } catch (e) {
                    logger.logError(`Encounter error when parsing autorestConfig from PR comment: \nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
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
                logger.logGreen(`Don't find autorest configuration in spec PR comment, and trying to find it in sdk repository`);
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
                                logger.logError(`Find ${regexExecResult.length} match in ${currentAutorestConfigFilePath}. The autorest configuration file should only contain one require with one readme.md file`);
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
                logger.logWarn(`Don't find autorest configuration in spec PR comment or sdk repository, skip generating codes`);
                logger.logWarn(`If you ensure there is autorest configuration file in sdk repository, please make sure it contains require keyword and the corresponding readme.md in swagger repository.`);
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

            logger.logGreen('Executing command:');
            logger.logGreen('------------------------------------------------------------');
            logger.logGreen(cmd);
            logger.logGreen('------------------------------------------------------------');
            try {
                execSync(cmd, {stdio: 'inherit', cwd: path.dirname(autorestConfigFilePath)});
            } catch (e) {
                throw new Error(`An error occurred while generating codes for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            }
        }
    }

    const outputPackageInfo = getOutputPackageInfo(options.runningEnvironment, options.readmeMd, options.typespecProject);

    try {
        if (!packagePath || !relativePackagePath) {
            const changedPackageDirectories: Set<string> = await getChangedPackageDirectory(!options.skipGeneration);
            if (changedPackageDirectories.size !== 1) {
                throw new Error(`Find unexpected changed package directory. Length: ${changedPackageDirectories}. Value: ${[...changedPackageDirectories].join(', ')}. Please only change files in one directory`)
            }
            for (const d of changedPackageDirectories) relativePackagePath = d;
            packagePath = path.join(options.sdkRepo, relativePackagePath!);
        }

        if (!packagePath || !relativePackagePath) {
            throw new Error(`Failed to get package path`);
        }

        const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
        const packageName = packageJson.name;
        logger.logGreen(`Generate some other files for ${packageName} in ${packagePath}...`);
        if (!options.skipGeneration) {
            await modifyOrGenerateCiYml(options.sdkRepo, packagePath, packageName, false);

            await changeRushJson(options.sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');

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

        logger.logGreen(`rush update...`);
        execSync('rush update', {stdio: 'inherit'});
        logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
        // To build generated codes except test and sample, we need to change tsconfig.json.
        execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
        logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`);
        execSync(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`, {stdio: 'inherit'});
        if (!options.skipGeneration) {
            logger.logGreen(`Generate changelog`);
            await generateChangelog(packagePath);
        }
        if (options.outputJson && options.runningEnvironment !== undefined && outputPackageInfo !== undefined) {
            for (const file of fs.readdirSync(packagePath)) {
                if (file.startsWith('azure-rest') && file.endsWith('.tgz')) {
                    outputPackageInfo.artifacts.push(path.join(relativePackagePath, file));
                }
            }
            addApiViewInfo(outputPackageInfo, packagePath, relativePackagePath);
        }
    } catch (e) {
        logger.logError('Error:');
        if (options.typespecProject) {
            logger.logError(`An error occurred while run build for typespec project: "${options.typespecProject}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
        } else {
            logger.logError(`An error occurred while run build for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
        }
        if (outputPackageInfo) {
            outputPackageInfo.result = 'failed';
        }
    } finally {
        if (options.outputJson && outputPackageInfo) {
            options.outputJson.packages.push(outputPackageInfo);
        }
        if (!options.skipGeneration && !!packagePath) {
            changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
        }
    }
}
