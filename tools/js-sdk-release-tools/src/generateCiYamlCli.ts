#!/usr/bin/env node

import commandLineArgs from "command-line-args";
import { createOrUpdateCiYaml } from "./common/ciYamlUtils.js";
import { getNpmPackageInfo, getNpmPackageName } from "./common/npmUtils.js";
import { SDKType, VersionPolicyName } from "./common/types.js";
import { getSDKType } from "./common/utils.js";
import { logger } from "./utils/logger.js";
import path from "path";

const generateCiYamlCli = async (
    sdkRepoPath: string | undefined,
    packageFolderPath: string | undefined
) => {
    if (!sdkRepoPath || !packageFolderPath) {
        logger.error(`SdkRepoPath and PackagePath are required.`);
        logger.error(
            `Usage: generate-ci-yaml --sdkRepoPath <SdkRepoPath> --packagePath <PackagePath>`
        );
        process.exit(1);
    }

    // Calculate relative path from sdkRepoPath to packagePath (packagePath is always absolute)
    const normalizedSdkRepoPath = path.resolve(sdkRepoPath);
    const absolutePackagePath = path.resolve(packageFolderPath);
    const relativePackagePath = path.relative(normalizedSdkRepoPath, absolutePackagePath);

    logger.info(`SDK Repo Path: ${normalizedSdkRepoPath}`);
    logger.info(`Package Path (absolute): ${absolutePackagePath}`);
    logger.info(`Package Path (relative): ${relativePackagePath}`);

    const sdkType = getSDKType(absolutePackagePath);
    if (sdkType !== SDKType.ModularClient) {
        logger.info(`Skipping CI yaml generation for non-ModularClient SDK type: ${sdkType}`);
        return;
    }

    const npmPackageInfo = await getNpmPackageInfo(absolutePackagePath);
    const packageName = getNpmPackageName(npmPackageInfo);
    const versionPolicyName: VersionPolicyName = packageName.includes("arm-") ? "management" : "client";
    logger.info(`Detected versionPolicyName: ${versionPolicyName} for package: ${packageName}`);

    const ciPath = await createOrUpdateCiYaml(
        relativePackagePath.replace(/\\/g, "/"),
        versionPolicyName,
        npmPackageInfo
    );

    logger.info(`CI yaml file created/updated at: ${ciPath}`);
};

const optionDefinitions = [
    { name: "sdkRepoPath", type: String },
    { name: "packagePath", type: String },
];

const options = commandLineArgs(optionDefinitions);

generateCiYamlCli(options["sdkRepoPath"], options["packagePath"]);
