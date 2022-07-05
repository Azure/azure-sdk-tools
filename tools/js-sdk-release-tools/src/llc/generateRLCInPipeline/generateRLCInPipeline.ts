import { execSync } from "child_process";
import fs from "fs";
import * as yaml from "js-yaml";
import * as path from "path";
import { addApiViewInfo } from "../../utils/addApiViewInfo";
import { modifyOrGenerateCiYml } from "../../utils/changeCiYaml";
import { changeConfigOfTestAndSample, ChangeModel, SdkType } from "../../utils/changeConfigOfTestAndSample";
import { changeRushJson } from "../../utils/changeRushJson";
import { getOutputPackageInfo } from "../../utils/getOutputPackageInfo";
import { getChangedCiYmlFilesInSpecificFolder } from "../../utils/git";
import { logger } from "../../utils/logger";
import { RunningEnvironment } from "../../utils/runningEnvironment";
import { generateChangelog } from "../utils/generateChangelog";
import {
    generateAutorestConfigurationFileForMultiClientByPrComment,
    generateAutorestConfigurationFileForSingleClientByPrComment, replaceRequireInAutorestConfigurationFile
} from '../utils/generateSampleReadmeMd';
import { getRelativePackagePath } from "../utils/utils";

export async function generateRLCInPipeline(options: {
    sdkRepo: string;
    swaggerRepo: string;
    readmeMd: string;
    autorestConfig: string | undefined
    use?: string;
    outputJson?: any;
    additionalArgs?: string;
    runningEnvironment?: RunningEnvironment;
}) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
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
                    const autorestConfigFilterRegex = new RegExp(`require:[\\s]*-?[\\s]*(.*${options.readmeMd.replace(/\//g, '\\/').replace(/\./, '\\.')})`);
                    const regexExecResult = autorestConfigFilterRegex.exec(fs.readFileSync(currentAutorestConfigFilePath, 'utf-8'));
                    if (!regexExecResult || regexExecResult.length < 2) {
                        continue;
                    }
                    if (regexExecResult.length !== 2) {
                        logger.logError(`Find ${regexExecResult.length} match in ${currentAutorestConfigFilePath}. The autorest configuration file should only contain one require with one readme.md file`);
                        continue;
                    }
                    replaceRequireInAutorestConfigurationFile(currentAutorestConfigFilePath, regexExecResult[1], path.join(options.swaggerRepo, options.readmeMd));
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

    const packagePath = path.dirname(path.dirname(autorestConfigFilePath));
    const relativePackagePath = path.relative(options.sdkRepo, packagePath);

    let cmd = `autorest --version=3.8.2 ${path.basename(autorestConfigFilePath)} --output-folder=${packagePath} --debug`;
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

    const outputPackageInfo = getOutputPackageInfo(options.runningEnvironment, options.readmeMd);

    try {
        const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
        const packageName = packageJson.name;
        logger.logGreen(`Generate some other files for ${packageName} in ${packagePath}...`);
        await modifyOrGenerateCiYml(options.sdkRepo, packagePath, packageName, false);
        await changeRushJson(options.sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');

        // change configuration to skip build test, sample
        changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);

        logger.logGreen(`rush update...`);
        execSync('rush update', {stdio: 'inherit'});
        logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
        // To build generated codes except test and sample, we need to change tsconfig.json.
        execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
        logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`);
        execSync(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`, {stdio: 'inherit'});
        logger.logGreen(`Generate changelog`);
        await generateChangelog(packagePath);
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
            for (const file of fs.readdirSync(packagePath)) {
                if (file.startsWith('azure-rest') && file.endsWith('.tgz')) {
                    outputPackageInfo.artifacts.push(path.join(relativePackagePath, file));
                }
            }
            addApiViewInfo(outputPackageInfo, packagePath, relativePackagePath);
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
        changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
    }
}
