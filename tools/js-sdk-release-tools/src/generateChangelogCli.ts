#!/usr/bin/env node

import { generateChangelog } from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import { logger } from "./utils/logger.js";

const generateChangelogCli = async (jsSdkRepoPath: string | undefined, packageFolderPath: string | undefined) => {
    if (!jsSdkRepoPath) {
        logger.error(`Invalid SDK repo path '${jsSdkRepoPath}'.`);
        return;
    }

    if (!packageFolderPath) {
        logger.error(`Invalid package path '${packageFolderPath}'.`);
        return;
    }

    await generateChangelog(jsSdkRepoPath, packageFolderPath);
};

const optionDefinitions = [
    { name: "SdkRepoPath", type: String, defaultOption: true },
    { name: "PackagePath", type: String },
];

import commandLineArgs from "command-line-args";
const options = commandLineArgs(optionDefinitions);

generateChangelogCli(options.SdkRepoPath, options.PackagePath);
