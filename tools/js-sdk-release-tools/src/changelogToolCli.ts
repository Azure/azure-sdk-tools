#!/usr/bin/env node

import { generateChangelogAndBumpVersion } from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import { logger } from "./utils/logger.js";

const changelogToolCli = async (
    packageFolderPath: string | undefined,
    apiVersion?: string,
    sdkReleaseType?: string
) => {
    if (!packageFolderPath) {
        logger.error(`Invalid package path '${packageFolderPath}'.`);
        return;
    }

    await generateChangelogAndBumpVersion(packageFolderPath, {
        apiVersion,
        sdkReleaseType,
    });
};

const optionDefinitions = [
    { name: "packagePath", type: String, defaultOption: true },
    { name: "apiVersion", type: String },
    { name: "sdkReleaseType", type: String },
];
import commandLineArgs from "command-line-args";
const options = commandLineArgs(optionDefinitions);

changelogToolCli(options.packagePath, options.apiVersion, options.sdkReleaseType);
