import fs from "fs";
import path from "path";
import shell from "shelljs";
import { logger } from "../../utils/logger.js";
import { getVersion, isBetaVersion } from "../../utils/version.js";
import { ApiVersionType, SDKType } from "../types.js";
import { getApiVersionType } from "../../xlc/apiVersion/apiVersionTypeExtractor.js";
import { getNpmPackageName } from "../utils.js";
import { tryGetNpmView } from "../npmUtils.js";
import { getChangedPackageDirectory } from "../../utils/git.js";
import { getGeneratedPackageDirectory } from "../../common/utils.js";
import { posix } from "node:path";
import * as yaml from "js-yaml";
import {
    generateAutorestConfigurationFileForSingleClientByPrComment,
    generateAutorestConfigurationFileForMultiClientByPrComment,
} from "../../llc/utils/generateSampleReadmeMd.js";
export async function generateCodeOwnersAndIgnoreLink(
    sdkType: SDKType,
    options: {
        skipGeneration?: boolean;
        typespecProject?: string;
        typeSpecDirectory: string;
        swaggerRepo: string;
        sdkRepo: string;
        readmeMd: string | undefined;
        autorestConfig: string | undefined;
    },
) {
    if (SDKType.HighLevelClient === sdkType) {
        logger.info(`Start with HighLevelClient.`);
        const changedPackageDirectories: Set<string> =
            await getChangedPackageDirectory(!options.skipGeneration);
        for (const packageFolderPath of changedPackageDirectories) {
            await generateCodeOwnersAndIgnoreLinkForPackage(packageFolderPath);
        }
    } else if (SDKType.RestLevelClient === sdkType) {
        logger.info(`Start with RestLevelClient.`);
        let relativePackagePath: string | undefined;

        if (options.typespecProject) {
            const typespecProject = path.join(
                options.swaggerRepo,
                options.typespecProject,
            );
            const generatedPackageDir = await getGeneratedPackageDirectory(
                typespecProject,
                options.sdkRepo,
            );
            relativePackagePath = path.relative(
                options.sdkRepo,
                generatedPackageDir,
            );
        } else {
            logger.info(`Start to generate SDK from '${options.readmeMd}'.`);
            if (!options.skipGeneration) {
                let autorestConfigFilePath: string | undefined;
                let isMultiClient: boolean = false;
                if (!!options.autorestConfig) {
                    logger.info(
                        `Start to find autorest configuration in PR comment: '${options.autorestConfig}'.`,
                    );
                    logger.info(
                        `Start to parse the autorest configuration in PR comment.`,
                    );
                    const yamlBlocks: {
                        condition: string;
                        yamlContent: any;
                    }[] = [];
                    try {
                        const regexToExtractAutorestConfig = new RegExp(
                            "(?<=``` *(?<condition>yaml.*)\\r\\n)(?<yaml>[^(```)]*)(?=\\r\\n```)",
                            "g",
                        );
                        let match = regexToExtractAutorestConfig.exec(
                            options.autorestConfig,
                        );
                        while (!!match) {
                            if (!!match.groups) {
                                // try to load the yaml to check whether it's valid
                                yamlBlocks.push({
                                    condition: match.groups.condition,
                                    yamlContent: yaml.load(match.groups.yaml),
                                });
                            }
                            match = regexToExtractAutorestConfig.exec(
                                options.autorestConfig,
                            );
                        }
                    } catch (e: any) {
                        logger.error(
                            `Failed to parse autorestConfig from PR comment: \nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`,
                        );
                        logger.error(
                            `Please check out https://github.com/Azure/autorest/blob/main/docs/troubleshooting.md to troubleshoot the issue.`,
                        );
                        throw e;
                    }

                    yamlBlocks.forEach((e) => {
                        if (e.condition.includes(`multi-client`)) {
                            isMultiClient = true;
                        }
                    });

                    if (isMultiClient) {
                        autorestConfigFilePath =
                            await generateAutorestConfigurationFileForMultiClientByPrComment(
                                yamlBlocks,
                                options.swaggerRepo,
                                options.sdkRepo,
                            );
                    } else {
                        if (yamlBlocks.length !== 1) {
                            throw new Error(
                                `The yaml config in comment should be 1, but find autorestConfig length: ${yamlBlocks.length}`,
                            );
                        }
                        const yamlContent = yamlBlocks[0].yamlContent;
                        autorestConfigFilePath =
                            await generateAutorestConfigurationFileForSingleClientByPrComment(
                                yamlContent,
                                options.swaggerRepo,
                                options.sdkRepo,
                            );
                    }
                } else {
                    const sdkFolderPath = path.join(options.sdkRepo, "sdk");
                    for (const rp of fs.readdirSync(sdkFolderPath)) {
                        logger.info(
                            `Start to find autorest configuration in '${rp}'.`,
                        );
                        if (!!autorestConfigFilePath) break;
                        const rpFolderPath = path.join(sdkFolderPath, rp);
                        if (fs.lstatSync(rpFolderPath).isDirectory()) {
                            for (const packageFolder of fs.readdirSync(
                                rpFolderPath,
                            )) {
                                if (!!autorestConfigFilePath) break;
                                if (!packageFolder.endsWith("-rest")) continue;

                                const packageFolderPath = path.join(
                                    rpFolderPath,
                                    packageFolder,
                                );
                                if (
                                    !fs
                                        .lstatSync(packageFolderPath)
                                        .isDirectory()
                                ) {
                                    continue;
                                }
                                const currentAutorestConfigFilePath = path.join(
                                    packageFolderPath,
                                    "swagger",
                                    "README.md",
                                );
                                if (
                                    !fs.existsSync(
                                        currentAutorestConfigFilePath,
                                    )
                                ) {
                                    continue;
                                }

                                const autorestConfigFilterRegex = new RegExp(
                                    `require:[\\s]*-?[\\s]*(.*${options.readmeMd!.replace(/\//g, "\\/").replace(/\./, "\\.")})`,
                                );
                                const autoRestConfigContent = fs.readFileSync(
                                    currentAutorestConfigFilePath,
                                    "utf-8",
                                );
                                const regexExecResult =
                                    autorestConfigFilterRegex.exec(
                                        autoRestConfigContent,
                                    );
                                const requireFoundOnlyOne =
                                    regexExecResult &&
                                    regexExecResult.length === 2;

                                const InputFilePattern = new RegExp(
                                    `input-file:.*${path.dirname(options.readmeMd!)}.*`,
                                );
                                const containsInputFile = InputFilePattern.test(
                                    autoRestConfigContent,
                                );

                                if (containsInputFile || requireFoundOnlyOne) {
                                    // NOTE: it can be overrided from other RPs
                                    autorestConfigFilePath =
                                        currentAutorestConfigFilePath;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!autorestConfigFilePath) {
                    return;
                }

                const packagePath = path.dirname(
                    path.dirname(autorestConfigFilePath),
                );
                relativePackagePath = path.relative(
                    options.sdkRepo,
                    packagePath,
                );
            }
        }

        if (!relativePackagePath) {
            throw new Error(`Failed to get package path`);
        }
        if (!options.skipGeneration) {
            await generateCodeOwnersAndIgnoreLinkForPackage(
                relativePackagePath,
            );
        }
    } else {
        logger.info(`Start with ModularClient.`);
        logger.info(`options: ${JSON.stringify(options)}`);
        const typeSpecDirectory = path.posix.join(
            options.typeSpecDirectory,
            options.typespecProject!,
        );
        const packageDirectory = await getGeneratedPackageDirectory(
            typeSpecDirectory,
            options.sdkRepo.replaceAll("\\", "/"),
        );
        const relativePackageDirToSdkRoot = posix.relative(
            posix.normalize(options.sdkRepo.replaceAll("\\", "/")),
            posix.normalize(packageDirectory),
        );
        await generateCodeOwnersAndIgnoreLinkForPackage(
            relativePackageDirToSdkRoot,
        );
    }
}
export async function generateCodeOwnersAndIgnoreLinkForPackage(
    packageFolderPath: string,
) {
    logger.info(
        `Start to generate CODEOWNERS and Ignore Link for ${packageFolderPath}`,
    );
    const jsSdkRepoPath = String(shell.pwd());
    const packageAbsolutePath = path.join(jsSdkRepoPath, packageFolderPath);
    const ApiType = await getApiVersionType(packageAbsolutePath);
    const isStableRelease = ApiType != ApiVersionType.Preview;
    const packageName = getNpmPackageName(packageAbsolutePath);
    const npmViewResult = await tryGetNpmView(packageName);
    const stableVersion = getVersion(npmViewResult, "latest");
    logger.info(`npmViewResult: ${npmViewResult}`);
    logger.info(`stableVersion: ${stableVersion}`);
    if (!npmViewResult) {
        logger.info(
            `Package ${packageName} is first beta release, start to generate CODEOWNERS and ignore link for first beta release.`,
        );
        updateCODEOWNERS(packageFolderPath);
        updateIgnoreLink(packageName);
        logger.info(
            `Generated updates for CODEOWNERS and ignore link successfully`,
        );
    }
}
function updateCODEOWNERS(packagePath: string) {
    const jsSdkRepoPath = String(shell.pwd());
    const codeownersPath = path.join(jsSdkRepoPath, ".github", "CODEOWNERS");
    let content = fs.readFileSync(codeownersPath, "utf8");

    // Insert content before Config section
    const configSectionIndex = content.indexOf(
        "###########\n# Config\n###########",
    );
    if (configSectionIndex !== -1) {
        const newContentBeforeConfig = `# PRLabel: %Mgmt\n${packagePath}/ @qiaozha @MaryGao\n`;
        content =
            content.slice(0, configSectionIndex) +
            newContentBeforeConfig +
            "\n" +
            content.slice(configSectionIndex);
    }
    fs.writeFileSync(codeownersPath, content);
}

function updateIgnoreLink(packageName: string) {
    const jsSdkRepoPath = String(shell.pwd());
    const ignoreLinksPath = path.join(jsSdkRepoPath, "eng", "ignore-links.txt");
    const content = fs.readFileSync(ignoreLinksPath, "utf8");
    const newLine = `https://learn.microsoft.com/javascript/api/${packageName}?view=azure-node-preview`;
    const updatedContent = content + newLine + "\n";
    fs.writeFileSync(ignoreLinksPath, updatedContent);
}
