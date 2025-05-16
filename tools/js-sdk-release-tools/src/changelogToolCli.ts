#!/usr/bin/env node

import { generateChangelogAndBumpVersion } from "./common/changlog/automaticGenerateChangeLogAndBumpVersion.js";
import { logger } from "./utils/logger.js";

const changelogToolCli = async (options: {
    packagePath: string;
    apiVersion: string;
    sdkReleaseType: string;
  }) => {
    if (!options.packagePath) {
        logger.error(`Invalid package path '${options.packagePath}'.`);
    } else {
        await generateChangelogAndBumpVersion(options.packagePath, {
            apiVersion: options.apiVersion,
            sdkReleaseType: options.sdkReleaseType,
        });
    }
};

const optionDefinitions = [
    { name: "packagePath", type: String },
    { name: "apiVersion", type: String },
    { name: "sdkReleaseType", type: String },
];
import commandLineArgs from "command-line-args";
const options = commandLineArgs(optionDefinitions);

changelogToolCli(options);
