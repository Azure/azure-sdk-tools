#!/usr/bin/env node

import { generateChangelog } from "./common/changelog/automaticGenerateChangeLogAndBumpVersion.js";
import { logger } from "./utils/logger.js";

const generateChangelogCli = async (packageFolderPath: string | undefined) => {
    if (!packageFolderPath) {
        logger.error(`PackagePath is required. Usage: generateChangelogCli <PackagePath>`);
        return;
    }

    await generateChangelog(packageFolderPath);
};

const optionDefinitions = [
    { name: "PackagePath", type: String, defaultOption: true },
];

import commandLineArgs from "command-line-args";
const options = commandLineArgs(optionDefinitions);

generateChangelogCli(options.PackagePath);
