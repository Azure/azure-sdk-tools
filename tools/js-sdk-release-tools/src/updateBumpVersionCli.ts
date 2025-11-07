#!/usr/bin/env node

import { updateBumpVersion } from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import { logger } from "./utils/logger.js";
import fs from "fs";
import path from "path";

const updateBumpVersionCli = async (
    packageFolderPath: string | undefined,
    sdkReleaseType?: string,
    sdkVersion?: string,
    sdkReleaseDate?: string
) => {
    if (!packageFolderPath) {
        logger.error(`Invalid package path '${packageFolderPath}'.`);
        return;
    }

    // Validate that for mgmt packages, at least one of SdkVersion or SdkReleaseType is provided
    try {
        const packageJsonPath = path.join(packageFolderPath, 'package.json');
        if (fs.existsSync(packageJsonPath)) {
            const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, { encoding: 'utf-8' }));
            const sdkType = packageJson['sdk-type'];
            
            if (sdkType === 'mgmt') {
                if (!sdkReleaseType && !sdkVersion) {
                    logger.error(
                        `For management-plane (mgmt) packages, at least one of SdkVersion or SdkReleaseType MUST be provided. ` +
                        `Package: ${packageJson.name || packageFolderPath}`
                    );
                    process.exit(1);
                }
                
                logger.info(`Management-plane package detected. SdkReleaseType: ${sdkReleaseType || 'not provided'}, SdkVersion: ${sdkVersion || 'not provided'}`);
            }
        }
    } catch (error) {
        logger.error(`Failed to validate package requirements: ${(error as Error)?.message || error}`);
        process.exit(1);
    }

    await updateBumpVersion(packageFolderPath, sdkReleaseType, sdkVersion, sdkReleaseDate);
};

const optionDefinitions = [
    { name: "PackagePath", type: String, defaultOption: true },
    { name: "SdkReleaseType", type: String },
    { name: "SdkVersion", type: String },
    { name: "SdkReleaseDate", type: String }
];

import commandLineArgs from "command-line-args";
const options = commandLineArgs(optionDefinitions);

updateBumpVersionCli(options.PackagePath, options.SdkReleaseType, options.SdkVersion, options.SdkReleaseDate);
